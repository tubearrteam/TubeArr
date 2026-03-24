namespace TubeArr.Backend.Data;

public sealed class CommandQueueJobEntity
{
	public long Id { get; set; }
	public int? CommandId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string JobType { get; set; } = string.Empty;
	public string PayloadJson { get; set; } = "{}";
	public string Status { get; set; } = "queued";
	public DateTimeOffset QueuedAtUtc { get; set; }
	public DateTimeOffset? StartedAtUtc { get; set; }
	public DateTimeOffset? EndedAtUtc { get; set; }
	public string? LastError { get; set; }
}
