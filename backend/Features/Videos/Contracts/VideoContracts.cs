namespace TubeArr.Backend.Contracts;

public record VideoDto(
	int Id,
	int ChannelId,
	string YoutubeVideoId,
	string Title,
	string? Description,
	string? ThumbnailUrl,
	DateTimeOffset UploadDateUtc,
	DateTimeOffset AirDateUtc,
	string AirDate,
	string Overview,
	int Runtime,
	bool Monitored,
	DateTimeOffset Added,
	int PlaylistNumber,
	int? VideoFileId = null,
	bool HasFile = false
);

public record UpdateVideoRequest(bool? Monitored);
public record MonitorVideosRequest(int[] VideoIds, bool Monitored);
