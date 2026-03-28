namespace TubeArr.Backend.Data;

/// <summary>
/// Active metadata jobs (refresh channel phases, get video details, RSS sync). Paired 1:1 with <see cref="CommandQueueJobEntity"/> for worker execution.
/// </summary>
public sealed class MetadataQueueEntity
{
	public long Id { get; set; }
	/// <summary>FK to the row in <c>CommandQueueJobs</c> that workers dequeue.</summary>
	public long CommandQueueJobId { get; set; }
	public int? CommandId { get; set; }
	public int? ChannelId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string JobType { get; set; } = string.Empty;
	public string PayloadJson { get; set; } = "{}";
	public string Status { get; set; } = QueueJobStatuses.Queued;
	public string? LastError { get; set; }
	public DateTimeOffset QueuedAtUtc { get; set; }
	public DateTimeOffset? StartedAtUtc { get; set; }
	public DateTimeOffset? EndedAtUtc { get; set; }
	public string AcquisitionMethodsJson { get; set; } = AcquisitionMethodsJsonHelper.DefaultCommandJson;
}
