using System.IO;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

/// <summary>
/// Resolves Netscape cookies file paths for yt-dlp: defaults next to the executable or under &lt;contentRoot&gt;/yt-dlp/,
/// plus relative paths stored in config — matching Settings API behavior.
/// </summary>
public static class YtDlpCookiesPathResolver
{
	/// <summary>cookies.txt next to the yt-dlp binary when executable path is set; otherwise &lt;contentRoot&gt;/yt-dlp/cookies.txt.</summary>
	public static string GetDefaultCookiesTxtPath(string? executablePath, string contentRoot)
	{
		var exe = (executablePath ?? "").Trim();
		if (!string.IsNullOrWhiteSpace(exe))
		{
			try
			{
				var dir = Path.GetDirectoryName(exe);
				if (!string.IsNullOrWhiteSpace(dir))
					return Path.Combine(dir, "cookies.txt");
			}
			catch
			{
				/* fall through */
			}
		}

		return Path.Combine(contentRoot, "yt-dlp", "cookies.txt");
	}

	/// <summary>
	/// Returns an absolute path to an existing cookies file, or null. Honors <see cref="YtDlpConfigEntity.CookiesPath"/> when
	/// that file exists; if it is missing (stale setting), falls back to the same default locations as when CookiesPath is empty
	/// (next to yt-dlp exe, then &lt;contentRoot&gt;/yt-dlp/cookies.txt, etc.).
	/// </summary>
	public static string? GetEffectiveCookiesFilePath(YtDlpConfigEntity config, string? contentRoot)
	{
		var exe = (config.ExecutablePath ?? "").Trim();
		var configured = (config.CookiesPath ?? "").Trim();

		if (string.IsNullOrWhiteSpace(contentRoot))
		{
			if (string.IsNullOrWhiteSpace(configured))
				return null;
			return FirstExistingFullPath(configured);
		}

		if (!string.IsNullOrWhiteSpace(configured))
		{
			if (Path.IsPathRooted(configured))
			{
				var rooted = FirstExistingFullPath(configured);
				if (rooted is not null)
					return rooted;
			}
			else
			{
				var relCandidates = new List<string>();
				try
				{
					if (!string.IsNullOrWhiteSpace(exe))
					{
						var dir = Path.GetDirectoryName(exe);
						if (!string.IsNullOrWhiteSpace(dir))
							relCandidates.Add(Path.Combine(dir, configured));
					}
				}
				catch
				{
					/* ignore */
				}

				relCandidates.Add(Path.Combine(contentRoot, configured));
				relCandidates.Add(Path.Combine(contentRoot, "yt-dlp", configured));

				foreach (var c in relCandidates)
				{
					var found = FirstExistingFullPath(c);
					if (found is not null)
						return found;
				}
			}
		}

		foreach (var c in EnumerateDefaultCookieSearchPaths(exe, contentRoot))
		{
			var found = FirstExistingFullPath(c);
			if (found is not null)
				return found;
		}

		return null;
	}

	/// <summary>Same search order as Settings → auto-detect cookies.</summary>
	public static IEnumerable<string> EnumerateDefaultCookieSearchPaths(string? executablePath, string contentRoot)
	{
		yield return GetDefaultCookiesTxtPath(executablePath, contentRoot);
		yield return Path.Combine(contentRoot, "..", "_output", "cookies.txt");
		yield return Path.Combine(contentRoot, "..", "cookies.txt");
		yield return Path.Combine(contentRoot, "_output", "cookies.txt");
	}

	static string? FirstExistingFullPath(string path)
	{
		try
		{
			var full = Path.GetFullPath(path.Trim());
			return File.Exists(full) ? full : null;
		}
		catch
		{
			return null;
		}
	}
}
