using System.IO;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.Data;
using System.Linq;

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
				.Select(v => new { v.Id, v.ChannelId, v.YoutubeVideoId })
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

						if (!TryResolveYoutubeVideoId(filePath, validYoutubeIds, channel.YoutubeChannelId, out var ytId))
							continue;

						if (!videoByYoutubeId.TryGetValue(ytId, out var video))
							continue;

						var fi = new FileInfo(filePath);
						if (!fi.Exists)
							continue;

						var relativePath = Path.GetRelativePath(root, filePath);
						foundByVideoId[video.Id] = (filePath, relativePath, fi.Length, video.ChannelId, primaryPlaylistByVideoId.GetValueOrDefault(video.Id));
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
		out string youtubeVideoId)
	{
		youtubeVideoId = "";

		var fileName = Path.GetFileName(filePath);
		if (!string.IsNullOrEmpty(fileName))
		{
			foreach (Match m in BracketSegment.Matches(fileName))
			{
				var inner = m.Groups["id"].Value.Trim();
				if (validYoutubeIds.Contains(inner))
				{
					youtubeVideoId = inner;
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
					return true;
				}
			}
		}

		youtubeVideoId = pathMatches[0];
		return true;
	}
}
