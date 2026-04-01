namespace TubeArr.Backend.Plex;

/// <summary>Same layout as <see cref="PlexLibraryArtworkExporter"/> episode thumb: <c>{basename}-thumb.jpg</c> next to the video file.</summary>
internal static class PlexEpisodeSidecarPaths
{
	internal static string? TryGetExistingSidecarPath(string? primaryMediaPath)
	{
		if (string.IsNullOrWhiteSpace(primaryMediaPath))
			return null;

		var dir = Path.GetDirectoryName(primaryMediaPath);
		var baseName = Path.GetFileNameWithoutExtension(primaryMediaPath);
		if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(baseName))
			return null;

		var thumb = Path.Combine(dir, baseName + "-thumb.jpg");
		return File.Exists(thumb) ? thumb : null;
	}
}
