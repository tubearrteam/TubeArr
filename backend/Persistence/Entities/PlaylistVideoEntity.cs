namespace TubeArr.Backend.Data;

/// <summary>Many-to-many: which curated playlists a video belongs to (uploads library is not a row here).</summary>
public sealed class PlaylistVideoEntity
{
	public int PlaylistId { get; set; }
	public int VideoId { get; set; }
	public string? PlaylistItemId { get; set; }
	public int? Position { get; set; }
	public DateTimeOffset? AddedAt { get; set; }
}
