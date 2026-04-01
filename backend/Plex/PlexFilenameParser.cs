using System.Text.RegularExpressions;

namespace TubeArr.Backend.Plex;

internal static class PlexFilenameParser
{
	static readonly Regex BracketSegment = new(@"\[(?<id>[^\]]+)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
	static readonly Regex YoutubeVideoIdToken = new(@"(?<![A-Za-z0-9_-])([A-Za-z0-9_-]{11})(?![A-Za-z0-9_-])", RegexOptions.Compiled);
	static readonly Regex YoutubeChannelIdToken = new(@"(?<![A-Za-z0-9_-])(UC[A-Za-z0-9_-]{20,})(?![A-Za-z0-9_-])", RegexOptions.Compiled);
	static readonly Regex SeasonEpisode = new(@"(?i)\bs(?<s>\d{1,3})e(?<e>\d{1,4})\b", RegexOptions.Compiled);
	static readonly Regex SeasonFolderSegment = new(@"^(?i)Season\s*\d+$|^season\d+$", RegexOptions.Compiled);

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

	internal static bool TryParseEpisodeDisplayTitleFromPath(string? path, out string displayTitle)
	{
		displayTitle = "";
		var p = (path ?? "").Trim();
		if (p.Length == 0)
			return false;

		var noExt = Path.GetFileNameWithoutExtension(Path.GetFileName(p.Replace('/', '\\')));
		if (string.IsNullOrWhiteSpace(noExt))
			return false;

		var m = SeasonEpisodeTitleTail.Match(noExt);
		if (m.Success)
		{
			displayTitle = TrimTrailingBracketIdSegment(m.Groups[1].Value.Trim());
			return displayTitle.Length > 0;
		}

		var mDate = DatePrefixTitle.Match(noExt);
		if (mDate.Success)
		{
			displayTitle = TrimTrailingBracketIdSegment(mDate.Groups[1].Value.Trim());
			return displayTitle.Length > 0;
		}

		return false;
	}

	static readonly Regex SeasonEpisodeTitleTail = new(
		@"(?i)\s-\s*s\d{1,3}e\d{1,4}\s*-\s*(.+)$",
		RegexOptions.Compiled);

	static readonly Regex DatePrefixTitle = new(
		@"^\d{8}\s*-\s*(.+)$",
		RegexOptions.Compiled);

	static string TrimTrailingBracketIdSegment(string s)
	{
		var t = s.Trim();
		var i = t.LastIndexOf(" [", StringComparison.Ordinal);
		if (i <= 0)
			return t;
		return t[..i].Trim();
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

