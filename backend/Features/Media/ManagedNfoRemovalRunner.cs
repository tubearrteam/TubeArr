using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

/// <summary>Deletes library NFO files recorded in each root folder’s <see cref="TubeArrManagedLibraryManifest"/> (kind <c>nfo</c>).</summary>
internal static class ManagedNfoRemovalRunner
{
	public static async Task<ManagedNfoRemovalResult> RunAsync(TubeArrDbContext db, ILogger logger, CancellationToken ct)
	{
		var rootFolders = await db.RootFolders.AsNoTracking().ToListAsync(ct);
		if (rootFolders.Count == 0)
			return new ManagedNfoRemovalResult(0, 0, 0, "No root folders configured.");

		var rootFoldersScanned = 0;
		var filesDeleted = 0;
		var filesMissing = 0;

		foreach (var rf in rootFolders)
		{
			ct.ThrowIfCancellationRequested();
			var path = (rf.Path ?? "").Trim();
			if (string.IsNullOrWhiteSpace(path))
				continue;

			string normalized;
			try
			{
				normalized = Path.GetFullPath(path);
			}
			catch
			{
				continue;
			}

			if (!Directory.Exists(normalized))
				continue;

			var manifestPath = Path.Combine(normalized, TubeArrManagedLibraryManifest.ManifestFileName);
			if (!File.Exists(manifestPath))
				continue;

			rootFoldersScanned++;
			try
			{
				var (del, miss) = TubeArrManagedLibraryManifest.RemoveManagedNfoFiles(normalized);
				filesDeleted += del;
				filesMissing += miss;
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Remove managed NFOs failed for libraryRoot={LibraryRoot}", normalized);
			}
		}

		var msg = $"Scanned {rootFoldersScanned} library root folder(s) with {TubeArrManagedLibraryManifest.ManifestFileName}; deleted {filesDeleted} NFO file(s); {filesMissing} listed path(s) had no file.";
		return new ManagedNfoRemovalResult(rootFoldersScanned, filesDeleted, filesMissing, msg);
	}
}

internal readonly record struct ManagedNfoRemovalResult(
	int ShowFoldersScanned,
	int NfosDeleted,
	int NfosAlreadyMissing,
	string Message);
