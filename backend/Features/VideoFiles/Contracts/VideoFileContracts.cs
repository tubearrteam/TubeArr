namespace TubeArr.Backend.Contracts;

public record VideoFileDto(
	int Id,
	int ChannelId,
	int PlaylistNumber,
	string Path,
	string RelativePath,
	long Size,
	DateTimeOffset DateAdded,
	string ReleaseGroup,
	object[] Languages,
	VideoFileQualityWrapper Quality,
	object[] CustomFormats,
	int CustomFormatScore,
	int IndexerFlags,
	string ReleaseType,
	object? MediaInfo,
	bool QualityCutoffNotMet
);

public record VideoFileQualityWrapper(VideoFileQualityDetails Quality, VideoFileRevision Revision);
public record VideoFileQualityDetails(int Id, string Name, string Source, int Resolution);
public record VideoFileRevision(int Version, int Real, bool IsRepack);

public record VideoFileBulkUpdateDto(int Id, bool QualityCutoffNotMet, object[] CustomFormats, int CustomFormatScore);
public record DeleteVideoFilesRequest(int[]? VideoFileIds);
public record VideoFileBulkSelectionRequest(int Id);
