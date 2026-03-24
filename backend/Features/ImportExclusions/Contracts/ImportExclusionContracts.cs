namespace TubeArr.Backend.Contracts;

public record ImportExclusionDto(int Id, string? YoutubeChannelId, string Title);
public record SaveImportExclusionRequest(string Title, string? YoutubeChannelId);
public record DeleteImportExclusionsRequest(int[]? Ids);
