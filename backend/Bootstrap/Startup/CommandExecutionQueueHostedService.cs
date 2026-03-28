using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TubeArr.Backend;

internal sealed class CommandExecutionQueueHostedService : BackgroundService
{
	const int WorkerCount = 3;

	readonly ICommandExecutionQueue _commandQueue;
	readonly ICommandRecoveryJobRunner _recoveryRunner;
	readonly ILogger<CommandExecutionQueueHostedService> _logger;

	public CommandExecutionQueueHostedService(
		ICommandExecutionQueue commandQueue,
		ICommandRecoveryJobRunner recoveryRunner,
		ILogger<CommandExecutionQueueHostedService> logger)
	{
		_commandQueue = commandQueue;
		_recoveryRunner = recoveryRunner;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("Command execution queue worker started.");

		try
		{
			await _commandQueue.RecoverRunningJobsAsync(stoppingToken);

			var workers = new Task[WorkerCount];
			for (var w = 0; w < WorkerCount; w++)
				workers[w] = RunCommandWorkerAsync(stoppingToken);
			await Task.WhenAll(workers);
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
			_logger.LogInformation("Command execution queue worker canceled.");
		}
		finally
		{
			_logger.LogInformation("Command execution queue worker stopped.");
		}
	}

	async Task RunCommandWorkerAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			var workItem = await _commandQueue.TryDequeueAsync(stoppingToken);
			if (workItem is null)
			{
				await Task.Delay(500, stoppingToken);
				continue;
			}

			try
			{
				if (workItem.ExecuteAsync is not null)
					await workItem.ExecuteAsync(stoppingToken);
				else
					await _recoveryRunner.ExecuteAsync(workItem, stoppingToken);

				await _commandQueue.MarkCompletedAsync(workItem.QueueItemId, CancellationToken.None);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				if (workItem.QueueItemId > 0)
					await _commandQueue.RequeueAsync(workItem.QueueItemId, "Host shutdown while command was running.", CancellationToken.None);

				_logger.LogInformation("Command execution queue worker stopping due to host shutdown.");
				break;
			}
			catch (Exception ex)
			{
				if (workItem.QueueItemId > 0)
					await _commandQueue.MarkFailedAsync(workItem.QueueItemId, ex.Message ?? "Unknown queue execution failure.", CancellationToken.None);

				_logger.LogError(ex, "Queued command job {JobName} failed.", workItem.Name);
			}
		}
	}
}
