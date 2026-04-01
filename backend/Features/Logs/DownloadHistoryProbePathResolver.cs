using TubeArr.Backend.Data;

namespace TubeArr.Backend;

/// <summary>
/// Chooses which on-disk path to ffprobe for optional history columns. Failed/deleted/etc. rows must not fall back
/// to the video's current library file, or a later successful download makes older failures look like imports.
/// </summary>
internal static class DownloadHistoryProbePathResolver
{
	internal static string? Resolve(DownloadHistoryEntity h, IReadOnlyDictionary<int, string?> latestFilePathByVideoId)
	{
		if (!string.IsNullOrWhiteSpace(h.OutputPath) && File.Exists(h.OutputPath))
			return h.OutputPath;

		// Imported (3) and renamed (6) rows describe library file state; allow DB fallback when the stored path moved.
		if (h.EventType is not (3 or 6))
			return null;

		if (latestFilePathByVideoId.TryGetValue(h.VideoId, out var tracked) &&
		    !string.IsNullOrWhiteSpace(tracked) && File.Exists(tracked))
			return tracked;

		return null;
	}
}
