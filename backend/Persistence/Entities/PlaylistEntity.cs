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
	/// Plex/Kodi season index for this playlist within the channel (02+). Kept in sync with curated playlist ordering:
	/// <see cref="Priority"/> then latest activity / title (same as the channel UI). Season 01 is channel-only uploads.
	/// </summary>
	public int? SeasonIndex { get; set; }
	public bool SeasonIndexLocked { get; set; }

	public DateTimeOffset Added { get; set; } = DateTimeOffset.UtcNow;
}