using System.Collections.Generic;
using TubeArr.Backend;

namespace TubeArr.Backend.Contracts;

public record PropertyFilterDto(string Key, object? Value, string? Type);
public record CustomFilterDto(int Id, string Type, string Label, List<PropertyFilterDto> Filters);
public record CustomFilterSaveRequest(int? Id, string Type, string Label, List<PropertyFilterDto> Filters);

public record PlaylistDto(
	int PlaylistNumber,
	string Title,
	bool Monitored,
	bool IsCustom = false,
	int? CustomPlaylistId = null,
	int? PlaylistId = null,
	int Priority = 0
);

public record ChannelCustomPlaylistRuleDto(
	string Field,
	string Operator,
	System.Text.Json.JsonElement? Value);

/// <summary>Per-channel custom playlist: core + matching (rules, max 5).</summary>
public record ChannelCustomPlaylistDto(
	int Id,
	int ChannelId,
	string Name,
	bool Enabled,
	int Priority,
	int MatchType,
	ChannelCustomPlaylistRuleDto[] Rules,
	int PlaylistNumber);

public record ChannelCustomPlaylistSaveDto(
	int? Id,
	string Name,
	bool Enabled,
	int Priority,
	int MatchType,
	ChannelCustomPlaylistRuleDto[] Rules);

public record ChannelPlaylistClientDto(
	int PlaylistNumber,
	bool Monitored,
	bool IsCustom = false,
	int? CustomPlaylistId = null,
	int? PlaylistId = null,
	int? Priority = null);

public record ChannelStatisticsDto(
	int VideoCount,
	int VideoFileCount,
	int PlaylistCount,
	long SizeOnDisk,
	int TotalVideoCount,
	DateTimeOffset? LastUploadUtc = null,
	DateTimeOffset? FirstUploadUtc = null // min air date (UTC) for Active Since; videos with blank air date ignored
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
	int PlaylistMultiMatchStrategy = 0,
	string PlaylistMultiMatchStrategyOrder = "0123",
	string? ChannelType = null,
	int? RoundRobinLatestVideoCount = null,
	bool FilterOutShorts = false,
	bool FilterOutLivestreams = false,
	bool? HasShortsTab = null,
	bool? HasStreamsTab = null,
	string? MonitorPreset = null,
	ChannelStatisticsDto? Statistics = null,
	ChannelCustomPlaylistDto[]? CustomPlaylists = null
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

/// <summary>One immediate child folder under a root that is not already a configured channel show folder, plus optional library-import resolve preview.</summary>
public record LibraryImportFolderDto(
	string Name,
	string Path,
	string RelativePath,
	string? ResolveInputTried,
	string? ResolutionMethod,
	bool ResolveSuccess,
	ChannelSearchResultDto? SuggestedChannel,
	string? ResolveFailureReason);

public record RootFolderDetailDto(
	int Id,
	string Path,
	bool Accessible,
	long? FreeSpace,
	LibraryImportFolderDto[] UnmappedFolders);

/// <summary>SSE payloads for library-import scan stream (camelCase JSON).</summary>
public record LibraryImportScanProgressDto(
	string Phase,
	string? FolderName = null,
	int? Index = null,
	int? Total = null,
	bool? ResolveSuccess = null,
	string? ChannelTitle = null,
	string? Message = null,
	RootFolderDetailDto? Result = null);

public record CreateChannelRequest(
	string YoutubeChannelId,
	string? Title,
	string? Description,
	bool Monitored = true,
	int? QualityProfileId = null,
	string? RootFolderPath = null,
	string? ChannelType = null,
	bool? PlaylistFolder = null,
	int? PlaylistMultiMatchStrategy = null,
	string? PlaylistMultiMatchStrategyOrder = null,
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
	int? PlaylistMultiMatchStrategy = null,
	string? PlaylistMultiMatchStrategyOrder = null,
	string? ChannelType = null,
	OptionalValue<int?> RoundRobinLatestVideoCount = default,
	bool? FilterOutShorts = null,
	bool? FilterOutLivestreams = null,
	OptionalValue<string?> MonitorPreset = default,
	ChannelPlaylistClientDto[]? Playlists = null,
	ChannelCustomPlaylistSaveDto[]? CustomPlaylists = null
);

public record BulkChannelMonitoringRequest(int[] ChannelIds, string Monitor, int? RoundRobinLatestVideoCount = null);
