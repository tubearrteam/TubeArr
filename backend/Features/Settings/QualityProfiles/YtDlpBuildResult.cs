namespace TubeArr.Backend.QualityProfile;

/// <summary>
/// Result of building yt-dlp format selection args from a quality profile.
/// </summary>
public sealed class YtDlpBuildResult
{
	public int ProfileId { get; set; }
	public string ProfileName { get; set; } = string.Empty;
	public string Selector { get; set; } = string.Empty;
	public string Sort { get; set; } = string.Empty;
	public string FallbackPlanSummary { get; set; } = string.Empty;
	public IReadOnlyList<string> YtDlpArgs { get; set; } = Array.Empty<string>();
	public IReadOnlyList<string> DebugMetadata { get; set; } = Array.Empty<string>();
}
