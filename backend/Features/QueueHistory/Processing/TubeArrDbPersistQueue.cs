using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

/// <summary>
/// Single-consumer queue for EF work: callers await until their operation runs and completes on a fresh <see cref="TubeArrDbContext"/>.
/// Heavy download/file work should stay outside; enqueue only persistence and short DB reads/writes here.
/// If the caller's <see cref="CancellationToken"/> fires while waiting in the queue, the item is dropped (no DB run). If work has
/// already started, the caller token is linked with the host shutdown token so EF operations can cancel.
/// </summary>
public sealed class TubeArrDbPersistQueue
{
	readonly object _lock = new();
	readonly Queue<(Func<TubeArrDbContext, CancellationToken, Task> Work, TaskCompletionSource Tcs, CancellationToken CallerCt)> _pending = new();
	readonly SemaphoreSlim _workAvailable = new(0, int.MaxValue);

	public async Task EnqueueAsync(Func<TubeArrDbContext, CancellationToken, Task> work, CancellationToken cancellationToken = default)
	{
		var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		lock (_lock)
			_pending.Enqueue((work, tcs, cancellationToken));
		_workAvailable.Release();
		try
		{
			await tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			var removed = false;
			lock (_lock)
			{
				var n = _pending.Count;
				for (var i = 0; i < n; i++)
				{
					var item = _pending.Dequeue();
					if (!ReferenceEquals(item.Tcs, tcs))
						_pending.Enqueue(item);
					else
						removed = true;
				}
			}

			if (removed)
			{
				await _workAvailable.WaitAsync(CancellationToken.None).ConfigureAwait(false);
				tcs.TrySetCanceled(cancellationToken);
			}

			throw;
		}
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
			CancellationToken callerCt;
			lock (_lock)
			{
				if (_pending.Count == 0)
					continue;
				(work, tcs, callerCt) = _pending.Dequeue();
			}

			try
			{
				await using var scope = scopeFactory.CreateAsyncScope();
				var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
				using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, callerCt);
				await work(db, linked.Token).ConfigureAwait(false);
				tcs.TrySetResult();
			}
			catch (Exception ex)
			{
				tcs.TrySetException(ex);
			}
		}
	}
}
