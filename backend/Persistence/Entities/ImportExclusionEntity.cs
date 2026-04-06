namespace TubeArr.Backend.Data;

public sealed class ImportExclusionEntity
{
	public int Id { get; set; }
	/// <summary>Discriminator for future non-channel exclusions; only <c>channel</c> is supported today.</summary>
	public string TargetType { get; set; } = "channel";
	public string YoutubeChannelId { get; set; } = string.Empty;
	/// <summary>Display snapshot when the exclusion was created (titles drift).</summary>
	public string Title { get; set; } = string.Empty;
	public string? Reason { get; set; }
	public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}