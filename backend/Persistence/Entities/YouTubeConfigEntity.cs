namespace TubeArr.Backend.Data;

public sealed class YouTubeConfigEntity
{
	public int Id { get; set; } = 1;

	public string ApiKey { get; set; } = "";
	public bool UseYouTubeApi { get; set; } = false;
	public string ApiPriorityMetadataItemsJson { get; set; } = "";
}
