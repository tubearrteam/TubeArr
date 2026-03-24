namespace TubeArr.Backend.Data;

/// <summary>
/// Persisted download history entry. Completed queue items are moved here.
/// </summary>
public sealed class DownloadHistoryEntity
{
	public int Id { get; set; }
	public int ChannelId { get; set; }
	public int VideoId { get; set; }
	public int? PlaylistId { get; set; }
	/// <summary>
	/// Sonarr-compatible event code shape used by existing UI filters.
	/// 1 = grabbed, 3 = imported, 4 = failed, 5 = deleted, 6 = renamed, 7 = ignored.
	/// </summary>
	public int EventType { get; set; }
	public string SourceTitle { get; set; } = string.Empty;
	public string? OutputPath { get; set; }
	public string? Message { get; set; }
	public string? DownloadId { get; set; }
	/// <summary>UTC when the event was recorded.</summary>
	public DateTime Date { get; set; } = DateTime.UtcNow;
}
