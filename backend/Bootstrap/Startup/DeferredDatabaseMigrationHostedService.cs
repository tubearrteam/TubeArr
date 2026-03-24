using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace TubeArr.Backend;

internal sealed class DeferredDatabaseMigrationHostedService : BackgroundService
{
	private static readonly SemaphoreSlim RunLock = new(1, 1);

	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IHostApplicationLifetime _applicationLifetime;
	private readonly ILogger<DeferredDatabaseMigrationHostedService> _logger;

	public DeferredDatabaseMigrationHostedService(
		IServiceScopeFactory scopeFactory,
		IHostApplicationLifetime applicationLifetime,
		ILogger<DeferredDatabaseMigrationHostedService> logger)
	{
		_scopeFactory = scopeFactory;
		_applicationLifetime = applicationLifetime;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!await RunLock.WaitAsync(0, stoppingToken))
		{
			_logger.LogWarning("Deferred database maintenance service already running; skipping duplicate run.");
			return;
		}

		try
		{
			if (!_applicationLifetime.ApplicationStarted.IsCancellationRequested)
			{
				var startedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
				using var startedRegistration = _applicationLifetime.ApplicationStarted.Register(() => startedTcs.TrySetResult());
				using var cancelledRegistration = stoppingToken.Register(() => startedTcs.TrySetCanceled(stoppingToken));
				await startedTcs.Task;
			}

			var sw = Stopwatch.StartNew();
			_logger.LogInformation("Deferred database maintenance service started.");

			using var scope = _scopeFactory.CreateScope();
			DatabaseBootstrap.RunDeferredMaintenance(scope.ServiceProvider, stoppingToken);

			_logger.LogInformation("Deferred database maintenance service completed in {ElapsedMs} ms.", sw.ElapsedMilliseconds);
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
			_logger.LogInformation("Deferred database maintenance service canceled.");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Deferred database maintenance service failed. The API will continue serving requests.");
		}
		finally
		{
			RunLock.Release();
		}
	}
}