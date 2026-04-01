using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class HousekeepingRunner
{
	/// <summary>Partial downloads older than this without a write are treated as dead (yt-dlp <c>*.part</c> files).</summary>
	internal static readonly TimeSpan StalePartFileMinAge = TimeSpan.FromHours(24);

	public static async Task<(int MovedQueueItems, bool WalCheckpointOk, string Message)> RunAsync(
		TubeArrDbContext db,
		ILogger logger,
		string contentRootPath,
		Func<string, Task>? reportProgress = null,
		CancellationToken ct = default)
	{
		if (reportProgress is not null)
			await reportProgress("Housekeeping: moving completed queue items…");
		var moved = await ProgramDbQueueHelpers.MoveCompletedQueueItemsToHistoryBatchedAsync(db, 250, logger, ct);
		if (reportProgress is not null)
			await reportProgress($"Housekeeping: moved {moved} completed queue row(s) to history.");

		if (reportProgress is not null)
			await reportProgress($"Housekeeping: scanning for stale yt-dlp .part files (>{StalePartFileMinAge.TotalHours:0}h) under root folders…");
		var rootFolders = await db.RootFolders.AsNoTracking().ToListAsync(ct);
		var (_, _, partMsg) = StaleYtDlpPartFileCleanupHelper.Cleanup(
			rootFolders,
			contentRootPath,
			logger,
			StalePartFileMinAge,
			reportProgress,
			ct);
		if (reportProgress is not null && !string.IsNullOrWhiteSpace(partMsg))
			await reportProgress("Housekeeping: " + partMsg);

		var walOk = false;
		var provider = db.Database.ProviderName ?? "";
		if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
		{
			if (reportProgress is not null)
				await reportProgress("Housekeeping: running SQLite WAL checkpoint…");
			try
			{
				await db.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);", ct);
				walOk = true;
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "SQLite WAL checkpoint failed.");
			}
		}

		var msg = $"Moved {moved} completed queue row(s) to history.";
		if (!string.IsNullOrEmpty(partMsg))
			msg += " " + partMsg;
		if (walOk)
			msg += " SQLite WAL checkpoint completed.";

		return (moved, walOk, msg);
	}
}
