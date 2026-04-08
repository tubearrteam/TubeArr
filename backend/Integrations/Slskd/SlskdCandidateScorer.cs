using System.IO;
using TubeArr.Backend.Data;

namespace TubeArr.Backend.Integrations.Slskd;

public static class SlskdCandidateScorer
{
	public static void ScoreAndAttach(
		ExternalDownloadCandidateDto c,
		VideoEntity video,
		ChannelEntity channel,
		string queryUsed,
		int stageIndex)
	{
		var signals = new List<ScoreSignalDto>();
		var score = 0;
		var vid = SlskdQueryGenerator.NormalizeVideoId(video.YoutubeVideoId);
		var title = (video.Title ?? "").Trim();
		var chTitle = (channel.Title ?? "").Trim();
		var pathBlob = $"{c.Filename} {c.Username}".ToLowerInvariant();

		if (!string.IsNullOrEmpty(vid) && ContainsVideoId(pathBlob, vid))
		{
			score += 100;
			signals.Add(new ScoreSignalDto { Code = "youtubeVideoIdInPath", Weight = 100, Detail = vid });
		}

		var nt = SlskdQueryGenerator.NormalizeForMatch(title);
		var nc = SlskdQueryGenerator.NormalizeForMatch(chTitle);
		var nf = SlskdQueryGenerator.NormalizeForMatch(Path.GetFileName(c.Filename));

		if (!string.IsNullOrEmpty(nt) && (!string.IsNullOrEmpty(nf) && nf.Contains(nt, StringComparison.Ordinal)
		                                  || nt.Equals(nf, StringComparison.OrdinalIgnoreCase)))
		{
			score += 40;
			signals.Add(new ScoreSignalDto { Code = "titleMatch", Weight = 40 });
		}
		else if (!string.IsNullOrEmpty(nt) && !string.IsNullOrEmpty(nf))
		{
			var sim = TokenJaccard(nt, nf);
			var add = (int)Math.Clamp(sim * 35, 0, 35);
			if (add > 0)
			{
				score += add;
				signals.Add(new ScoreSignalDto { Code = "titleSimilarity", Weight = add, Detail = sim.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) });
			}
		}

		if (!string.IsNullOrEmpty(nc) && pathBlob.Contains(nc, StringComparison.OrdinalIgnoreCase))
		{
			score += 25;
			signals.Add(new ScoreSignalDto { Code = "channelInPath", Weight = 25 });
		}

		var chId = (channel.YoutubeChannelId ?? "").Trim();
		if (!string.IsNullOrEmpty(chId) && pathBlob.Contains(chId, StringComparison.OrdinalIgnoreCase))
		{
			score += 8;
			signals.Add(new ScoreSignalDto { Code = "youtubeChannelIdBonus", Weight = 8 });
		}

		if (video.Runtime > 0 && c.DurationSeconds is > 0)
		{
			var d = Math.Abs(video.Runtime - c.DurationSeconds.Value);
			int w;
			if (d <= 2)
				w = 20;
			else if (d <= 10)
				w = 12;
			else if (d <= 60)
				w = 6;
			else
				w = -Math.Min(20, d / 30);
			score += w;
			if (w != 0)
				signals.Add(new ScoreSignalDto { Code = "durationDelta", Weight = w, Detail = $"{d}s" });
		}

		if (c.Size > 0 && video.Runtime > 0)
		{
			var expected = video.Runtime * 200_000L;
			if (c.Size < expected / 20)
			{
				score -= 15;
				signals.Add(new ScoreSignalDto { Code = "sizeImplausibleSmall", Weight = -15 });
			}
			else if (c.Size <= expected * 80)
				signals.Add(new ScoreSignalDto { Code = "sizePlausible", Weight = 10 });
		}

		foreach (var p in new[] { "sample", "preview", "reaction", " cam ", "trailer" })
		{
			if (pathBlob.Contains(p, StringComparison.OrdinalIgnoreCase))
			{
				score -= 15;
				signals.Add(new ScoreSignalDto { Code = "suspiciousKeyword", Weight = -15, Detail = p.Trim() });
			}
		}

		if (stageIndex >= 5)
		{
			score -= 10;
			signals.Add(new ScoreSignalDto { Code = "lateStageQuery", Weight = -10 });
		}

		c.MatchScore = Math.Clamp(score, -1000, 500);
		c.Confidence = Bucket(c.MatchScore, vid, nt, nc, pathBlob, c);
		c.MatchedSignals = signals;
		c.SearchQueryUsed = queryUsed;
	}

	static bool ContainsVideoId(string haystack, string videoId)
	{
		if (string.IsNullOrEmpty(videoId))
			return false;
		return haystack.Contains(videoId, StringComparison.OrdinalIgnoreCase);
	}

	static double TokenJaccard(string a, string b)
	{
		var ta = new HashSet<string>(a.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
		var tb = new HashSet<string>(b.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
		if (ta.Count == 0 || tb.Count == 0)
			return 0;
		var inter = ta.Intersect(tb).Count();
		var union = ta.Union(tb).Count();
		return union == 0 ? 0 : (double)inter / union;
	}

	static string Bucket(int score, string vid, string nt, string nc, string pathBlob, ExternalDownloadCandidateDto c)
	{
		var idHit = !string.IsNullOrEmpty(vid) && ContainsVideoId(pathBlob, vid);
		var high = score >= 85 && (idHit || (!string.IsNullOrEmpty(nt) && pathBlob.Contains(nt, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(nc) && pathBlob.Contains(nc, StringComparison.OrdinalIgnoreCase)));
		if (high)
			return "high";
		if (score >= 55)
			return "medium";
		return "low";
	}
}
