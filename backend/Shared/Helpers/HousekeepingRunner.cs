using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class HousekeepingRunner
{
	public static async Task<(int MovedQueueItems, bool WalCheckpointOk, string Message)> RunAsync(
		TubeArrDbContext db,
		ILogger logger,
		CancellationToken ct = default)
	{
		var moved = ProgramDbQueueHelpers.MoveCompletedQueueItemsToHistoryBatched(db, logger: logger);

		var walOk = false;
		var provider = db.Database.ProviderName ?? "";
		if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
		{
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
		if (walOk)
			msg += " SQLite WAL checkpoint completed.";

		return (moved, walOk, msg);
	}
}
