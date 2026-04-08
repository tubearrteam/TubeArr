using System.Threading;

namespace TubeArr.Backend;

/// <summary>
/// Coalesces POST /queue/process requests; <see cref="DownloadQueueProcessorHostedService"/> consumes signals.
/// </summary>
public sealed class DownloadQueueProcessTrigger
{
	readonly SemaphoreSlim _runRequested = new(0, int.MaxValue);

	public void SignalRunRequested() => _runRequested.Release();

	public Task WaitForRunRequestAsync(CancellationToken cancellationToken) =>
		_runRequested.WaitAsync(cancellationToken);
}
