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
	public DateTimeOffset Added { get; set; } = DateTimeOffset.UtcNow;
}