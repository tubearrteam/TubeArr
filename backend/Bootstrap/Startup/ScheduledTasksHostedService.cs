using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TubeArr.Backend.Data;
using TubeArr.Backend.Realtime;

namespace TubeArr.Backend;

internal sealed class ScheduledTasksHostedService : BackgroundService
{
	readonly IServiceScopeFactory _scopeFactory;
	readonly ILogger<ScheduledTasksHostedService> _logger;

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

		foreach (var entry in ScheduledTaskCatalog.Entries)
		{
			if (entry.Interval <= 0 || !ScheduledTaskCatalog.RecordsRuns(entry.TaskName))
				continue;

			byName.TryGetValue(entry.TaskName, out var state);
			var last = state?.LastCompletedAt;
			var due = (last ?? ScheduledTaskCatalog.ProcessStartUtc).AddMinutes(entry.Interval);
			if (now < due)
				continue;

			if (IsCommandRunning(commandState, entry.TaskName))
				continue;

			var payload = JsonSerializer.SerializeToElement(new Dictionary<string, string>
			{
				["name"] = entry.TaskName,
				["trigger"] = "scheduled"
			});

			_logger.LogInformation("Running scheduled task {TaskName}.", entry.TaskName);
			await dispatcher.DispatchAsync(payload, db, _scopeFactory, logger, realtime, metadata, youTubeDataApi);
		}
	}

	static bool IsCommandRunning(InMemoryCommandState state, string taskName)
	{
		lock (state.CommandsGate)
		{
			foreach (var cmd in state.Commands)
			{
				var nameStr = "";
				if (cmd.TryGetValue("commandName", out var cn) && cn is string c1)
					nameStr = c1;
				else if (cmd.TryGetValue("name", out var n) && n is string c2)
					nameStr = c2;

				if (!string.Equals(nameStr, taskName, StringComparison.OrdinalIgnoreCase))
					continue;

				if (!cmd.TryGetValue("status", out var st) || st is not string status)
					continue;

				if (string.Equals(status, "queued", StringComparison.OrdinalIgnoreCase) ||
				    string.Equals(status, "started", StringComparison.OrdinalIgnoreCase))
					return true;
			}
		}

		return false;
	}
}
