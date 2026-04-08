namespace TubeArr.Backend.Media;

/// <summary>Extensions scanned when linking or resolving media files on disk.</summary>
internal static class MediaFileKnownExtensions
{
	internal static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
	{
		".mp4", ".mkv", ".webm", ".avi", ".mov", ".m4v", ".flv", ".wmv", ".mpg", ".mpeg",
		".m4a", ".mp3", ".aac", ".opus", ".ogg", ".wav", ".flac"
	};
}
