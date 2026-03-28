using System.IO;

namespace TubeArr.Backend;

/// <summary>
/// Builds yt-dlp command-line arguments for channel resolution and search.
/// All channel metadata/discovery commands go through this builder; no ad hoc strings in controllers.
/// </summary>
public static class YtDlpCommandBuilder
{
	/// <summary>Base arguments for metadata-only runs: no download, machine-readable output.</summary>
	public static readonly IReadOnlyList<string> BaseArgs = new[]
	{
		"--skip-download",
		"--encoding", "utf-8"
	};

	/// <summary>Adds <c>--cookies</c> when the Netscape file exists. Returns whether the file was readable.</summary>
	public static bool TryAppendYoutubeCookiesFile(List<string> args, string? cookiesPath)
	{
		if (string.IsNullOrWhiteSpace(cookiesPath))
			return false;
		try
		{
			var full = Path.GetFullPath(cookiesPath.Trim());
			if (!File.Exists(full))
				return false;
			args.Add("--cookies");
			args.Add(full);
			return true;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>Optional Netscape cookies file — for metadata/resolve paths.</summary>
	public static void AppendYoutubeAuthMitigations(List<string> args, string? cookiesPath) =>
		TryAppendYoutubeCookiesFile(args, cookiesPath);

	/// <summary>Exact resolve: first uploads-tab item only (-j --playlist-items 1).
	/// Works for https://www.youtube.com/channel/UC.../videos and https://www.youtube.com/@handle/videos.</summary>
	/// <param name="url">Channel uploads URL (must be a tab that lists videos).</param>
	/// <param name="cookiesPath">Optional path to cookies file (Netscape format). Uses --cookies if provided.</param>
	public static IReadOnlyList<string> BuildExactResolveArgs(string url, string? cookiesPath = null, bool verbose = false)
	{
		var args = new List<string>(BaseArgs);
		AppendYoutubeAuthMitigations(args, cookiesPath);
		if (verbose)
			args.Add("--verbose");
		args.Add("-j");
		args.Add("--playlist-items");
		args.Add("1");
		args.Add(url);
		return args;
	}

	/// <summary>Build arguments for channel search by free text. Uses ytsearchN:term. Full JSON per result for channel metadata.</summary>
	/// <param name="term">Search term (will be appended to ytsearchN:).</param>
	/// <param name="maxResults">N in ytsearchN (e.g. 10 or 20).</param>
	/// <param name="cookiesPath">Optional path to cookies file (Netscape format). Uses --cookies if provided.</param>
	public static IReadOnlyList<string> BuildSearchArgs(string term, int maxResults = 20, string? cookiesPath = null, bool verbose = false)
	{
		var args = new List<string>(BaseArgs);
		AppendYoutubeAuthMitigations(args, cookiesPath);
		if (verbose)
			args.Add("--verbose");
		args.Add("-j");
		args.Add($"ytsearch{maxResults}: {term}");
		return args;
	}

	/// <summary>Escape an argument for safe use in a single process argument string. Prefer passing list to runner that uses no shell.</summary>
	public static string EscapeArg(string arg)
	{
		if (string.IsNullOrEmpty(arg)) return "\"\"";
		if (!arg.Contains(' ') && !arg.Contains('"') && !arg.Contains('\'')) return arg;
		return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
	}
}
