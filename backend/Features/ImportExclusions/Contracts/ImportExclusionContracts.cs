namespace TubeArr.Backend.Contracts;

public record ImportExclusionDto(
	int Id,
	string TargetType,
	string YoutubeChannelId,
	string Title,
	string? Reason,
	DateTimeOffset CreatedAtUtc);

public record SaveImportExclusionRequest(string YoutubeChannelId, string? Title = null, string? Reason = null, string? TargetType = null);

public record DeleteImportExclusionsRequest(int[]? Ids);
