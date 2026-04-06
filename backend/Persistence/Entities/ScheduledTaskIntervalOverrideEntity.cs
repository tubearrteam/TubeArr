namespace TubeArr.Backend.Data;

/// <summary>Optional per-task interval override (minutes). When unset, <see cref="ScheduledTaskCatalog"/> defaults apply.</summary>
public sealed class ScheduledTaskIntervalOverrideEntity
{
	public string TaskName { get; set; } = "";

	public int IntervalMinutes { get; set; }
}
