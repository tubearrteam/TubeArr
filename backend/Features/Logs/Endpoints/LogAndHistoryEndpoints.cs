using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Data;

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

			var mapped = rows.Select(h => new
			{
				id = h.Id,
				level = EventLevel(h.EventType),
				time = h.Date,
				logger = BuildLogger(h.ChannelId, h.PlaylistId, h.VideoId),
				message = BuildMessage(h),
				exception = string.IsNullOrWhiteSpace(h.Message)
					? (!string.IsNullOrWhiteSpace(h.OutputPath) ? h.OutputPath : null)
					: h.Message
			}).ToList();

			var ordered = sortKey.ToLowerInvariant() switch
			{
				"level" => sortDescending ? mapped.OrderByDescending(x => x.level).ThenByDescending(x => x.time) : mapped.OrderBy(x => x.level).ThenBy(x => x.time),
				"logger" => sortDescending ? mapped.OrderByDescending(x => x.logger).ThenByDescending(x => x.time) : mapped.OrderBy(x => x.logger).ThenBy(x => x.time),
				"message" => sortDescending ? mapped.OrderByDescending(x => x.message).ThenByDescending(x => x.time) : mapped.OrderBy(x => x.message).ThenBy(x => x.time),
				_ => sortDescending ? mapped.OrderByDescending(x => x.time) : mapped.OrderBy(x => x.time)
			};

			var totalRecords = mapped.Count;
			var records = ordered
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
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
			var records = ordered
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.Select(h =>
				{
					var hasChannel = channels.TryGetValue(h.ChannelId, out var channel);
					var hasVideo = videos.TryGetValue(h.VideoId, out var video);
					var resolvedTitle = hasVideo ? video!.Title : h.SourceTitle;
					var details = !string.IsNullOrWhiteSpace(h.Message)
						? h.Message
						: (!string.IsNullOrWhiteSpace(h.OutputPath) ? h.OutputPath : "-");

					return new
					{
						id = h.Id,
						channelId = h.ChannelId,
						videoId = h.VideoId,
						eventType = h.EventType,
						sourceTitle = string.IsNullOrWhiteSpace(h.SourceTitle) ? resolvedTitle : h.SourceTitle,
						quality = "-",
						customFormats = "-",
						languages = "-",
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
