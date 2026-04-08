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
	/// <summary>yt-dlp selected format id(s), e.g. <c>137+140</c>, parsed from <c>[info] … Downloading N format(s): …</c>.</summary>
	public string? FormatSummary { get; set; }
	public string? OutputPath { get; set; }
	public string? LastError { get; set; }

	public DateTimeOffset QueuedAtUtc { get; set; } = DateTimeOffset.UtcNow;
	public DateTimeOffset? StartedAtUtc { get; set; }
	public DateTimeOffset? EndedAtUtc { get; set; }

	/// <summary>JSON array; downloads use <see cref="AcquisitionMethodIds.YtDlp"/> (same shape as <see cref="CommandQueueJobEntity.AcquisitionMethodsJson"/>).</summary>
	public string AcquisitionMethodsJson { get; set; } = AcquisitionMethodsJsonHelper.DefaultDownloadJson;

	/// <summary>slskd / cross-provider acquisition state (JSON blob managed by download processor).</summary>
	public string? ExternalAcquisitionJson { get; set; }

	/// <summary>When 1, a running queue item still needs the download worker (poll slskd, continue pipeline).</summary>
	public int ExternalWorkPending { get; set; }
}
