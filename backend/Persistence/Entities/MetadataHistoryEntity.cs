namespace TubeArr.Backend.Data;

/// <summary>
/// Completed, failed, or aborted metadata jobs (append-only). Rows are inserted when work leaves <see cref="MetadataQueueEntity"/>.
/// </summary>
public sealed class MetadataHistoryEntity
{
	public long Id { get; set; }
	public long? CommandQueueJobId { get; set; }
	public int? CommandId { get; set; }
	public int? ChannelId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string JobType { get; set; } = string.Empty;
	public string PayloadJson { get; set; } = "{}";
	/// <summary><see cref="QueueJobStatuses.Completed"/>, <see cref="QueueJobStatuses.Failed"/>, or <c>aborted</c> (cancelled while queued/running).</summary>
	public string ResultStatus { get; set; } = QueueJobStatuses.Completed;
	public string? Message { get; set; }
	public DateTimeOffset QueuedAtUtc { get; set; }
	public DateTimeOffset? StartedAtUtc { get; set; }
	public DateTimeOffset? EndedAtUtc { get; set; }
	public string AcquisitionMethodsJson { get; set; } = AcquisitionMethodsJsonHelper.DefaultCommandJson;
}
