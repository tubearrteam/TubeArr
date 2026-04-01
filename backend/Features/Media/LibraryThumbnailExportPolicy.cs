namespace TubeArr.Backend;

/// <summary>Episode thumbnail sidecars are written when the user opts in or when the Plex metadata provider is on.</summary>
internal static class LibraryThumbnailExportPolicy
{
	public static bool ShouldExport(bool downloadLibraryThumbnails, bool plexProviderEnabled) =>
		downloadLibraryThumbnails || plexProviderEnabled;
}
