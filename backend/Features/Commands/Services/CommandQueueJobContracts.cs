namespace TubeArr.Backend;

public static class CommandQueueJobTypes
{
	public const string RefreshChannel = "RefreshChannel";
	public const string GetVideoDetails = "GetVideoDetails";
	public const string RssSync = "RssSync";
	public const string DownloadMonitoredQueuePump = "DownloadMonitoredQueuePump";
}

public sealed record RefreshChannelQueueJobPayload(
	string Name,
	string Trigger,
	int[] ChannelIds);

public sealed record GetVideoDetailsQueueJobPayload(
	string Name,
	string Trigger,
	int ChannelId);

public sealed record RssSyncQueueJobPayload(
	string Name,
	string Trigger,
	int? ChannelId);

public sealed record DownloadMonitoredQueuePumpPayload(string Name);
