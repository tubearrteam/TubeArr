namespace TubeArr.Backend;

public static class CommandQueueJobTypes
{
	public const string RefreshChannel = "RefreshChannel";
	public const string GetVideoDetails = "GetVideoDetails";
	public const string GetChannelPlaylists = "GetChannelPlaylists";
	public const string RssSync = "RssSync";
	public const string DownloadMonitoredQueuePump = "DownloadMonitoredQueuePump";
	public const string RefreshMonitoredDownloads = "RefreshMonitoredDownloads";
	public const string RenameFiles = "RenameFiles";
	public const string RenameChannel = "RenameChannel";
	public const string MapUnmappedVideoFiles = "MapUnmappedVideoFiles";
	public const string SyncCustomNfos = "SyncCustomNfos";
	public const string RepairLibraryNfosAndArtwork = "RepairLibraryNfosAndArtwork";
}

public sealed record RefreshChannelQueueJobPayload(
	string Name,
	string Trigger,
	int[] ChannelIds,
	string? Phase = null,
	int[]? AllChannelIdsInBatch = null,
	int? ChannelIndexInBatch = null,
	bool RecordScheduledTaskForBatch = false,
	DateTimeOffset? BatchStartedAtUtc = null,
	string? SerializedPlaylistDiscoveryItems = null,
	bool StopAfterThisPhase = false);

public sealed record GetVideoDetailsQueueJobPayload(
	string Name,
	string Trigger,
	int ChannelId);

public sealed record GetChannelPlaylistsQueueJobPayload(
	string Name,
	string Trigger,
	int ChannelId);

public sealed record RssSyncQueueJobPayload(
	string Name,
	string Trigger,
	int? ChannelId);

public sealed record DownloadMonitoredQueuePumpPayload(string Name);

public sealed record RefreshMonitoredDownloadsQueueJobPayload(string Name, string Trigger);

public sealed record RenameFilesQueueJobPayload(string Name, string Trigger, int ChannelId, int[] FileIds, bool Force = false);

public sealed record RenameChannelQueueJobPayload(string Name, string Trigger, int[] ChannelIds);

public sealed record MapUnmappedVideoFilesQueueJobPayload(string Name, string Trigger);

public sealed record SyncCustomNfosQueueJobPayload(string Name, string Trigger);

public sealed record RepairLibraryNfosQueueJobPayload(string Name, string Trigger);
