using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class DownloadHistoryCleanupHelper
{
	public static async Task<(int Removed, string Message)> PruneOlderThanAsync(
		TubeArrDbContext db,
		int retentionDays,
		ILogger logger,
		CancellationToken ct = default)
	{
		retentionDays = Math.Max(1, retentionDays);
		var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

		var old = await db.DownloadHistory
			.Where(h => h.Date < cutoff)
			.ToListAsync(ct);

		if (old.Count == 0)
			return (0, $"No download history entries older than {retentionDays} day(s).");

		db.DownloadHistory.RemoveRange(old);
		await db.SaveChangesAsync(ct);

		logger.LogInformation("Messaging cleanup removed {Count} download history row(s) older than {Days} days.", old.Count, retentionDays);

		return (old.Count, $"Removed {old.Count} download history entr(y/ies) older than {retentionDays} day(s).");
	}
}
