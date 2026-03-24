namespace TubeArr.Backend.Data;

/// <summary>
/// A video queued for download by yt-dlp. Processed in order; uses channel's quality profile and root folder.
/// </summary>
public sealed class DownloadQueueEntity
{
	public int Id { get; set; }
	public int VideoId { get; set; }
	public int ChannelId { get; set; }
	/// <summary>0 = Queued, 1 = Downloading, 2 = Completed, 3 = Failed</summary>
	public int Status { get; set; }
	/// <summary>Download progress 0.0â€“1.0 while downloading; set on completion.</summary>
	public double? Progress { get; set; }
	/// <summary>Latest ETA reported by yt-dlp in whole seconds.</summary>
	public int? EstimatedSecondsRemaining { get; set; }
	/// <summary>Resolved output file path when download completes.</summary>
	public string? OutputPath { get; set; }
	public string? ErrorMessage { get; set; }
	public DateTimeOffset QueuedAt { get; set; } = DateTimeOffset.UtcNow;
	public DateTimeOffset? StartedAt { get; set; }
	public DateTimeOffset? CompletedAt { get; set; }
}
