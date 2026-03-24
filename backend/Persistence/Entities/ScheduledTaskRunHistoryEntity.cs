namespace TubeArr.Backend.Data;

/// <summary>One completed run of a catalog scheduled task, for system / events log.</summary>
public sealed class ScheduledTaskRunHistoryEntity
{
	public int Id { get; set; }
	public string TaskName { get; set; } = "";
	public DateTimeOffset CompletedAt { get; set; }
	public long DurationTicks { get; set; }

	/// <summary>Optional outcome summary for the system log (e.g. RSS counts, health check summary).</summary>
	public string? ResultMessage { get; set; }
}
