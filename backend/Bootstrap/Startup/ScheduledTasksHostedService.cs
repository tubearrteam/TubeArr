using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using TubeArr.Backend.Data;
using TubeArr.Backend.Realtime;

namespace TubeArr.Backend;

internal sealed class ScheduledTasksHostedService : BackgroundService
{
	const int MaxRetries = 2;
	static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

	readonly IServiceScopeFactory _scopeFactory;
	readonly ILogger<ScheduledTasksHostedService> _logger;
	readonly ConcurrentDictionary<string, bool> _runningTasks = new(StringComparer.OrdinalIgnoreCase);

	public ScheduledTasksHostedService(
		IServiceScopeFactory scopeFactory,
		ILogger<ScheduledTasksHostedService> logger)
	{
		_scopeFactory = scopeFactory;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("Scheduled tasks worker started.");

		try
		{
			await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
			return;
		}

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await TickAsync(stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Scheduled tasks tick failed.");
			}

			try
			{
				await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
		}

		_logger.LogInformation("Scheduled tasks worker stopped.");
	}

	async Task TickAsync(CancellationToken ct)
	{
		using var scope = _scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
		var commandState = scope.ServiceProvider.GetRequiredService<InMemoryCommandState>();
		var dispatcher = scope.ServiceProvider.GetRequiredService<CommandDispatcher>();
		var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
		var realtime = scope.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>();
		var metadata = scope.ServiceProvider.GetRequiredService<ChannelMetadataAcquisitionService>();
		var youTubeDataApi = scope.ServiceProvider.GetRequiredService<YouTubeDataApiMetadataService>();

		var now = DateTimeOffset.UtcNow;
		var states = await db.ScheduledTaskStates.AsNoTracking().ToListAsync(ct);
		var byName = states.ToDictionary(x => x.TaskName, StringComparer.OrdinalIgnoreCase);
		var mediaManagement = await db.MediaManagementConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		var customNfosEnabled = mediaManagement?.UseCustomNfos != false;
		var plexProvider = await db.PlexProviderConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		var downloadNewThumbnailsTaskEnabled = LibraryThumbnailExportPolicy.ShouldExport(
			mediaManagement?.DownloadLibraryThumbnails == true,
			plexProvider?.Enabled == true);

		var overrides = await db.ScheduledTaskIntervalOverrides.AsNoTracking()
			.ToDictionaryAsync(x => x.TaskName, x => x.IntervalMinutes, StringComparer.OrdinalIgnoreCase, ct);

		foreach (var entry in ScheduledTaskCatalog.Entries)
		{
			if (!ScheduledTaskCatalog.RecordsRuns(entry.TaskName))
				continue;

			if (string.Equals(entry.TaskName, "SyncCustomNfos", StringComparison.OrdinalIgnoreCase) && !customNfosEnabled)
				continue;
			if (string.Equals(entry.TaskName, "RepairLibraryNfosAndArtwork", StringComparison.OrdinalIgnoreCase) && !downloadNewThumbnailsTaskEnabled)
				continue;

			var interval = entry.Interval;
			if (interval > 0 && overrides.TryGetValue(entry.TaskName, out var ovr) && ovr > 0)
				interval = ovr;
			if (interval <= 0)
				continue;

			byName.TryGetValue(entry.TaskName, out var state);
			var last = state?.LastCompletedAt;
			var due = (last ?? ScheduledTaskCatalog.ProcessStartUtc).AddMinutes(interval);
			if (now < due)
				continue;

			if (commandState.IsCommandNameRunning(entry.TaskName))
				continue;

			if (!_runningTasks.TryAdd(entry.TaskName, true))
			{
				_logger.LogDebug("Skipping scheduled task {TaskName} — already executing.", entry.TaskName);
				continue;
			}

			try
			{
				var payload = JsonSerializer.SerializeToElement(new Dictionary<string, string>
				{
					["name"] = entry.TaskName,
					["trigger"] = "scheduled"
				});

				_logger.LogInformation("Running scheduled task {TaskName}.", entry.TaskName);

				for (var attempt = 0; ; attempt++)
				{
					try
					{
						await dispatcher.DispatchAsync(payload, db, _scopeFactory, logger, realtime, metadata, youTubeDataApi);
						break;
					}
					catch (OperationCanceledException) { throw; }
					catch (Exception ex) when (attempt < MaxRetries)
					{
						_logger.LogWarning(ex, "Scheduled task {TaskName} failed (attempt {Attempt}/{Max}), retrying in {Delay}s.",
							entry.TaskName, attempt + 1, MaxRetries + 1, RetryDelay.TotalSeconds);
						await Task.Delay(RetryDelay, ct);
					}
					catch (Exception ex)
					{
						_logger.LogWarning(ex, "Scheduled task {TaskName} failed after {Max} attempts, giving up.",
							entry.TaskName, MaxRetries + 1);
						break;
					}
				}
			}
			finally
			{
				_runningTasks.TryRemove(entry.TaskName, out _);
			}
		}
	}

}
