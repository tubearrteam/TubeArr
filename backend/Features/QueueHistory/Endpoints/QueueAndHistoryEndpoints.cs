using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;
using TubeArr.Backend.Realtime;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TubeArr.Backend;

public static class QueueAndHistoryEndpoints
{
	public static void Map(RouteGroupBuilder api)
	{
		api.MapPost("/history/failed/{id:int}", async (int id, TubeArrDbContext db, IRealtimeEventBroadcaster realtime) =>
		{
			var row = await db.DownloadHistory.FirstOrDefaultAsync(h => h.Id == id);
			if (row is null)
				return Results.NotFound();

			row.EventType = 4; // failed
			if (string.IsNullOrWhiteSpace(row.Message))
				row.Message = "Marked as failed manually.";
			row.Date = DateTime.UtcNow;
			await db.SaveChangesAsync();

			await RealtimeBroadcastHelper.BroadcastLiveQueueAndHistoryAsync(realtime);
			return Results.Ok();
		});

		api.MapGet("/history/channel", async (HttpContext httpContext, int channelId, int? playlistNumber, TubeArrDbContext db, CancellationToken ct) =>
		{
			if (channelId <= 0)
				return Results.Json(Array.Empty<object>());

			int? playlistId = null;
			if (playlistNumber.HasValue && playlistNumber.Value > 1)
			{
				var orderedPlaylists = await ChannelDtoMapper.LoadPlaylistsOrderedByLatestUploadAsync(db, channelId, ct);
				var index = playlistNumber.Value - 2;
				if (index >= 0 && index < orderedPlaylists.Count)
					playlistId = orderedPlaylists[index].Id;
			}

			var page = Math.Max(1, int.TryParse(httpContext.Request.Query["page"].FirstOrDefault(), out var p) ? p : 1);
			var pageSize = Math.Clamp(int.TryParse(httpContext.Request.Query["pageSize"].FirstOrDefault(), out var ps) ? ps : 100, 1, 500);

			var historyQuery = db.DownloadHistory.AsNoTracking().Where(h => h.ChannelId == channelId);
			if (playlistId.HasValue)
				historyQuery = historyQuery.Where(h => h.PlaylistId == playlistId.Value);

			var rows = await historyQuery
				.OrderByDescending(h => h.Date)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync(ct);
			var videoIds = rows.Select(h => h.VideoId).Distinct().ToList();
			var videos = videoIds.Count == 0
				? new Dictionary<int, VideoEntity>()
				: await db.Videos.AsNoTracking().Where(v => videoIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);

			static string MapHistoryEventType(int eventType) => eventType switch
			{
				1 => "grabbed",
				3 => "downloadFolderImported",
				4 => "downloadFailed",
				5 => "videoFileDeleted",
				6 => "videoFileRenamed",
				7 => "downloadIgnored",
				_ => "downloadFolderImported"
			};

			static string HistoryQualityName(DownloadHistoryEntity h)
			{
				if (h.EventType == 4)
				{
					return string.Equals(
						(h.Message ?? "").Trim(),
						DownloadQueueProcessor.DownloadQueueCancelledUserMessage,
						StringComparison.Ordinal)
						? "Cancelled"
						: "Failed";
				}

				return "Unknown";
			}

			var items = rows
				.Select(h =>
				{
					videos.TryGetValue(h.VideoId, out var video);
					var title = video?.Title ?? h.SourceTitle;
					object data = h.EventType == 4
						? new HistoryFailedData(string.IsNullOrWhiteSpace(h.Message) ? "Download failed." : h.Message)
						: new HistoryImportedData("yt-dlp", "yt-dlp", h.OutputPath ?? string.Empty, h.OutputPath ?? string.Empty);

					return new HistoryItemDto(
						Id: h.Id,
						ChannelId: h.ChannelId,
						VideoId: h.VideoId,
						EventType: MapHistoryEventType(h.EventType),
						SourceTitle: string.IsNullOrWhiteSpace(h.SourceTitle) ? title : h.SourceTitle,
						Languages: Array.Empty<object>(),
						Quality: new HistoryQualityDto(
							new HistoryQualityDetailsDto(0, HistoryQualityName(h)),
							new HistoryRevisionDto(1, 0, false)),
						QualityCutoffNotMet: false,
						CustomFormats: Array.Empty<object>(),
						CustomFormatScore: 0,
						Date: new DateTimeOffset(DateTime.SpecifyKind(h.Date, DateTimeKind.Utc), TimeSpan.Zero),
						Data: data,
						DownloadId: h.DownloadId ?? h.Id.ToString()
					);
				})
				.ToArray();

			return Results.Json(items);
		});

		api.MapGet("/queue", async (HttpContext httpContext, TubeArrDbContext db) =>
		{
			var page = Math.Max(1, int.TryParse(httpContext.Request.Query["page"].FirstOrDefault(), out var p) ? p : 1);
			var pageSize = Math.Clamp(int.TryParse(httpContext.Request.Query["pageSize"].FirstOrDefault(), out var ps) ? ps : 20, 1, 500);
			var sortKey = httpContext.Request.Query["sortKey"].FirstOrDefault() ?? "queuedAt";
			var sortDirection = string.Equals(httpContext.Request.Query["sortDirection"].FirstOrDefault(), "descending", StringComparison.OrdinalIgnoreCase)
				? "descending"
				: "ascending";

			static string StatusLabel(string s) => s switch
			{
				QueueJobStatuses.Queued => "Queued",
				QueueJobStatuses.Running => "Downloading",
				QueueJobStatuses.Completed => "Completed",
				QueueJobStatuses.Failed => "Failed",
				QueueJobStatuses.Aborted => "Cancelled",
				_ => "Unknown"
			};

			var sortKeyLower = sortKey.ToLowerInvariant();
			var desc = sortDirection == "descending";

			var filteredQueue = db.DownloadQueue.AsNoTracking()
				.Where(q => q.Status != QueueJobStatuses.Completed);

			var totalRecords = await filteredQueue.CountAsync();
			if (totalRecords == 0)
				return Results.Json(new QueuePageDto(Array.Empty<QueueItemDto>(), 0, pageSize, page));

			var totalPages = Math.Max((int)Math.Ceiling(totalRecords / (double)pageSize), 1);
			if (page > totalPages)
				page = totalPages;

			// SQLite: EF cannot translate ORDER BY on DateTimeOffset (e.g. QueuedAtUtc, AirDateUtc). Use raw SQL so SQLite sorts the underlying TEXT columns.
			var orderByExpr = sortKeyLower switch
			{
				"status" => "q.\"Status\"",
				"channel.sorttitle" or "channels" => "c.\"Title\"",
				"videos.title" or "video" => "v.\"Title\"",
				"videos.airdateutc" => "v.\"AirDateUtc\"",
				"estimatedcompletiontime" or "timeleft" => "q.\"EstimatedSecondsRemaining\"",
				"progress" => "q.\"Progress\"",
				_ => "q.\"QueuedAtUtc\"",
			};
			var dir = desc ? "DESC" : "ASC";
			var offset = (page - 1) * pageSize;
			var idSql =
				"SELECT q.\"Id\" FROM \"DownloadQueue\" q " +
				"INNER JOIN \"Channels\" c ON q.\"ChannelId\" = c.\"Id\" " +
				"INNER JOIN \"Videos\" v ON q.\"VideoId\" = v.\"Id\" " +
				"WHERE q.\"Status\" <> {0} " +
				"ORDER BY " + orderByExpr + " " + dir + ", q.\"Id\" " + dir + " " +
				"LIMIT {1} OFFSET {2}";
			var ids = await db.Database.SqlQueryRaw<int>(idSql, QueueJobStatuses.Completed, pageSize, offset).ToListAsync();

			List<(DownloadQueueEntity q, ChannelEntity c, VideoEntity v)> pageData;
			if (ids.Count == 0)
				pageData = new List<(DownloadQueueEntity, ChannelEntity, VideoEntity)>();
			else
			{
				var idSet = ids.ToHashSet();
				var rows = await (
					from q in db.DownloadQueue.AsNoTracking()
					join c in db.Channels.AsNoTracking() on q.ChannelId equals c.Id
					join v in db.Videos.AsNoTracking() on q.VideoId equals v.Id
					where idSet.Contains(q.Id)
					select new { q, c, v }).ToListAsync();
				var byId = rows.ToDictionary(x => x.q.Id);
				pageData = ids.Select(id => (byId[id].q, byId[id].c, byId[id].v)).ToList();
			}

			var profileIds = pageData.Select(x => x.c.QualityProfileId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
			var profiles = profileIds.Count > 0
				? await db.QualityProfiles.AsNoTracking().Where(p => profileIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id)
				: new Dictionary<int, QualityProfileEntity>();

			var records = pageData.Select(x =>
			{
				var q = x.q;
				double? progress = q.Progress ?? (q.Status == QueueJobStatuses.Running ? 0 : (q.Status == QueueJobStatuses.Completed ? 1.0 : null));
				QueueQualityRef? quality = null;
				if (x.c.QualityProfileId.HasValue && profiles.TryGetValue(x.c.QualityProfileId.Value, out var prof))
					quality = new QueueQualityRef(prof.Id, prof.Name);

				var statusText = StatusLabel(q.Status);
				var acquisitionMethods = AcquisitionMethodsJsonHelper.Parse(q.AcquisitionMethodsJson);
				return new QueueItemDto(
					Id: q.Id,
					VideoId: q.VideoId,
					ChannelId: q.ChannelId,
					Status: statusText,
					StatusLabel: statusText,
					ErrorMessage: q.LastError,
					OutputPath: q.OutputPath,
					AcquisitionMethods: acquisitionMethods,
					QueuedAt: q.QueuedAtUtc,
					StartedAt: q.StartedAtUtc,
					CompletedAt: q.EndedAtUtc,
					Progress: progress,
					EstimatedSecondsRemaining: q.EstimatedSecondsRemaining,
					EstimatedCompletionTime: q.EstimatedSecondsRemaining,
					FormatSummary: q.FormatSummary,
					DownloadedBytes: q.DownloadedBytes,
					TotalBytes: q.TotalBytes,
					SpeedBytesPerSecond: q.SpeedBytesPerSecond,
					Quality: quality,
					Channel: new QueueChannelRef(x.c.Id, x.c.Title, x.c.Title),
					Video: new QueueVideoRef(x.v.Id, x.v.Title, x.v.YoutubeVideoId),
					Videos: new QueueVideosRef(x.v.Title, x.v.UploadDateUtc)
				);
			}).ToList();

			return Results.Json(new QueuePageDto(records, totalRecords, pageSize, page));
		});

		api.MapGet("/queue/details", async (HttpContext httpContext, TubeArrDbContext db, CancellationToken ct) =>
		{
			static string TrackedState(string s) => s switch
			{
				QueueJobStatuses.Queued => "queued",
				QueueJobStatuses.Running => "downloading",
				QueueJobStatuses.Completed => "completed",
				QueueJobStatuses.Failed => "failed",
				QueueJobStatuses.Aborted => "cancelled",
				_ => "unknown"
			};

			var channelIdParam = httpContext.Request.Query["channelId"].FirstOrDefault();
			var items = await db.DownloadQueue.AsNoTracking()
				.Join(
					db.Videos.AsNoTracking(),
					q => q.VideoId,
					v => v.Id,
					(q, v) => new { q.ChannelId, q.VideoId, q.Status })
				.ToListAsync(ct);
			var queuedVideoIds = items.Select(x => x.VideoId).Distinct().ToList();
			var videoFiles = queuedVideoIds.Count == 0
				? new List<(int VideoId, string Path)>()
				: (await db.VideoFiles.AsNoTracking()
					.Where(vf => queuedVideoIds.Contains(vf.VideoId))
					.Select(vf => new { vf.VideoId, vf.Path })
					.ToListAsync(ct))
					.Select(x => (VideoId: x.VideoId, Path: x.Path ?? string.Empty))
					.ToList();
			var videoIdsWithFiles = new HashSet<int>(
				videoFiles
					.Where(vf => !string.IsNullOrWhiteSpace(vf.Path) && File.Exists(vf.Path))
					.Select(vf => vf.VideoId));

			var videoIdsByChannel = items
				.GroupBy(x => x.ChannelId)
				.ToDictionary(g => g.Key, g => (IReadOnlyCollection<int>)g.Select(x => x.VideoId).Distinct().ToList());
			var primaryPlaylistByVideoId = await ChannelDtoMapper.LoadPrimaryPlaylistIdByVideoIdsBatchedAsync(db, videoIdsByChannel, ct);

			var channelIdsForPlaylists = items.Select(x => x.ChannelId).Distinct().ToList();
			var playlistRows = channelIdsForPlaylists.Count == 0
				? new List<PlaylistEntity>()
				: await db.Playlists.AsNoTracking()
					.Where(p => channelIdsForPlaylists.Contains(p.ChannelId))
					.OrderBy(p => p.ChannelId)
					.ThenBy(p => p.Id)
					.ToListAsync(ct);

			var maxUploadByPlaylist = await ChannelDtoMapper.LoadMaxUploadUtcByPlaylistIdsAsync(db, playlistRows.Select(p => p.Id), ct);
			var customPlForQueue = channelIdsForPlaylists.Count == 0
				? new List<ChannelCustomPlaylistEntity>()
				: await db.ChannelCustomPlaylists.AsNoTracking()
					.Where(c => channelIdsForPlaylists.Contains(c.ChannelId))
					.ToListAsync(ct);
			var customByChannelForQueue = customPlForQueue
				.GroupBy(c => c.ChannelId)
				.ToDictionary(g => g.Key, g => (IReadOnlyList<ChannelCustomPlaylistEntity>)g.OrderBy(x => x.Priority).ThenBy(x => x.Id).ToList());
			var mergedYoutubePlaylistIdToNumber = new Dictionary<int, int>();
			foreach (var group in playlistRows.GroupBy(p => p.ChannelId))
			{
				var ordered = ChannelDtoMapper.OrderPlaylistsByLatestUpload(group.ToList(), maxUploadByPlaylist);
				var customFor = customByChannelForQueue.TryGetValue(group.Key, out var cp) ? cp : Array.Empty<ChannelCustomPlaylistEntity>();
				var (ytMap, _) = ChannelDtoMapper.BuildMergedCuratedPlaylistNumberMaps(ordered, customFor);
				foreach (var kv in ytMap)
					mergedYoutubePlaylistIdToNumber[kv.Key] = kv.Value;
			}

			var list = items.Select(q =>
			{
				var ppid = primaryPlaylistByVideoId.GetValueOrDefault(q.VideoId);
				var pn = 1;
				if (ppid.HasValue && mergedYoutubePlaylistIdToNumber.TryGetValue(ppid.Value, out var mapped))
					pn = mapped;
				return new QueueDetailItemDto(
					ChannelId: q.ChannelId,
					VideoId: q.VideoId,
					TrackedDownloadState: TrackedState(q.Status),
					VideoHasFile: videoIdsWithFiles.Contains(q.VideoId),
					PlaylistNumber: pn
				);
			}).ToList();

			if (int.TryParse(channelIdParam, out var channelId) && channelId > 0)
				list = list.Where(x => x.ChannelId == channelId).ToList();

			return Results.Json(list);
		});

		api.MapGet("/queue/status", async (TubeArrDbContext db) =>
		{
			var totalCount = await db.DownloadQueue.CountAsync(q =>
				q.Status == QueueJobStatuses.Queued || q.Status == QueueJobStatuses.Running);
			return Results.Json(new { totalCount });
		});

		api.MapDelete("/queue/{id:int}", async (int id, TubeArrDbContext db, IRealtimeEventBroadcaster realtime, CancellationToken ct) =>
		{
			var row = await db.DownloadQueue.FirstOrDefaultAsync(q => q.Id == id, ct);
			if (row is null)
				return Results.NotFound();

			switch (row.Status)
			{
				case QueueJobStatuses.Queued:
				case QueueJobStatuses.Failed:
				case QueueJobStatuses.Aborted:
					db.DownloadQueue.Remove(row);
					await db.SaveChangesAsync(ct);
					await RealtimeBroadcastHelper.BroadcastLiveQueueAndHistoryAsync(realtime, ct);
					return Results.Ok(new { id, removed = true });
				case QueueJobStatuses.Running:
					if (DownloadQueueProcessor.TryCancelActiveDownload(id))
					{
						await RealtimeBroadcastHelper.BroadcastLiveQueueAndHistoryAsync(realtime, ct);
						return Results.Ok(new { id, cancelling = true });
					}

					return Results.Conflict(new { message = "Download is running but could not be cancelled (try again in a moment)." });
				default:
					return Results.Conflict(new { message = "This queue item cannot be removed in its current state." });
			}
		});

		api.MapDelete("/queue", async (TubeArrDbContext db, IRealtimeEventBroadcaster realtime, CancellationToken ct) =>
		{
			var removed = await db.DownloadQueue
				.Where(q =>
					q.Status == QueueJobStatuses.Queued
					|| q.Status == QueueJobStatuses.Failed
					|| q.Status == QueueJobStatuses.Aborted)
				.ExecuteDeleteAsync(ct);

			var runningIds = await db.DownloadQueue.AsNoTracking()
				.Where(q => q.Status == QueueJobStatuses.Running)
				.Select(q => q.Id)
				.ToListAsync(ct);
			var cancelling = 0;
			var cancelFailed = 0;
			foreach (var id in runningIds)
			{
				if (DownloadQueueProcessor.TryCancelActiveDownload(id))
					cancelling++;
				else
					cancelFailed++;
			}

			if (removed > 0 || cancelling > 0)
				await RealtimeBroadcastHelper.BroadcastLiveQueueAndHistoryAsync(realtime, ct);

			return Results.Json(new
			{
				removed,
				cancelling,
				cancelFailed,
				running = runningIds.Count
			});
		});

		api.MapPost("/queue/process", async (TubeArrDbContext db, DownloadQueueProcessTrigger queueTrigger, IRealtimeEventBroadcaster realtime) =>
		{
			if (DownloadQueueProcessor.IsProcessing)
				return Results.Json(new { started = false, alreadyRunning = true });

			var executablePath = await YtDlpMetadataService.GetExecutablePathAsync(db, default);
			if (string.IsNullOrWhiteSpace(executablePath))
				return Results.Json(new { started = false, reason = "yt-dlp path is not configured. Set it in Settings → Tools → yt-dlp." }, statusCode: 503);

			queueTrigger.SignalRunRequested();
			await RealtimeBroadcastHelper.BroadcastLiveQueueAndHistoryAsync(realtime);
			return Results.Json(new { started = true, accepted = true });
		});
	}
}

