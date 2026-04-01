namespace TubeArr.Backend.Data;

public sealed class VideoEntity
{
	public int Id { get; set; }
	public int ChannelId { get; set; }
	public string YoutubeVideoId { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string? ThumbnailUrl { get; set; }
	public DateTimeOffset UploadDateUtc { get; set; }
	public DateTimeOffset AirDateUtc { get; set; } = DateTimeOffset.UnixEpoch;
	public string AirDate { get; set; } = string.Empty;
	public string? Overview { get; set; }
	public int Runtime { get; set; }
	public int? Width { get; set; }
	public int? Height { get; set; }
	/// <summary>Set from watch-page metadata and/or channel Shorts tab listing during acquisition.</summary>
	public bool IsShort { get; set; }
	/// <summary>Set from watch-page metadata / fallback metadata when content is or was a livestream.</summary>
	public bool IsLivestream { get; set; }
	public bool Monitored { get; set; } = true;

	/// <summary>Raw <c>videos.list</c> resource fragments (snippet, contentDetails, statistics, status, liveStreamingDetails) from the YouTube Data API when last fetched.</summary>
	public string? YouTubeDataApiVideoResourceJson { get; set; }

	/// <summary>
	/// Stable Plex season/episode numbering for this video in the TV library. Once assigned, does not change automatically.
	/// Season 01 is reserved for "channel-only" videos (no curated playlist).
	/// </summary>
	public int? PlexPrimaryPlaylistId { get; set; }
	public int? PlexSeasonIndex { get; set; }
	public int? PlexEpisodeIndex { get; set; }
	public bool PlexIndexLocked { get; set; }

	public DateTimeOffset Added { get; set; } = DateTimeOffset.UtcNow;
}