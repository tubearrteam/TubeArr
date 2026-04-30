using TubeArr.Backend;

namespace TubeArr.Backend.Data;

/// <summary>
/// A video queued for download by yt-dlp. Column names and <see cref="Status"/> values align with <see cref="CommandQueueJobEntity"/>.
/// </summary>
public sealed class DownloadQueueEntity
{
	public int Id { get; set; }
	public int VideoId { get; set; }
	public int ChannelId { get; set; }

	/// <summary><see cref="QueueJobStatuses"/> values: queued, running, completed, failed.</summary>
	public string Status { get; set; } = QueueJobStatuses.Queued;

	public double? Progress { get; set; }
	public int? EstimatedSecondsRemaining { get; set; }
	/// <summary>Current downloaded bytes as parsed from yt-dlp output (best-effort).</summary>
	public long? DownloadedBytes { get; set; }
	/// <summary>Total bytes expected as parsed from yt-dlp output (best-effort; may be null for unknown/live).</summary>
	public long? TotalBytes { get; set; }
	/// <summary>Current transfer speed in bytes/sec as parsed from yt-dlp output (best-effort).</summary>
	public long? SpeedBytesPerSecond { get; set; }
	/// <summary>yt-dlp selected format id(s), e.g. <c>137+140</c>, parsed from <c>[info] … Downloading N format(s): …</c>.</summary>
	public string? FormatSummary { get; set; }
	public string? OutputPath { get; set; }
	public string? LastError { get; set; }

	public DateTimeOffset QueuedAtUtc { get; set; } = DateTimeOffset.UtcNow;
	public DateTimeOffset? StartedAtUtc { get; set; }
	public DateTimeOffset? EndedAtUtc { get; set; }

	/// <summary>JSON array; downloads use <see cref="AcquisitionMethodIds.YtDlp"/> (same shape as <see cref="CommandQueueJobEntity.AcquisitionMethodsJson"/>).</summary>
	public string AcquisitionMethodsJson { get; set; } = AcquisitionMethodsJsonHelper.DefaultDownloadJson;
}
