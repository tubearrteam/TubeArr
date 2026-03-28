namespace TubeArr.Backend;

/// <summary>
/// Set at startup to the host content root so yt-dlp and other static helpers can resolve relative paths
/// (e.g. cookies.txt) the same way HTTP endpoints do.
/// </summary>
public static class TubeArrAppPaths
{
	public static string? ContentRoot { get; set; }
}
