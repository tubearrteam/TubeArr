namespace TubeArr.Backend.Plex;

/// <summary>
/// Paths relative to the provider base URL (e.g. <c>http://host:port/tv</c>), per Plex Custom Metadata Provider docs.
/// </summary>
internal static class PlexKeys
{
	internal static string LibraryMetadata(string ratingKey) =>
		"/library/metadata/" + (ratingKey ?? "").Trim();

	/// <summary>Show and season metadata objects use this for <c>key</c> so Plex can list child items (per Plex Metadata.md examples).</summary>
	internal static string LibraryMetadataChildren(string ratingKey) =>
		LibraryMetadata(ratingKey) + "/children";
}
