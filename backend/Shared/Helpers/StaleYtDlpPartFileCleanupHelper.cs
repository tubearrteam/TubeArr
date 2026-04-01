using Microsoft.Extensions.Logging;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

/// <summary>
/// Removes yt-dlp partial download files (<c>*.part</c>) that have not been written for a while (abandoned / crashed runs).
/// </summary>
internal static class StaleYtDlpPartFileCleanupHelper
{
	public static (int DeletedFiles, int Errors, string Message) Cleanup(
		IReadOnlyList<RootFolderEntity> rootFolders,
		string contentRootPath,
		ILogger logger,
		TimeSpan minAgeWithoutWrite,
		Func<string, Task>? reportProgress,
		CancellationToken ct)
	{
		if (rootFolders.Count == 0)
			return (0, 0, "");

		var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var rf in rootFolders)
		{
			var resolved = ResolveRootPath(rf.Path, contentRootPath);
			if (!string.IsNullOrWhiteSpace(resolved))
				roots.Add(resolved);
		}

		if (roots.Count == 0)
			return (0, 0, "");

		var cutoff = DateTime.UtcNow - minAgeWithoutWrite;
		var deleted = 0;
		var errors = 0;

		foreach (var root in roots)
		{
			if (ct.IsCancellationRequested)
				break;
			if (!Directory.Exists(root))
				continue;

			if (reportProgress is not null)
			{
				try { reportProgress($"Housekeeping: checking {root}…").GetAwaiter().GetResult(); }
				catch { /* best-effort */ }
			}

			IEnumerable<string> files;
			try
			{
				files = Directory.EnumerateFiles(root, "*.part", SearchOption.AllDirectories);
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Housekeeping: could not enumerate .part files under {Root}", root);
				errors++;
				continue;
			}

			foreach (var file in files)
			{
				if (ct.IsCancellationRequested)
					break;
				try
				{
					var lastWrite = File.GetLastWriteTimeUtc(file);
					if (lastWrite > cutoff)
						continue;
					File.Delete(file);
					deleted++;
				}
				catch (Exception ex)
				{
					errors++;
					logger.LogWarning(ex, "Housekeeping: failed to delete stale .part file {Path}", file);
				}
			}
		}

		if (deleted == 0 && errors == 0)
			return (0, 0, "No stale .part files found under root folders.");

		var msg = errors == 0
			? $"Removed {deleted} stale .part file(s)."
			: $"Removed {deleted} stale .part file(s); {errors} error(s).";
		return (deleted, errors, msg);
	}

	static string ResolveRootPath(string? raw, string contentRootPath)
	{
		var trimmed = (raw ?? "").Trim();
		if (string.IsNullOrWhiteSpace(trimmed))
			return "";

		return Path.IsPathRooted(trimmed)
			? Path.GetFullPath(trimmed)
			: Path.GetFullPath(Path.Combine(contentRootPath, trimmed));
	}
}
