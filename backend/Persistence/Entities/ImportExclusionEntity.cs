namespace TubeArr.Backend.Data;

public sealed class ImportExclusionEntity
{
	public int Id { get; set; }
	public string? YoutubeChannelId { get; set; }
	public string Title { get; set; } = string.Empty;
}