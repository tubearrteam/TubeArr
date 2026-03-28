using TubeArr.Backend;

namespace TubeArr.Backend.Data;

public sealed class CommandQueueJobEntity
{
	public long Id { get; set; }
	public int? CommandId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string JobType { get; set; } = string.Empty;
	public string PayloadJson { get; set; } = "{}";
	/// <summary><see cref="QueueJobStatuses"/> and same lifecycle names as <see cref="DownloadQueueEntity"/>.</summary>
	public string Status { get; set; } = QueueJobStatuses.Queued;
	public DateTimeOffset QueuedAtUtc { get; set; }
	public DateTimeOffset? StartedAtUtc { get; set; }
	public DateTimeOffset? EndedAtUtc { get; set; }
	public string? LastError { get; set; }

	/// <summary>JSON array of <see cref="AcquisitionMethodIds"/> values used while this command ran (internal HTML, yt-dlp, YouTube Data API).</summary>
	public string AcquisitionMethodsJson { get; set; } = AcquisitionMethodsJsonHelper.DefaultCommandJson;
}
