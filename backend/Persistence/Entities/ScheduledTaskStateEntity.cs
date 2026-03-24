namespace TubeArr.Backend.Data;

public sealed class ScheduledTaskStateEntity
{
	public string TaskName { get; set; } = "";

	public DateTimeOffset? LastCompletedAt { get; set; }

	public long? LastDurationTicks { get; set; }
}
