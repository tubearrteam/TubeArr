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
	public DateTimeOffset Added { get; set; } = DateTimeOffset.UtcNow;
}