using System.Collections.Concurrent;
using System.Threading;

namespace TubeArr.Backend;

/// <summary>Process-wide coordination for the download queue worker (single active processor assumption).</summary>
internal static class DownloadQueueWorkerSync
{
	internal static readonly ManualResetEventSlim DownloadUnpaused = new(true);

	internal static readonly SemaphoreSlim CookieRefreshMutex = new(1, 1);

	internal static readonly ConcurrentDictionary<int, CancellationTokenSource> ActiveDownloadCancellations = new();

	internal static bool TryCancelActiveDownload(int queueId)
	{
		if (!ActiveDownloadCancellations.TryGetValue(queueId, out var cts))
			return false;
		cts.Cancel();
		return true;
	}
}
