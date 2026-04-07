using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.Realtime;

namespace TubeArr.Backend;

/// <summary>
/// Runs <see cref="DownloadQueueProcessor.RunUntilEmptyAsync"/> when <see cref="DownloadQueueProcessTrigger"/> is signaled.
/// </summary>
public sealed class DownloadQueueProcessorHostedService : BackgroundService
{
	readonly IServiceScopeFactory _scopeFactory;
	readonly DownloadQueueProcessTrigger _trigger;
	readonly ILogger<DownloadQueueProcessorHostedService> _logger;

	public DownloadQueueProcessorHostedService(
		IServiceScopeFactory scopeFactory,
		DownloadQueueProcessTrigger trigger,
		ILogger<DownloadQueueProcessorHostedService> logger)
	{
		_scopeFactory = scopeFactory;
		_trigger = trigger;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await _trigger.WaitForRunRequestAsync(stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				break;
			}

			if (DownloadQueueProcessor.IsProcessing)
				continue;

			try
			{
				using var scope = _scopeFactory.CreateScope();
				var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
				var realtime = scope.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>();

				await DownloadQueueProcessor.RunUntilEmptyAsync(
					_scopeFactory,
					env.ContentRootPath,
					stoppingToken,
					_logger,
					async ct => await RealtimeBroadcastHelper.BroadcastLiveQueueAndHistoryAsync(realtime, ct).ConfigureAwait(false))
					.ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Download queue processor failed");
			}
		}
	}
}
