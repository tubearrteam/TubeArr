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

		var query = db.DownloadHistory.Where(h => h.Date < cutoff);
		var oldCount = await query.CountAsync(ct);
		if (oldCount == 0)
			return (0, $"No download history entries older than {retentionDays} day(s).");

		await query.ExecuteDeleteAsync(ct);

		logger.LogInformation("Messaging cleanup removed {Count} download history row(s) older than {Days} days.", oldCount, retentionDays);

		return (oldCount, $"Removed {oldCount} download history entr(y/ies) older than {retentionDays} day(s).");
	}
}
