using Microsoft.Extensions.Logging;

namespace TubeArr.Backend;

internal static class RecycleBinCleanupHelper
{
	public static (int DeletedFiles, int Errors, string Message) CleanupOldFiles(
		string? recycleBinPathRaw,
		int cleanupDays,
		string contentRootPath,
		ILogger? logger)
	{
		if (string.IsNullOrWhiteSpace(recycleBinPathRaw))
			return (0, 0, "Recycle bin is not configured.");

		var trimmed = recycleBinPathRaw.Trim();
		var fullPath = Path.IsPathRooted(trimmed)
			? Path.GetFullPath(trimmed)
			: Path.GetFullPath(Path.Combine(contentRootPath, trimmed));

		if (!Directory.Exists(fullPath))
			return (0, 0, $"Recycle bin folder does not exist ({fullPath}).");

		var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, cleanupDays));
		var deleted = 0;
		var errors = 0;

		foreach (var file in Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories))
		{
			DateTime lastWrite;
			try
			{
				lastWrite = File.GetLastWriteTimeUtc(file);
			}
			catch (Exception ex)
			{
				logger?.LogDebug(ex, "Recycle bin: could not read file time {Path}", file);
				continue;
			}

			if (lastWrite > cutoff)
				continue;

			try
			{
				File.Delete(file);
				deleted++;
			}
			catch (Exception ex)
			{
				errors++;
				logger?.LogWarning(ex, "Recycle bin: failed to delete {Path}", file);
			}
		}

		var msg = $"Deleted {deleted} file(s) older than {cleanupDays} day(s) from recycle bin.";
		if (errors > 0)
			msg += $" {errors} file(s) could not be deleted.";

		return (deleted, errors, msg);
	}
}
