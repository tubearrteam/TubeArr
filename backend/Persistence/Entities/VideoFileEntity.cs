namespace TubeArr.Backend.Data;

public sealed class VideoFileEntity
{
	public int Id { get; set; }
	public int VideoId { get; set; }
	public int ChannelId { get; set; }
	public int? PlaylistId { get; set; }
	public string Path { get; set; } = string.Empty;
	public string RelativePath { get; set; } = string.Empty;
	public long Size { get; set; }
	public DateTimeOffset DateAdded { get; set; } = DateTimeOffset.UtcNow;
}
