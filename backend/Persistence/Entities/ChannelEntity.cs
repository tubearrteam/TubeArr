namespace TubeArr.Backend.Data;

public sealed class ChannelEntity
{
	public int Id { get; set; }
	public string YoutubeChannelId { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string? ThumbnailUrl { get; set; }
	public string? BannerUrl { get; set; }
	public string TitleSlug { get; set; } = string.Empty;
	public bool Monitored { get; set; } = true;
	public DateTimeOffset Added { get; set; } = DateTimeOffset.UtcNow;
	/// <summary>Optional. When null, use default quality profile from settings if available.</summary>
	public int? QualityProfileId { get; set; }
	/// <summary>Channel folder path (root folder + channel folder).</summary>
	public string? Path { get; set; }
	/// <summary>Root folder path selected for this channel (UI convenience).</summary>
	public string? RootFolderPath { get; set; }
	/// <summary>Monitor new playlists setting (0=all, 1=none, etc.).</summary>
	public int? MonitorNewItems { get; set; }
	/// <summary>UI preset when using specific video/playlist monitoring (e.g. specificVideos, specificPlaylists).</summary>
	public string? MonitorPreset { get; set; }
	/// <summary>Sort videos into playlist folders.</summary>
	public bool? PlaylistFolder { get; set; }

	/// <summary>When a video is in multiple curated playlists, which playlist drives folder path and primary id (<see cref="PlaylistMultiMatchStrategy"/>).</summary>
	public int PlaylistMultiMatchStrategy { get; set; }

	/// <summary>Permutation of 0–3: tie-break order for multi-playlist resolution (<see cref="PlaylistMultiMatchStrategy"/>). First character is the legacy primary strategy.</summary>
	public string PlaylistMultiMatchStrategyOrder { get; set; } = "0123";
	/// <summary>Channel type for renaming/parsing.</summary>
	public string? ChannelType { get; set; }
	/// <summary>Round-robin monitoring: when channel is monitored and this is set to N &gt; 0, only the N newest videos remain monitored.</summary>
	public int? RoundRobinLatestVideoCount { get; set; }
	/// <summary>When true, Shorts (channel Shorts tab / player metadata) are not monitored.</summary>
	public bool FilterOutShorts { get; set; }
	/// <summary>When true, livestreams (active/upcoming/archived live) are not monitored.</summary>
	public bool FilterOutLivestreams { get; set; }
	/// <summary>
	/// Heuristic from channel page embedded data (e.g. Shorts tab). Null when unknown; true when a Shorts tab signal was found.
	/// </summary>
	public bool? HasShortsTab { get; set; }

	/// <summary>
	/// Heuristic from channel page embedded data (e.g. Streams / Live tab). Null when unknown; true when a Streams tab signal was found.
	/// </summary>
	public bool? HasStreamsTab { get; set; }
}
