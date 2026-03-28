using System.Collections.Generic;
using TubeArr.Backend;

namespace TubeArr.Backend.Contracts;

public record PropertyFilterDto(string Key, object? Value, string? Type);
public record CustomFilterDto(int Id, string Type, string Label, List<PropertyFilterDto> Filters);
public record CustomFilterSaveRequest(int? Id, string Type, string Label, List<PropertyFilterDto> Filters);

public record PlaylistDto(
	int PlaylistNumber,
	string Title,
	bool Monitored
);

public record ChannelStatisticsDto(
	int VideoCount,
	int VideoFileCount,
	int PlaylistCount,
	long SizeOnDisk,
	int TotalVideoCount
);

public record ChannelDto(
	int Id,
	string YoutubeChannelId,
	string Title,
	string TitleSlug,
	string? Description,
	string? ThumbnailUrl,
	string? BannerUrl,
	bool Monitored,
	DateTimeOffset Added,
	int QualityProfileId,
	PlaylistDto[] Playlists,
	string? Path = null,
	string? RootFolderPath = null,
	string? Tags = null,
	int? MonitorNewItems = null,
	bool? PlaylistFolder = null,
	string? ChannelType = null,
	int? RoundRobinLatestVideoCount = null,
	bool FilterOutShorts = false,
	bool FilterOutLivestreams = false,
	bool? HasShortsTab = null,
	string? MonitorPreset = null,
	ChannelStatisticsDto? Statistics = null
);

public record ChannelSearchResultDto(
	string YoutubeChannelId,
	string Title,
	string TitleSlug,
	string? Description,
	string? ThumbnailUrl,
	string? ChannelUrl = null,
	string? Handle = null,
	long? SubscriberCount = null,
	long? VideoCount = null
);

public record ChannelResolveResultDto(
	bool Success,
	string? ChannelId,
	string? CanonicalUrl,
	string? Title,
	string? ResolutionMethod,
	string? FailureReason,
	ChannelSearchResultDto[]? Items
);

public record CreateChannelRequest(
	string YoutubeChannelId,
	string? Title,
	string? Description,
	bool Monitored = true,
	int? QualityProfileId = null,
	string? RootFolderPath = null,
	string? ChannelType = null,
	bool? PlaylistFolder = null,
	string? Path = null,
	int[]? Tags = null,
	int? MonitorNewItems = null,
	int? RoundRobinLatestVideoCount = null,
	bool FilterOutShorts = false,
	bool FilterOutLivestreams = false,
	string? MonitorPreset = null
);

public record UpdateChannelRequest(
	string? Title,
	string? Description,
	string? ThumbnailUrl,
	bool? Monitored,
	OptionalValue<int?> QualityProfileId = default,
	string? Path = null,
	string? RootFolderPath = null,
	[property: System.Text.Json.Serialization.JsonConverter(typeof(TagsJsonConverter))] string? Tags = null,
	int? MonitorNewItems = null,
	bool? PlaylistFolder = null,
	string? ChannelType = null,
	OptionalValue<int?> RoundRobinLatestVideoCount = default,
	bool? FilterOutShorts = null,
	bool? FilterOutLivestreams = null,
	OptionalValue<string?> MonitorPreset = default
);

public record BulkChannelMonitoringRequest(int[] ChannelIds, string Monitor, int? RoundRobinLatestVideoCount = null);
