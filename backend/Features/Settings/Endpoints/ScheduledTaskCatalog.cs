using TubeArr.Backend.Contracts;

namespace TubeArr.Backend;

internal static class ScheduledTaskCatalog
{
	internal static List<ScheduledTaskDto> GetScheduledTaskDtos()
	{
		// No scheduler yet: return catalog with stub timestamps. taskName is the command name for Execute Now (/command).
		var never = "2000-01-01T00:00:00Z";
		var now = DateTimeOffset.UtcNow;
		var tasks = new List<(int Id, string Name, string TaskName, int Interval)>
		{
			(1, "Application Update Check", "ApplicationUpdate", 360),
			(2, "Backup", "Backup", 10080),
			(3, "Check Health", "CheckHealth", 360),
			(4, "Clean Up Recycle Bin", "CleanUpRecycleBin", 1440),
			(5, "Housekeeping", "Housekeeping", 1440),
			(6, "Subscription Sync", "SubscriptionSync", 5),
			(7, "Messaging Cleanup", "MessagingCleanup", 5),
			(8, "Refresh Active Downloads", "RefreshMonitoredDownloads", 1),
			(9, "Refresh Channels", "RefreshChannels", 720),
			(10, "Upload Feed Sync", "RssSync", 15),
			(11, "Metadata Mapping Update", "MetadataMappingUpdate", 180),
		};
		return tasks.Select(t =>
		{
			var next = t.Interval > 0 ? now.AddMinutes(t.Interval).ToString("O") : never;
			return new ScheduledTaskDto(
				Id: t.Id,
				Name: t.Name,
				TaskName: t.TaskName,
				Interval: t.Interval,
				LastExecution: never,
				LastStartTime: never,
				LastDuration: "00:00:00",
				NextExecution: next
			);
		}).ToList();
	}
}
