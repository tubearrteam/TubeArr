using System.IO;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.Data;
using System.Linq;
using TubeArr.Backend.Media;

namespace TubeArr.Backend;

/// <summary>
/// Scans monitored channel folders for media files not linked (or with stale paths) in <see cref="VideoFileEntity"/>,
/// and links them to videos using YouTube video ids from filenames or paths. Channel scope comes from each channel's configured folder roots.
/// </summary>
internal static class UnmappedVideoFileMappingRunner
{
	static readonly HashSet<string> MediaExts = new(StringComparer.OrdinalIgnoreCase)
	{
		".mp4", ".mkv", ".webm", ".avi", ".mov", ".m4v", ".flv", ".wmv", ".mpg", ".mpeg",
		".m4a", ".mp3", ".aac", ".opus", ".ogg", ".wav", ".flac"
	};

	static readonly Regex BracketSegment = new(@"\[(?<id>[^\]]+)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
	static readonly Regex ElevenCharToken = new(@"(?<![A-Za-z0-9_-])([A-Za-z0-9_-]{11})(?![A-Za-z0-9_-])", RegexOptions.Compiled);
	static readonly Regex NonAlnum = new(@"[^a-z0-9]+", RegexOptions.Compiled);

	enum IdMatchStrength
	{
		StrongBracketed,
		WeakToken
	}

	public static async Task<(int Mapped, string Message)> RunAsync(
		TubeArrDbContext db,
		ILogger logger,
		CancellationToken ct,
		Func<string, Task>? reportProgress = null)
	{
		var naming = await db.NamingConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct)
			?? new NamingConfigEntity { Id = 1 };
		var rootFolders = await db.RootFolders.AsNoTracking().ToListAsync(ct);
		if (rootFolders.Count == 0)
			return (0, "No root folders configured; nothing to scan.");

		var channels = await db.Channels.AsNoTracking()
			.Where(c => c.Monitored)
			.OrderBy(c => c.Id)
			.ToListAsync(ct);

		if (channels.Count == 0)
			return (0, "No monitored channels; nothing to scan.");

		logger.LogInformation(
			"MapUnmappedVideoFiles: scanning {ChannelCount} monitored channel(s) for media on disk.",
			channels.Count);
		if (reportProgress is not null)
			await reportProgress($"Mapping: {channels.Count} channel(s) to scan…");

		var totalMapped = 0;
		var totalUpdated = 0;

		var ffmpegConfig = await db.FFmpegConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		var ffmpegPath = ffmpegConfig is not null && ffmpegConfig.Enabled && !string.IsNullOrWhiteSpace(ffmpegConfig.ExecutablePath)
			? ffmpegConfig.ExecutablePath
			: null;

		for (var ci = 0; ci < channels.Count; ci++)
		{
			var channel = channels[ci];
			if (reportProgress is not null)
				await reportProgress($"Mapping files: channel {ci + 1}/{channels.Count} — {channel.Title}…");

			var scanRoots = BuildScanRoots(channel, naming, rootFolders);
			if (scanRoots.Count == 0)
				continue;

			var channelVideos = await db.Videos.AsNoTracking()
				.Where(v => v.ChannelId == channel.Id)
				.Select(v => new { v.Id, v.ChannelId, v.YoutubeVideoId, v.Title, v.Runtime })
				.ToListAsync(ct);

			var primaryPlaylistByVideoId = await ChannelDtoMapper.LoadPrimaryPlaylistIdByVideoIdsForChannelAsync(
				db,
				channel.Id,
				channelVideos.Select(v => v.Id).ToList(),
				ct);

			var videoByYoutubeId = channelVideos
				.Where(v => !string.IsNullOrWhiteSpace(v.YoutubeVideoId))
				.ToDictionary(v => v.YoutubeVideoId!, StringComparer.OrdinalIgnoreCase);

			if (videoByYoutubeId.Count == 0)
				continue;

			var validYoutubeIds = new HashSet<string>(videoByYoutubeId.Keys, StringComparer.OrdinalIgnoreCase);

			var existingByVideoId = await db.VideoFiles
				.Where(vf => vf.ChannelId == channel.Id)
				.ToDictionaryAsync(vf => vf.VideoId, ct);

			var foundByVideoId = new Dictionary<int, (string Path, string RelativePath, long Size, int ChannelId, int? PlaylistId)>();

			foreach (var root in scanRoots.Distinct(StringComparer.OrdinalIgnoreCase))
			{
				if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
					continue;

				try
				{
					var scannedInTree = 0;
					foreach (var filePath in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
					{
						scannedInTree++;
						if (reportProgress is not null && scannedInTree % 5000 == 0)
							await reportProgress($"Mapping files: {channel.Title} — scanned {scannedInTree} media file(s) in folder tree…");

						var ext = Path.GetExtension(filePath);
						if (!MediaExts.Contains(ext))
							continue;

						string? ytId = null;
						IdMatchStrength strength = IdMatchStrength.WeakToken;
						var hasId = TryResolveYoutubeVideoId(filePath, validYoutubeIds, channel.YoutubeChannelId, out var resolvedYtId, out strength);
						if (hasId)
							ytId = resolvedYtId;

						var channelOkForTitleMatch = ChannelLooksLikePath(channel.Title, channel.YoutubeChannelId, filePath);

						dynamic? video = null;
						if (ytId is not null && videoByYoutubeId.TryGetValue(ytId, out var v0))
						{
							video = v0;
						}
						else if (ytId is null)
						{
							// No id at all: fall back to title match.
							if (!channelOkForTitleMatch)
								continue;

							if (!TryResolveVideoByTitle(channelVideos, filePath, out var vByTitle))
								continue;

							video = vByTitle;
							ytId = vByTitle.YoutubeVideoId;
							strength = IdMatchStrength.WeakToken;
						}

						if (video is null)
							continue;

						if (strength == IdMatchStrength.WeakToken)
						{
							// Weak id-shaped tokens are only trusted when the path looks like the channel,
							// and either the title matches reasonably or duration matches.
							var channelOk = channelOkForTitleMatch;
							var titleOk = TitleLooksLikeFileName((string)video.Title, filePath);

							var durationOk = false;
							if (!titleOk && ffmpegPath is not null && (int)video.Runtime > 0)
							{
								var probe = FfProbeMediaProbe.Probe(filePath, ffmpegPath);
								if (probe is not null && probe.DurationSeconds > 0)
								{
									var diff = Math.Abs(probe.DurationSeconds - (int)video.Runtime);
									var tol = Math.Max(15, (int)Math.Round(((int)video.Runtime) * 0.02));
									durationOk = diff <= tol;
								}
							}

							if (!channelOk || (!titleOk && !durationOk))
							{
								logger.LogDebug(
									"MapUnmappedVideoFiles: skipping weak token id match for channelId={ChannelId} videoId={VideoId} ytId={YoutubeVideoId} file={Path}",
									channel.Id,
									(int)video.Id,
									ytId,
									filePath);
								continue;
							}
						}

						var fi = new FileInfo(filePath);
						if (!fi.Exists)
							continue;

						var relativePath = Path.GetRelativePath(root, filePath);
						foundByVideoId[(int)video.Id] = (filePath, relativePath, fi.Length, (int)video.ChannelId, primaryPlaylistByVideoId.GetValueOrDefault((int)video.Id));
					}
				}
				catch (Exception ex)
				{
					logger.LogWarning(ex, "Unmapped file scan failed for channelId={ChannelId} root={Root}", channel.Id, root);
				}
			}

			foreach (var kv in foundByVideoId)
			{
				var videoId = kv.Key;
				var found = kv.Value;

				if (!existingByVideoId.TryGetValue(videoId, out var row))
				{
					db.VideoFiles.Add(new VideoFileEntity
					{
						VideoId = videoId,
						ChannelId = found.ChannelId,
						PlaylistId = found.PlaylistId,
						Path = found.Path,
						RelativePath = found.RelativePath,
						Size = found.Size,
						DateAdded = DateTimeOffset.UtcNow
					});
					totalMapped++;
					continue;
				}

				var pathChanged = !string.Equals(row.Path, found.Path, StringComparison.OrdinalIgnoreCase);
				var sizeChanged = row.Size != found.Size;
				if (pathChanged || sizeChanged || row.PlaylistId != found.PlaylistId)
				{
					row.Path = found.Path;
					row.RelativePath = found.RelativePath;
					row.Size = found.Size;
					row.ChannelId = found.ChannelId;
					row.PlaylistId = found.PlaylistId;
					row.DateAdded = DateTimeOffset.UtcNow;
					totalUpdated++;
				}
			}
		}

		if (totalMapped == 0 && totalUpdated == 0)
			return (0, "Scan complete; no new or updated file mappings.");

		await db.SaveChangesAsync(ct);

		var msg = $"Mapped {totalMapped} new file(s)";
		if (totalUpdated > 0)
			msg += $", updated {totalUpdated} existing row(s)";
		msg += ".";

		return (totalMapped + totalUpdated, msg);
	}

	static List<string> BuildScanRoots(ChannelEntity channel, NamingConfigEntity naming, IReadOnlyList<RootFolderEntity> rootFolders)
	{
		var scanRoots = new List<string>();
		if (!string.IsNullOrWhiteSpace(channel.Path))
		{
			foreach (var root in rootFolders)
			{
				var rootPath = (root.Path ?? "").Trim();
				if (string.IsNullOrWhiteSpace(rootPath))
					continue;

				var path = Path.IsPathRooted(channel.Path)
					? channel.Path
					: Path.Combine(rootPath, channel.Path);

				if (!string.IsNullOrWhiteSpace(path))
					scanRoots.Add(path);
			}
		}
		else
		{
			var dummyVideo = new VideoEntity { Title = "", YoutubeVideoId = "", UploadDateUtc = DateTimeOffset.UtcNow };
			var context = new VideoFileNaming.NamingContext(Channel: channel, Video: dummyVideo, Playlist: null, PlaylistIndex: null, QualityFull: null, Resolution: null, Extension: null);
			var channelFolderName = VideoFileNaming.BuildFolderName(naming.ChannelFolderFormat, context, naming);
			if (string.IsNullOrWhiteSpace(channelFolderName))
				return scanRoots;

			foreach (var root in rootFolders)
			{
				var rootPath = (root.Path ?? "").Trim();
				if (!string.IsNullOrWhiteSpace(rootPath))
					scanRoots.Add(Path.Combine(rootPath, channelFolderName));
			}
		}

		return scanRoots;
	}

	/// <summary>
	/// Prefer bracketed ids in the filename, then bracketed ids anywhere in the path, then 11-character tokens in the path
	/// that match a video id in this channel. When multiple 11-character tokens match, picks the leftmost; if the path
	/// contains the channel's YouTube channel id, requires every matching token to be consistent or uses the first after the channel id segment.
	/// </summary>
	static bool TryResolveYoutubeVideoId(
		string filePath,
		HashSet<string> validYoutubeIds,
		string channelYoutubeId,
		out string youtubeVideoId,
		out IdMatchStrength strength)
	{
		youtubeVideoId = "";
		strength = IdMatchStrength.WeakToken;

		var fileName = Path.GetFileName(filePath);
		if (!string.IsNullOrEmpty(fileName))
		{
			foreach (Match m in BracketSegment.Matches(fileName))
			{
				var inner = m.Groups["id"].Value.Trim();
				if (validYoutubeIds.Contains(inner))
				{
					youtubeVideoId = inner;
					strength = IdMatchStrength.StrongBracketed;
					return true;
				}
			}
		}

		foreach (Match m in BracketSegment.Matches(filePath))
		{
			var inner = m.Groups["id"].Value.Trim();
			if (validYoutubeIds.Contains(inner))
			{
				youtubeVideoId = inner;
				strength = IdMatchStrength.StrongBracketed;
				return true;
			}
		}

		var pathMatches = new List<string>();
		foreach (Match m in ElevenCharToken.Matches(filePath))
		{
			var token = m.Groups[1].Value;
			if (validYoutubeIds.Contains(token))
				pathMatches.Add(token);
		}

		if (pathMatches.Count == 0)
			return false;

		if (pathMatches.Count == 1)
		{
			youtubeVideoId = pathMatches[0];
			strength = IdMatchStrength.WeakToken;
			return true;
		}

		// Ambiguous: multiple id-shaped tokens match library videos — narrow using channel id in path if present.
		if (!string.IsNullOrWhiteSpace(channelYoutubeId) &&
		    filePath.IndexOf(channelYoutubeId, StringComparison.OrdinalIgnoreCase) >= 0)
		{
			var afterChannel = filePath.IndexOf(channelYoutubeId, StringComparison.OrdinalIgnoreCase) + channelYoutubeId.Length;
			var tail = filePath.Length > afterChannel ? filePath[afterChannel..] : "";
			foreach (Match m in ElevenCharToken.Matches(tail))
			{
				var token = m.Groups[1].Value;
				if (validYoutubeIds.Contains(token))
				{
					youtubeVideoId = token;
					strength = IdMatchStrength.WeakToken;
					return true;
				}
			}
		}

		youtubeVideoId = pathMatches[0];
		strength = IdMatchStrength.WeakToken;
		return true;
	}

	internal static bool TitleLooksLikeFileName(string title, string filePath)
	{
		var t = (title ?? "").Trim();
		if (string.IsNullOrWhiteSpace(t))
			return false;

		var fileName = Path.GetFileNameWithoutExtension(filePath) ?? "";
		if (string.IsNullOrWhiteSpace(fileName))
			return false;

		// Remove bracketed segments (ids/quality tags) for title comparisons.
		fileName = BracketSegment.Replace(fileName, " ");

		static IEnumerable<string> Words(string s) =>
			Regex.Split((s ?? "").ToLowerInvariant(), @"[^a-z0-9]+")
				.Select(w => w.Trim())
				.Where(w => w.Length >= 3);

		var titleWords = Words(t).Distinct().ToList();
		if (titleWords.Count == 0)
			return false;

		var fileWords = Words(fileName).ToHashSet();
		var hit = 0;
		foreach (var w in titleWords)
		{
			if (fileWords.Contains(w))
				hit++;
		}

		// Require at least half the (significant) title words to be present in the filename.
		return hit >= Math.Max(1, (int)Math.Ceiling(titleWords.Count * 0.5));
	}

	internal static bool TryResolveVideoByTitle(
		IReadOnlyList<dynamic> channelVideos,
		string filePath,
		out dynamic video)
	{
		video = default!;

		var fileName = Path.GetFileNameWithoutExtension(filePath) ?? "";
		if (string.IsNullOrWhiteSpace(fileName))
			return false;

		// Strip bracket segments (ids/quality tags), normalize to compare.
		fileName = BracketSegment.Replace(fileName, " ");
		var fileNorm = NormalizeForContains(fileName);
		if (string.IsNullOrWhiteSpace(fileNorm))
			return false;

		// "filename includes channel video title" => pretty sure.
		dynamic? bestExact = null;
		var bestExactLen = 0;
		for (var i = 0; i < channelVideos.Count; i++)
		{
			var v = channelVideos[i];
			var title = (string)(v.Title ?? "");
			var titleNorm = NormalizeForContains(title);
			if (string.IsNullOrWhiteSpace(titleNorm) || titleNorm.Length < 6)
				continue;
			if (fileNorm.Contains(titleNorm, StringComparison.Ordinal))
			{
				if (titleNorm.Length > bestExactLen)
				{
					bestExact = v;
					bestExactLen = titleNorm.Length;
				}
			}
		}

		if (bestExact is not null)
		{
			video = bestExact;
			return true;
		}

		// "filename includes video title within 90%" => pretty positive, but require a clear winner.
		static IEnumerable<string> Words(string s) =>
			Regex.Split((s ?? "").ToLowerInvariant(), @"[^a-z0-9]+")
				.Select(w => w.Trim())
				.Where(w => w.Length >= 3);

		var fileWords = Words(fileName).ToHashSet();
		if (fileWords.Count == 0)
			return false;

		dynamic? best = null;
		double bestScore = 0;
		double second = 0;

		for (var i = 0; i < channelVideos.Count; i++)
		{
			var v = channelVideos[i];
			var title = (string)(v.Title ?? "");
			if (string.IsNullOrWhiteSpace(title))
				continue;

			var titleWords = Words(title).Distinct().ToList();
			if (titleWords.Count == 0)
				continue;

			var hit = 0;
			foreach (var w in titleWords)
			{
				if (fileWords.Contains(w))
					hit++;
			}

			var score = (double)hit / titleWords.Count;
			if (score > bestScore)
			{
				second = bestScore;
				bestScore = score;
				best = v;
			}
			else if (score > second)
			{
				second = score;
			}
		}

		if (best is null)
			return false;

		if (bestScore < 0.90)
			return false;

		// Avoid collisions: if runner-up is close, do not guess.
		if (second >= 0.90 && (bestScore - second) < 0.05)
			return false;

		video = best;
		return true;
	}

	static string NormalizeForContains(string s)
	{
		var lower = (s ?? "").Trim().ToLowerInvariant();
		if (string.IsNullOrWhiteSpace(lower))
			return "";
		// Use alphanumeric-only normalization so substring checks are resilient to punctuation/spacing differences.
		return NonAlnum.Replace(lower, "");
	}

	internal static bool ChannelLooksLikePath(string channelTitle, string channelYoutubeId, string filePath)
	{
		if (!string.IsNullOrWhiteSpace(channelYoutubeId) &&
		    filePath.IndexOf(channelYoutubeId, StringComparison.OrdinalIgnoreCase) >= 0)
			return true;

		var t = (channelTitle ?? "").Trim();
		if (string.IsNullOrWhiteSpace(t))
			return true;

		static IEnumerable<string> Words(string s) =>
			Regex.Split((s ?? "").ToLowerInvariant(), @"[^a-z0-9]+")
				.Select(w => w.Trim())
				.Where(w => w.Length >= 3);

		var titleWords = Words(t).Distinct().ToList();
		if (titleWords.Count == 0)
			return true;

		var pathLower = (filePath ?? "").ToLowerInvariant();
		var hit = 0;
		foreach (var w in titleWords)
		{
			if (pathLower.Contains(w, StringComparison.Ordinal))
				hit++;
		}

		// Require at least one significant channel word somewhere in the path.
		return hit >= 1;
	}
}
