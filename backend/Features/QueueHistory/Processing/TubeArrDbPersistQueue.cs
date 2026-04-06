using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

/// <summary>
/// Single-consumer queue for EF work: callers await until their operation runs and completes on a fresh <see cref="TubeArrDbContext"/>.
/// Heavy download/file work should stay outside; enqueue only persistence and short DB reads/writes here.
/// </summary>
public sealed class TubeArrDbPersistQueue
{
	readonly object _lock = new();
	readonly Queue<(Func<TubeArrDbContext, CancellationToken, Task> Work, TaskCompletionSource Tcs)> _pending = new();
	readonly SemaphoreSlim _workAvailable = new(0, int.MaxValue);

	public async Task EnqueueAsync(Func<TubeArrDbContext, CancellationToken, Task> work, CancellationToken cancellationToken = default)
	{
		var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		lock (_lock)
			_pending.Enqueue((work, tcs));
		_workAvailable.Release();
		await tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
	}

	internal async Task DrainLoopAsync(IServiceScopeFactory scopeFactory, CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await _workAvailable.WaitAsync(stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				break;
			}

			Func<TubeArrDbContext, CancellationToken, Task> work;
			TaskCompletionSource tcs;
			lock (_lock)
			{
				if (_pending.Count == 0)
					continue;
				(work, tcs) = _pending.Dequeue();
			}

			try
			{
				await using var scope = scopeFactory.CreateAsyncScope();
				var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
				await work(db, stoppingToken).ConfigureAwait(false);
				tcs.TrySetResult();
			}
			catch (Exception ex)
			{
				tcs.TrySetException(ex);
			}
		}
	}
}
