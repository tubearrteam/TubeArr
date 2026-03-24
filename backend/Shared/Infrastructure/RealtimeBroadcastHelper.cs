using System.Threading;
using System.Threading.Tasks;
using TubeArr.Backend.Realtime;

namespace TubeArr.Backend;

public static class RealtimeBroadcastHelper
{
	public static async Task BroadcastLiveQueueAndHistoryAsync(IRealtimeEventBroadcaster realtime, CancellationToken ct = default)
	{
		await realtime.BroadcastAsync("queue", new { action = "sync" }, ct);
		await realtime.BroadcastAsync("queueDetails", new { action = "sync" }, ct);
		await realtime.BroadcastAsync("queueStatus", new { action = "sync" }, ct);
		await realtime.BroadcastAsync("history", new { action = "sync" }, ct);
		await realtime.BroadcastAsync("channelHistory", new { action = "sync" }, ct);
		await realtime.BroadcastAsync("videoHistory", new { action = "sync" }, ct);
	}
}

