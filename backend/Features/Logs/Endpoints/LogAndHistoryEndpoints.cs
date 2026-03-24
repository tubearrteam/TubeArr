using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Data;
using TubeArr.Backend.Media;

namespace TubeArr.Backend;

internal static class LogAndHistoryEndpoints
{
	internal static void Map(RouteGroupBuilder api, Lazy<IReadOnlyDictionary<string, string>> englishStringsLazy)
	{
		api.MapGet("/log", async (HttpContext httpContext, TubeArrDbContext db) =>
		{
			var strings = englishStringsLazy.Value;

			static string Localized(IReadOnlyDictionary<string, string> dict, string key, string fallback) =>
				dict.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

			var page = Math.Max(1, int.TryParse(httpContext.Request.Query["page"].FirstOrDefault(), out var p) ? p : 1);
			var pageSize = Math.Clamp(int.TryParse(httpContext.Request.Query["pageSize"].FirstOrDefault(), out var ps) ? ps : 50, 1, 200);
			var sortKey = (httpContext.Request.Query["sortKey"].FirstOrDefault() ?? "time").Trim();
			var sortDirectionRaw = (httpContext.Request.Query["sortDirection"].FirstOrDefault() ?? "descending").Trim();
			var sortDescending = sortDirectionRaw.Equals("descending", StringComparison.OrdinalIgnoreCase) ||
				sortDirectionRaw.Equals("desc", StringComparison.OrdinalIgnoreCase);
			var levelFilter = (httpContext.Request.Query["level"].FirstOrDefault() ?? string.Empty).Trim().ToLowerInvariant();

			var rows = await db.DownloadHistory.AsNoTracking().ToListAsync();
			if (!string.IsNullOrWhiteSpace(levelFilter))
			{
				static string MapEventToLevel(int eventType) => eventType switch
				{
					4 => "error",
					5 => "warn",
					7 => "warn",
					_ => "info"
				};
				rows = rows.Where(h => string.Equals(MapEventToLevel(h.EventType), levelFilter, StringComparison.OrdinalIgnoreCase)).ToList();
			}

			var channelIds = rows.Select(h => h.ChannelId).Distinct().ToList();
			var videoIds = rows.Select(h => h.VideoId).Distinct().ToList();
			var channelsById = channelIds.Count == 0
				? new Dictionary<int, ChannelEntity>()
				: await db.Channels.AsNoTracking().Where(c => channelIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
			var videosById = videoIds.Count == 0
				? new Dictionary<int, VideoEntity>()
				: await db.Videos.AsNoTracking().Where(v => videoIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id);
			var playlistRows = await db.Playlists.AsNoTracking()
				.Where(pl => channelIds.Contains(pl.ChannelId))
				.OrderBy(pl => pl.ChannelId)
				.ThenBy(pl => pl.Id)
				.ToListAsync();
			var playlistNumberByPlaylistId = new Dictionary<int, int>();
			foreach (var group in playlistRows.GroupBy(pl => pl.ChannelId))
			{
				var playlistNumber = 2;
				foreach (var playlist in group)
					playlistNumberByPlaylistId[playlist.Id] = playlistNumber++;
			}

			static string EventLevel(int eventType) => eventType switch
			{
				4 => "error",
				5 => "warn",
				7 => "warn",
				_ => "info"
			};

			string EventText(int eventType) => eventType switch
			{
				1 => Localized(strings, "Grabbed", "Grabbed"),
				3 => Localized(strings, "Imported", "Imported"),
				4 => Localized(strings, "Failed", "Failed"),
				5 => Localized(strings, "Deleted", "Deleted"),
				6 => Localized(strings, "Renamed", "Renamed"),
				7 => Localized(strings, "Ignored", "Ignored"),
				_ => Localized(strings, "History", "History")
			};

			string BuildLogger(int channelId, int? playlistId, int videoId)
			{
				var channelTitle = channelsById.TryGetValue(channelId, out var channel) ? channel.Title : $"Channel {channelId}";
				var videoTitle = videosById.TryGetValue(videoId, out var video) ? video.Title : $"Video {videoId}";
				if (playlistId.HasValue && playlistNumberByPlaylistId.TryGetValue(playlistId.Value, out var playlistNumber))
					return $"{channelTitle} / PL{playlistNumber:00} / {videoTitle}";
				return $"{channelTitle} / {videoTitle}";
			}

			string BuildMessage(DownloadHistoryEntity h)
			{
				var channelTitle = channelsById.TryGetValue(h.ChannelId, out var channel) ? channel.Title : $"Channel {h.ChannelId}";
				var videoTitle = videosById.TryGetValue(h.VideoId, out var video) ? video.Title : h.SourceTitle;
				var eventText = EventText(h.EventType);
				var playlistText = string.Empty;
				if (h.PlaylistId.HasValue && playlistNumberByPlaylistId.TryGetValue(h.PlaylistId.Value, out var playlistNumber))
					playlistText = $" (PL{playlistNumber:00})";

				return $"{eventText}: \"{videoTitle}\" {Localized(strings, "Channels", "Channels")}: \"{channelTitle}\"{playlistText}";
			}

			var downloadEntries = rows.Select(h => (
				Id: h.Id,
				Level: EventLevel(h.EventType),
				Time: h.Date,
				Logger: BuildLogger(h.ChannelId, h.PlaylistId, h.VideoId),
				Message: BuildMessage(h),
				Exception: string.IsNullOrWhiteSpace(h.Message)
					? (!string.IsNullOrWhiteSpace(h.OutputPath) ? h.OutputPath : null)
					: h.Message
			)).ToList();

			var combined = new List<(int Id, string Level, DateTime Time, string Logger, string Message, string? Exception)>(downloadEntries.Count + 64);
			combined.AddRange(downloadEntries);

			if (string.IsNullOrWhiteSpace(levelFilter) || levelFilter == "info")
			{
				var taskRuns = await db.ScheduledTaskRunHistory.AsNoTracking().ToListAsync();
				var taskResultFmt = Localized(strings, "ScheduledTaskFinishedResult", "Finished in {0}.");
				var taskResultWithDurationFmt = Localized(strings, "ScheduledTaskResultWithDuration", "{0} ({1})");
				foreach (var run in taskRuns)
				{
					var displayName = ScheduledTaskCatalog.GetDisplayName(run.TaskName);
					var ticks = run.DurationTicks < 0 ? 0 : run.DurationTicks;
					var dur = CommandRecordFactory.FormatCommandDuration(TimeSpan.FromTicks(ticks));
					var summary = string.IsNullOrWhiteSpace(run.ResultMessage)
						? string.Format(taskResultFmt, dur)
						: string.Format(taskResultWithDurationFmt, run.ResultMessage.Trim(), dur);
					combined.Add((
						Id: -run.Id,
						Level: "info",
						Time: run.CompletedAt.UtcDateTime,
						Logger: displayName,
						Message: summary,
						Exception: (string?)null
					));
				}
			}

			var ordered = sortKey.ToLowerInvariant() switch
			{
				"level" => sortDescending ? combined.OrderByDescending(x => x.Level).ThenByDescending(x => x.Time) : combined.OrderBy(x => x.Level).ThenBy(x => x.Time),
				"logger" => sortDescending ? combined.OrderByDescending(x => x.Logger).ThenByDescending(x => x.Time) : combined.OrderBy(x => x.Logger).ThenBy(x => x.Time),
				"message" => sortDescending ? combined.OrderByDescending(x => x.Message).ThenByDescending(x => x.Time) : combined.OrderBy(x => x.Message).ThenBy(x => x.Time),
				_ => sortDescending ? combined.OrderByDescending(x => x.Time) : combined.OrderBy(x => x.Time)
			};

			var totalRecords = combined.Count;
			var records = ordered
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.Select(x => new
				{
					id = x.Id,
					level = x.Level,
					time = x.Time,
					logger = x.Logger,
					message = x.Message,
					exception = x.Exception
				})
				.ToArray();

			return Results.Json(new { records, totalRecords, pageSize });
		});

		api.MapGet("/history", async (HttpContext httpContext, TubeArrDbContext db) =>
		{
			var page = Math.Max(1, int.TryParse(httpContext.Request.Query["page"].FirstOrDefault(), out var p) ? p : 1);
			var pageSize = Math.Clamp(int.TryParse(httpContext.Request.Query["pageSize"].FirstOrDefault(), out var ps) ? ps : 20, 1, 1000);
			var sortKey = (httpContext.Request.Query["sortKey"].FirstOrDefault() ?? "date").Trim();
			var sortDirectionRaw = (httpContext.Request.Query["sortDirection"].FirstOrDefault() ?? "descending").Trim();
			var sortDescending = sortDirectionRaw.Equals("descending", StringComparison.OrdinalIgnoreCase) ||
				sortDirectionRaw.Equals("desc", StringComparison.OrdinalIgnoreCase);

			int? eventTypeFilter = null;
			if (int.TryParse(httpContext.Request.Query["eventType"].FirstOrDefault(), out var eventTypeValue))
				eventTypeFilter = eventTypeValue;

			int? videoIdFilter = null;
			if (int.TryParse(httpContext.Request.Query["videoId"].FirstOrDefault(), out var videoIdValue))
				videoIdFilter = videoIdValue;

			var channelIdFilterSet = new HashSet<int>();
			foreach (var raw in httpContext.Request.Query["channelIds"])
			{
				if (string.IsNullOrWhiteSpace(raw))
					continue;

				foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
				{
					if (int.TryParse(part, out var id) && id > 0)
						channelIdFilterSet.Add(id);
				}
			}

			var historyQuery = db.DownloadHistory.AsNoTracking().AsQueryable();
			if (eventTypeFilter.HasValue)
				historyQuery = historyQuery.Where(h => h.EventType == eventTypeFilter.Value);
			if (videoIdFilter.HasValue)
				historyQuery = historyQuery.Where(h => h.VideoId == videoIdFilter.Value);
			if (channelIdFilterSet.Count > 0)
				historyQuery = historyQuery.Where(h => channelIdFilterSet.Contains(h.ChannelId));

			var historyRows = await historyQuery.ToListAsync();
			var channelIds = historyRows.Select(h => h.ChannelId).Distinct().ToList();
			var videoIds = historyRows.Select(h => h.VideoId).Distinct().ToList();
			var channels = channelIds.Count == 0
				? new Dictionary<int, ChannelEntity>()
				: await db.Channels.AsNoTracking().Where(c => channelIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
			var videos = videoIds.Count == 0
				? new Dictionary<int, VideoEntity>()
				: await db.Videos.AsNoTracking().Where(v => videoIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id);

			IEnumerable<DownloadHistoryEntity> ordered = sortKey.ToLowerInvariant() switch
			{
				"eventtype" => sortDescending ? historyRows.OrderByDescending(h => h.EventType).ThenByDescending(h => h.Date) : historyRows.OrderBy(h => h.EventType).ThenBy(h => h.Date),
				"channel.sorttitle" or "channels" => sortDescending
					? historyRows.OrderByDescending(h => channels.TryGetValue(h.ChannelId, out var c) ? c.Title : string.Empty).ThenByDescending(h => h.Date)
					: historyRows.OrderBy(h => channels.TryGetValue(h.ChannelId, out var c) ? c.Title : string.Empty).ThenBy(h => h.Date),
				"videos.title" or "video" => sortDescending
					? historyRows.OrderByDescending(h => videos.TryGetValue(h.VideoId, out var v) ? v.Title : h.SourceTitle).ThenByDescending(h => h.Date)
					: historyRows.OrderBy(h => videos.TryGetValue(h.VideoId, out var v) ? v.Title : h.SourceTitle).ThenBy(h => h.Date),
				_ => sortDescending ? historyRows.OrderByDescending(h => h.Date) : historyRows.OrderBy(h => h.Date)
			};

			var totalRecords = historyRows.Count;
			var pageRows = ordered
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.ToList();

			var ffmpegRow = await db.FFmpegConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync();
			var ffmpegPath = ffmpegRow?.ExecutablePath;

			var pageVideoIds = pageRows.Select(h => h.VideoId).Distinct().ToList();
			var latestFilePathByVideoId = pageVideoIds.Count == 0
				? new Dictionary<int, string?>()
				: await db.VideoFiles.AsNoTracking()
					.Where(vf => pageVideoIds.Contains(vf.VideoId))
					.GroupBy(vf => vf.VideoId)
					.ToDictionaryAsync(
						g => g.Key,
						g => g.OrderByDescending(x => x.DateAdded).Select(x => x.Path).FirstOrDefault());

			static string? ResolveExistingMediaPath(DownloadHistoryEntity h, IReadOnlyDictionary<int, string?> pathsByVideoId)
			{
				if (!string.IsNullOrWhiteSpace(h.OutputPath) && File.Exists(h.OutputPath))
					return h.OutputPath;
				if (pathsByVideoId.TryGetValue(h.VideoId, out var tracked) &&
				    !string.IsNullOrWhiteSpace(tracked) && File.Exists(tracked))
					return tracked;
				return null;
			}

			var probeByPath = new ConcurrentDictionary<string, FfProbeVideoSummary.Result?>(StringComparer.OrdinalIgnoreCase);
			if (pageRows.Count > 0 && !string.IsNullOrWhiteSpace(ffmpegPath))
			{
				var pathsToProbe = pageRows
					.Select(h => ResolveExistingMediaPath(h, latestFilePathByVideoId))
					.Where(p => !string.IsNullOrWhiteSpace(p))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();

				Parallel.ForEach(
					pathsToProbe,
					new ParallelOptions { MaxDegreeOfParallelism = 4 },
					path =>
					{
						if (string.IsNullOrWhiteSpace(path))
							return;
						probeByPath[path] = FfProbeVideoSummary.Probe(path!, ffmpegPath);
					});
			}

			object BuildQualityPayload(FfProbeVideoSummary.Result? probe)
			{
				var label = probe is null
					? string.Empty
					: FfProbeVideoSummary.BuildQualityLabel(probe.Height, probe.FrameRate);
				if (string.IsNullOrWhiteSpace(label))
					label = "-";

				var resHeight = probe?.Height ?? 0;
				var source = probe is null ? "unknown" : "web";
				return new
				{
					quality = new { id = 0, name = label, resolution = resHeight, source },
					revision = new { version = 1, real = 0, isRepack = false }
				};
			}

			object[] BuildFormatsPayload(FfProbeVideoSummary.Result? probe, string mediaPath)
			{
				var ext = Path.GetExtension(mediaPath);
				var container = string.IsNullOrWhiteSpace(ext) ? null : ext.TrimStart('.').ToLowerInvariant();
				var fmt = FfProbeVideoSummary.FormatCodecContainerLabel(probe?.VideoCodec, container);
				if (string.IsNullOrWhiteSpace(fmt))
					fmt = "-";
				return new object[]
				{
					new { id = 0, name = fmt, includeCustomFormatWhenRenaming = false }
				};
			}

			var records = pageRows.Select(h =>
				{
					var hasChannel = channels.TryGetValue(h.ChannelId, out var channel);
					var hasVideo = videos.TryGetValue(h.VideoId, out var video);
					var resolvedTitle = hasVideo ? video!.Title : h.SourceTitle;
					var details = !string.IsNullOrWhiteSpace(h.Message)
						? h.Message
						: (!string.IsNullOrWhiteSpace(h.OutputPath) ? h.OutputPath : "-");

					var mediaPath = ResolveExistingMediaPath(h, latestFilePathByVideoId);
					FfProbeVideoSummary.Result? probe = null;
					if (!string.IsNullOrWhiteSpace(mediaPath))
						probeByPath.TryGetValue(mediaPath, out probe);

					object qualityPayload = mediaPath is null
						? new
						{
							quality = new { id = 0, name = "-", resolution = 0, source = "unknown" },
							revision = new { version = 1, real = 0, isRepack = false }
						}
						: BuildQualityPayload(probe);

					object[] formatsPayload = mediaPath is null
						? new object[] { new { id = 0, name = "-", includeCustomFormatWhenRenaming = false } }
						: BuildFormatsPayload(probe, mediaPath);

					return new
					{
						id = h.Id,
						channelId = h.ChannelId,
						videoId = h.VideoId,
						eventType = h.EventType,
						sourceTitle = string.IsNullOrWhiteSpace(h.SourceTitle) ? resolvedTitle : h.SourceTitle,
						quality = qualityPayload,
						customFormats = formatsPayload,
						languages = Array.Empty<object>(),
						date = h.Date,
						downloadClient = "yt-dlp",
						indexer = "-",
						releaseGroup = "-",
						customFormatScore = 0,
						details,
						channel = hasChannel ? new { id = channel!.Id, title = channel.Title, sortTitle = channel.Title } : new { id = h.ChannelId, title = "Unknown Channel", sortTitle = "Unknown Channel" },
						video = new { id = h.VideoId, title = resolvedTitle },
						videos = new { title = resolvedTitle, airDateUtc = hasVideo ? video!.UploadDateUtc : (DateTimeOffset?)null }
					};
				})
				.ToList();

			return Results.Json(new { records, totalRecords, pageSize });
		});
	}
}
