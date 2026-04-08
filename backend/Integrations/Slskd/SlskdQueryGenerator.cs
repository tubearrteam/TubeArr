using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TubeArr.Backend.Data;

namespace TubeArr.Backend.Integrations.Slskd;

/// <summary>Staged Soulseek search strings from TubeArr-owned metadata only.</summary>
public static class SlskdQueryGenerator
{
	static readonly string[] NoisePhrases =
	{
		"official video", "official audio", "lyric video", "reupload", "mirror", "clip", "trailer", "reaction"
	};

	public static IReadOnlyList<string> BuildStages(VideoEntity video, ChannelEntity channel)
	{
		var vid = NormalizeVideoId(video.YoutubeVideoId);
		var title = (video.Title ?? "").Trim();
		var ch = (channel.Title ?? "").Trim();

		var list = new List<string>();

		if (!string.IsNullOrEmpty(vid))
			list.Add(vid);

		if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(vid))
			list.Add($"{title} {vid}");

		if (!string.IsNullOrEmpty(ch) && !string.IsNullOrEmpty(vid))
			list.Add($"{ch} {vid}");

		if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(ch))
			list.Add($"{title} {ch}");

		var nt = NormalizeForMatch(title);
		var nc = NormalizeForMatch(ch);
		if (!string.IsNullOrEmpty(nt) && !string.IsNullOrEmpty(nc))
			list.Add($"{nt} {nc}");

		if (!string.IsNullOrEmpty(nt))
			list.Add(nt);

		var loose = StripBracketed(RemoveNoisePhrases(nt));
		if (!string.IsNullOrEmpty(loose) && !list.Contains(loose, StringComparer.OrdinalIgnoreCase))
			list.Add(loose);

		return Dedupe(list);
	}

	internal static string NormalizeVideoId(string? raw) =>
		DownloadQueueProcessor.SanitizeYoutubeVideoIdForWatchUrl(raw);

	internal static string NormalizeForMatch(string s)
	{
		if (string.IsNullOrWhiteSpace(s))
			return "";
		var t = s.Trim().ToLowerInvariant();
		try
		{
			t = t.Normalize(NormalizationForm.FormC);
		}
		catch
		{
			/* ignore */
		}

		t = t.Replace('&', ' ').Replace(" and ", " ", StringComparison.Ordinal);
		t = Regex.Replace(t, @"\[[^\]]*\]", " ");
		t = Regex.Replace(t, @"\([^)]*\)", " ");
		t = Regex.Replace(t, @"[^\p{L}\p{N}\s]+", " ");
		t = Regex.Replace(t, @"\s+", " ").Trim();
		return RemoveNoisePhrases(t);
	}

	static string RemoveNoisePhrases(string t)
	{
		if (string.IsNullOrEmpty(t))
			return t;
		var x = t;
		foreach (var n in NoisePhrases)
			x = Regex.Replace(x, @"\b" + Regex.Escape(n) + @"\b", " ", RegexOptions.IgnoreCase);
		return Regex.Replace(x, @"\s+", " ").Trim();
	}

	static string StripBracketed(string t) =>
		string.IsNullOrEmpty(t) ? t : Regex.Replace(t, @"\[[^\]]*\]", " ").Trim();

	static List<string> Dedupe(List<string> items)
	{
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var o = new List<string>();
		foreach (var x in items)
		{
			var z = x.Trim();
			if (z.Length < 2)
				continue;
			if (seen.Add(z))
				o.Add(z);
		}

		return o;
	}
}
