using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class ScheduledTaskCatalog
{
	internal static readonly DateTimeOffset ProcessStartUtc = DateTimeOffset.UtcNow;

	internal static readonly IReadOnlyList<ScheduledTaskCatalogEntry> Entries = new List<ScheduledTaskCatalogEntry>
	{
		new(1, "Application Update Check", "ApplicationUpdate", 360),
		new(2, "Backup", "Backup", 10080),
		new(3, "Check Health", "CheckHealth", 360),
		new(4, "Clean Up Recycle Bin", "CleanUpRecycleBin", 1440),
		new(5, "Housekeeping", "Housekeeping", 1440),
		new(6, "Messaging Cleanup", "MessagingCleanup", 10080),
		new(7, "Refresh Monitored Downloads", "RefreshMonitoredDownloads", 1),
		new(8, "Refresh Channels", "RefreshChannels", 10080),
		new(9, "Upload Feed Sync", "RssSync", 15),
		new(10, "Map Unmapped Video Files", "MapUnmappedVideoFiles", 15),
		new(11, "Sync Library NFOs", "SyncCustomNfos", 1440),
		new(12, "Download new thumbnails", "RepairLibraryNfosAndArtwork", 10080),
	};

	internal static string GetDisplayName(string taskName)
	{
		foreach (var e in Entries)
		{
			if (string.Equals(e.TaskName, taskName, StringComparison.OrdinalIgnoreCase))
				return e.Name;
		}

		return taskName;
	}

	internal static bool RecordsRuns(string taskName) =>
		string.Equals(taskName, "ApplicationUpdate", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(taskName, "Backup", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(taskName, "CheckHealth", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(taskName, "CleanUpRecycleBin", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(taskName, "Housekeeping", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(taskName, "MessagingCleanup", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(taskName, "RefreshChannels", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(taskName, "RssSync", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(taskName, "RefreshMonitoredDownloads", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(taskName, "MapUnmappedVideoFiles", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(taskName, "SyncCustomNfos", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(taskName, "RepairLibraryNfosAndArtwork", StringComparison.OrdinalIgnoreCase);

	internal static async Task<List<ScheduledTaskDto>> GetScheduledTaskDtosAsync(TubeArrDbContext db, CancellationToken ct = default)
	{
		var states = await db.ScheduledTaskStates.AsNoTracking().ToListAsync(ct);
		var byName = states.ToDictionary(x => x.TaskName, StringComparer.OrdinalIgnoreCase);
		var overrides = await db.ScheduledTaskIntervalOverrides.AsNoTracking()
			.ToDictionaryAsync(x => x.TaskName, x => x.IntervalMinutes, StringComparer.OrdinalIgnoreCase, ct);
		var media = await db.MediaManagementConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		var customNfosEnabled = media?.UseCustomNfos != false;
		var plex = await db.PlexProviderConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		var plexEnabled = plex?.Enabled == true;
		var downloadNewThumbnailsTaskEnabled = LibraryThumbnailExportPolicy.ShouldExport(
			media?.DownloadLibraryThumbnails == true,
			plexEnabled);

		var list = new List<ScheduledTaskDto>(Entries.Count);
		foreach (var t in Entries)
		{
			byName.TryGetValue(t.TaskName, out var state);

			var interval = t.Interval;
			if (string.Equals(t.TaskName, "SyncCustomNfos", StringComparison.OrdinalIgnoreCase) && !customNfosEnabled)
				interval = 0;
			if (string.Equals(t.TaskName, "RepairLibraryNfosAndArtwork", StringComparison.OrdinalIgnoreCase) && !downloadNewThumbnailsTaskEnabled)
				interval = 0;
			int? intervalOverride = overrides.TryGetValue(t.TaskName, out var ovr) && ovr > 0 ? ovr : null;
			if (interval > 0 && intervalOverride is { } ovrMin && ovrMin > 0)
				interval = ovrMin;

			string? lastExecution = null;
			string? lastStart = null;
			string? lastDuration = null;
			if (state?.LastCompletedAt is { } completed)
			{
				lastExecution = completed.ToString("O");
				if (state.LastDurationTicks is { } ticks && ticks >= 0)
				{
					var maxSubtractableTicks = completed.Ticks - DateTimeOffset.MinValue.Ticks;
					var startedAt = ticks <= maxSubtractableTicks ? completed.AddTicks(-ticks) : completed;
					lastStart = startedAt.ToString("O");
					lastDuration = CommandRecordFactory.FormatCommandDuration(TimeSpan.FromTicks(ticks));
				}
				else
					lastStart = completed.ToString("O");
			}

			string? next = null;
			if (interval > 0)
			{
				var anchor = state?.LastCompletedAt ?? ProcessStartUtc;
				next = anchor.AddMinutes(interval).ToString("O");
			}

			list.Add(new ScheduledTaskDto(
				Id: t.Id,
				Name: t.Name,
				TaskName: t.TaskName,
				Interval: interval,
				DefaultInterval: t.Interval,
				IntervalOverride: intervalOverride,
				LastExecution: lastExecution,
				LastStartTime: lastStart,
				LastDuration: lastDuration,
				NextExecution: next));
		}

		return list;
	}
}

internal sealed record ScheduledTaskCatalogEntry(int Id, string Name, string TaskName, int Interval);
