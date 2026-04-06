using System.Text.RegularExpressions;

namespace TubeArr.Backend.Plex;

internal static class PlexFilenameParser
{
	static readonly Regex BracketSegment = new(@"\[(?<id>[^\]]+)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
	static readonly Regex YoutubeVideoIdToken = new(@"(?<![A-Za-z0-9_-])([A-Za-z0-9_-]{11})(?![A-Za-z0-9_-])", RegexOptions.Compiled);
	static readonly Regex YoutubeChannelIdToken = new(@"(?<![A-Za-z0-9_-])(UC[A-Za-z0-9_-]{20,})(?![A-Za-z0-9_-])", RegexOptions.Compiled);
	static readonly Regex SeasonEpisode = new(@"(?i)\bs(?<s>\d{1,3})e(?<e>\d{1,4})\b", RegexOptions.Compiled);
	static readonly Regex SeasonFolderSegment = new(@"^(?i)Season\s*\d+$|^season\d+$", RegexOptions.Compiled);
	static readonly Regex SeasonFolderWithNumber = new(@"^(?i)season\s*(?<n>\d+)\s*$", RegexOptions.Compiled);

	internal static bool TryParseYoutubeVideoIdFromPath(string? path, out string youtubeVideoId)
	{
		youtubeVideoId = "";
		var p = path ?? "";
		if (string.IsNullOrWhiteSpace(p))
			return false;

		var fileName = Path.GetFileName(p) ?? "";
		foreach (Match m in BracketSegment.Matches(fileName))
		{
			var inner = m.Groups["id"].Value.Trim();
			if (inner.Length == 11 && PlexIdentifier.IsSafeRatingKey("v_" + inner))
			{
				youtubeVideoId = inner;
				return true;
			}
		}

		foreach (Match m in BracketSegment.Matches(p))
		{
			var inner = m.Groups["id"].Value.Trim();
			if (inner.Length == 11 && PlexIdentifier.IsSafeRatingKey("v_" + inner))
			{
				youtubeVideoId = inner;
				return true;
			}
		}

		var token = YoutubeVideoIdToken.Match(p);
		if (token.Success)
		{
			youtubeVideoId = token.Groups[1].Value;
			return true;
		}

		return false;
	}

	internal static bool TryParseYoutubeChannelIdFromPath(string? path, out string youtubeChannelId)
	{
		youtubeChannelId = "";
		var p = path ?? "";
		if (string.IsNullOrWhiteSpace(p))
			return false;

		foreach (Match m in BracketSegment.Matches(p))
		{
			var inner = m.Groups["id"].Value.Trim();
			if (inner.StartsWith("UC", StringComparison.OrdinalIgnoreCase) && PlexIdentifier.IsSafeRatingKey("ch_" + inner))
			{
				youtubeChannelId = inner;
				return true;
			}
		}

		var token = YoutubeChannelIdToken.Match(p);
		if (token.Success)
		{
			youtubeChannelId = token.Value;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Plex often sends full Windows paths without <c>[UC…]</c> in folder names (e.g. <c>D:\Youtube\Blitz\Season 01\file.mp4</c>).
	/// Returns the rightmost path segment that is not a <c>Season NN</c> folder — typically the show folder (here: Blitz).
	/// </summary>
	internal static bool TryGetShowFolderNameFromPath(string? path, out string folderName)
	{
		folderName = "";
		var p = (path ?? "").Trim();
		if (p.Length == 0)
			return false;
		p = p.Replace('/', '\\');
		if (Path.GetExtension(p).Length > 0)
			p = Path.GetDirectoryName(p) ?? p;
		var parts = p.Split('\\', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0)
			return false;
		for (var i = parts.Length - 1; i >= 0; i--)
		{
			var seg = parts[i].Trim();
			if (seg.Length == 0)
				continue;
			if (seg.Length == 2 && seg[1] == ':')
				continue;
			if (SeasonFolderSegment.IsMatch(seg))
				continue;
			folderName = seg;
			return true;
		}
		return false;
	}

	/// <summary>Rightmost path segment matching <c>Season NN</c> (e.g. file in <c>...\Season 10001\</c> → 10001).</summary>
	internal static bool TryParseDeepestSeasonFolderNumberFromPath(string? path, out int seasonNumber)
	{
		seasonNumber = 0;
		var p = (path ?? "").Trim();
		if (p.Length == 0)
			return false;
		p = p.Replace('/', '\\');
		if (Path.GetExtension(p).Length > 0)
		{
			var dir = Path.GetDirectoryName(p);
			if (!string.IsNullOrEmpty(dir))
				p = dir;
		}

		var parts = p.Split('\\', StringSplitOptions.RemoveEmptyEntries);
		for (var i = parts.Length - 1; i >= 0; i--)
		{
			var seg = parts[i].Trim();
			if (seg.Length == 0)
				continue;
			if (seg.Length == 2 && seg[1] == ':')
				continue;
			var m = SeasonFolderWithNumber.Match(seg);
			if (m.Success && int.TryParse(m.Groups["n"].Value, out var n) && n > 0)
			{
				seasonNumber = n;
				return true;
			}
		}

		return false;
	}

	internal static bool TryParseSeasonEpisodeFromPath(string? path, out int seasonIndex, out int episodeIndex)
	{
		seasonIndex = 0;
		episodeIndex = 0;
		var p = path ?? "";
		if (string.IsNullOrWhiteSpace(p))
			return false;

		var m = SeasonEpisode.Match(p);
		if (!m.Success)
			return false;

		if (!int.TryParse(m.Groups["s"].Value, out seasonIndex) || seasonIndex <= 0)
			return false;
		if (!int.TryParse(m.Groups["e"].Value, out episodeIndex) || episodeIndex <= 0)
			return false;

		return true;
	}
}

