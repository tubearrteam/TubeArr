namespace TubeArr.Backend.Data;

public sealed class PlaylistEntity
{
	public int Id { get; set; }
	public int ChannelId { get; set; }
	public string YoutubePlaylistId { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string? ThumbnailUrl { get; set; }
	public bool Monitored { get; set; } = true;
	/// <summary>Lower numbers sort before higher (same idea as <see cref="ChannelCustomPlaylistEntity.Priority"/>); ties use activity/strategy ordering.</summary>
	public int Priority { get; set; }

	/// <summary>
	/// Stable Plex/Kodi season index for this playlist within the channel. Assigned once and never renumbered automatically.
	/// Season 01 is reserved for "channel-only" videos (no curated playlist); playlist seasons start at 02.
	/// </summary>
	public int? SeasonIndex { get; set; }
	public bool SeasonIndexLocked { get; set; }

	public DateTimeOffset Added { get; set; } = DateTimeOffset.UtcNow;
}