namespace TubeArr.Backend.Contracts;

public record HistoryItemDto(
	int Id,
	int ChannelId,
	int VideoId,
	string EventType,
	string SourceTitle,
	object[] Languages,
	HistoryQualityDto Quality,
	bool QualityCutoffNotMet,
	object[] CustomFormats,
	int CustomFormatScore,
	DateTimeOffset Date,
	object Data,
	string DownloadId
);

public record HistoryQualityDto(HistoryQualityDetailsDto Quality, HistoryRevisionDto Revision);
public record HistoryQualityDetailsDto(int Id, string Name);
public record HistoryRevisionDto(int Version, int Real, bool IsRepack);

public record HistoryFailedData(string Message);
public record HistoryImportedData(string DownloadClient, string DownloadClientName, string DroppedPath, string ImportedPath);

public record QueuePageDto(IReadOnlyList<QueueItemDto> Records, int TotalRecords, int PageSize);

public record QueueItemDto(
	int Id,
	int VideoId,
	int ChannelId,
	string Status,
	string StatusLabel,
	string? ErrorMessage,
	string? OutputPath,
	DateTimeOffset QueuedAt,
	DateTimeOffset? StartedAt,
	DateTimeOffset? CompletedAt,
	double? Progress,
	int? EstimatedSecondsRemaining,
	int? EstimatedCompletionTime,
	QueueQualityRef? Quality,
	QueueChannelRef Channel,
	QueueVideoRef Video,
	QueueVideosRef Videos
);

public record QueueQualityRef(int Id, string Name);
public record QueueChannelRef(int Id, string Title, string SortTitle);
public record QueueVideoRef(int Id, string Title, string YoutubeVideoId);
public record QueueVideosRef(string Title, DateTimeOffset AirDateUtc);

public record QueueDetailItemDto(int ChannelId, int VideoId, string TrackedDownloadState, bool VideoHasFile, int PlaylistNumber);
