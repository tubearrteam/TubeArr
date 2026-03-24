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
	sealed record QueueRowJoin(DownloadQueueEntity Queue, ChannelEntity Channel, VideoEntity Video);

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
			row.Date = DateTimeOffset.UtcNow;
			await db.SaveChangesAsync();

			await RealtimeBroadcastHelper.BroadcastLiveQueueAndHistoryAsync(realtime);
			return Results.Ok();
		});

		api.MapGet("/history/channel", async (int channelId, int? playlistNumber, TubeArrDbContext db) =>
		{
			if (channelId <= 0)
				return Results.Json(Array.Empty<object>());

			int? playlistId = null;
			if (playlistNumber.HasValue && playlistNumber.Value > 1)
			{
				var orderedPlaylists = await db.Playlists.AsNoTracking()
					.Where(p => p.ChannelId == channelId)
					.OrderBy(p => p.Id)
					.ToListAsync();
				var index = playlistNumber.Value - 2;
				if (index >= 0 && index < orderedPlaylists.Count)
					playlistId = orderedPlaylists[index].Id;
			}

			var historyQuery = db.DownloadHistory.AsNoTracking().Where(h => h.ChannelId == channelId);
			if (playlistId.HasValue)
				historyQuery = historyQuery.Where(h => h.PlaylistId == playlistId.Value);

			var rows = await historyQuery.ToListAsync();
			var videoIds = rows.Select(h => h.VideoId).Distinct().ToList();
			var videos = videoIds.Count == 0
				? new Dictionary<int, VideoEntity>()
				: await db.Videos.AsNoTracking().Where(v => videoIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id);

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

			var items = rows
				.OrderByDescending(h => h.Date)
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
							new HistoryQualityDetailsDto(0, "Unknown"),
							new HistoryRevisionDto(1, 0, false)),
						QualityCutoffNotMet: false,
						CustomFormats: Array.Empty<object>(),
						CustomFormatScore: 0,
						Date: h.Date,
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
			var pageSize = Math.Clamp(int.TryParse(httpContext.Request.Query["pageSize"].FirstOrDefault(), out var ps) ? ps : 20, 1, 100);
			var sortKey = httpContext.Request.Query["sortKey"].FirstOrDefault() ?? "queuedAt";
			var sortDirection = string.Equals(httpContext.Request.Query["sortDirection"].FirstOrDefault(), "descending", StringComparison.OrdinalIgnoreCase)
				? "descending"
				: "ascending";

			const int Queued = 0, Downloading = 1, Completed = 2, Failed = 3;
			string StatusLabel(int s) => s switch { Queued => "Queued", Downloading => "Downloading", Completed => "Completed", Failed => "Failed", _ => "Unknown" };

			var queueRows = await db.DownloadQueue.AsNoTracking()
				.Where(q => q.Status != Completed)
				.ToListAsync();

			var totalRecords = queueRows.Count;
			if (totalRecords == 0)
				return Results.Json(new QueuePageDto(Array.Empty<QueueItemDto>(), 0, pageSize));

			var channelIds = queueRows.Select(q => q.ChannelId).Distinct().ToList();
			var videoIds = queueRows.Select(q => q.VideoId).Distinct().ToList();

			var channelsById = await db.Channels.AsNoTracking()
				.Where(c => channelIds.Contains(c.Id))
				.ToDictionaryAsync(c => c.Id);

			var videosById = await db.Videos.AsNoTracking()
				.Where(v => videoIds.Contains(v.Id))
				.ToDictionaryAsync(v => v.Id);

			var joinedRows = queueRows
				.Select(q =>
				{
					var channel = channelsById.GetValueOrDefault(q.ChannelId);
					var video = videosById.GetValueOrDefault(q.VideoId);
					if (channel is null || video is null)
						return null;

					return new QueueRowJoin(q, channel, video);
				})
				.Where(x => x is not null)
				.Select(x => x!)
				.ToList();

			var sortKeyLower = sortKey.ToLowerInvariant();
			var desc = sortDirection == "descending";
			IOrderedEnumerable<QueueRowJoin> orderedRows = (sortKeyLower, desc) switch
			{
				("status", true) => joinedRows.OrderByDescending(x => x.Queue.Status).ThenByDescending(x => x.Queue.QueuedAt),
				("status", false) => joinedRows.OrderBy(x => x.Queue.Status).ThenBy(x => x.Queue.QueuedAt),
				("channel.sorttitle" or "channels", true) => joinedRows.OrderByDescending(x => x.Channel.Title).ThenByDescending(x => x.Queue.QueuedAt),
				("channel.sorttitle" or "channels", false) => joinedRows.OrderBy(x => x.Channel.Title).ThenBy(x => x.Queue.QueuedAt),
				("videos.title" or "video", true) => joinedRows.OrderByDescending(x => x.Video.Title).ThenByDescending(x => x.Queue.QueuedAt),
				("videos.title" or "video", false) => joinedRows.OrderBy(x => x.Video.Title).ThenBy(x => x.Queue.QueuedAt),
				("estimatedcompletiontime" or "timeleft", true) => joinedRows.OrderByDescending(x => x.Queue.EstimatedSecondsRemaining).ThenByDescending(x => x.Queue.QueuedAt),
				("estimatedcompletiontime" or "timeleft", false) => joinedRows.OrderBy(x => x.Queue.EstimatedSecondsRemaining).ThenBy(x => x.Queue.QueuedAt),
				(_, true) => joinedRows.OrderByDescending(x => x.Queue.QueuedAt),
				(_, false) => joinedRows.OrderBy(x => x.Queue.QueuedAt),
			};

			var pageData = orderedRows
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.ToList();

			var profileIds = pageData.Select(x => x.Channel.QualityProfileId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
			var profiles = profileIds.Count > 0
				? await db.QualityProfiles.AsNoTracking().Where(p => profileIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id)
				: new Dictionary<int, QualityProfileEntity>();

			var records = pageData.Select(x =>
			{
				var q = x.Queue;
				double? progress = q.Progress ?? (q.Status == Downloading ? 0 : (q.Status == Completed ? 1.0 : null));
				QueueQualityRef? quality = null;
				if (x.Channel.QualityProfileId.HasValue && profiles.TryGetValue(x.Channel.QualityProfileId.Value, out var prof))
					quality = new QueueQualityRef(prof.Id, prof.Name);

				var statusText = StatusLabel(q.Status);
				return new QueueItemDto(
					Id: q.Id,
					VideoId: q.VideoId,
					ChannelId: q.ChannelId,
					Status: statusText,
					StatusLabel: statusText,
					ErrorMessage: q.ErrorMessage,
					OutputPath: q.OutputPath,
					QueuedAt: q.QueuedAt,
					StartedAt: q.StartedAt,
					CompletedAt: q.CompletedAt,
					Progress: progress,
					EstimatedSecondsRemaining: q.EstimatedSecondsRemaining,
					EstimatedCompletionTime: q.EstimatedSecondsRemaining,
					Quality: quality,
					Channel: new QueueChannelRef(x.Channel.Id, x.Channel.Title, x.Channel.Title),
					Video: new QueueVideoRef(x.Video.Id, x.Video.Title, x.Video.YoutubeVideoId),
					Videos: new QueueVideosRef(x.Video.Title, x.Video.UploadDateUtc)
				);
			}).ToList();

			return Results.Json(new QueuePageDto(records, totalRecords, pageSize));
		});

		api.MapGet("/queue/details", async (HttpContext httpContext, TubeArrDbContext db) =>
		{
			const int Queued = 0, Downloading = 1, Completed = 2, Failed = 3;
			string TrackedState(int s) => s switch { Queued => "queued", Downloading => "downloading", Completed => "completed", Failed => "failed", _ => "unknown" };

			var channelIdParam = httpContext.Request.Query["channelId"].FirstOrDefault();
			var videoFiles = await db.VideoFiles.AsNoTracking().Select(vf => new { vf.VideoId, vf.Path }).ToListAsync();
			var videoIdsWithFiles = new HashSet<int>(
				videoFiles
					.Where(vf => !string.IsNullOrWhiteSpace(vf.Path) && File.Exists(vf.Path))
					.Select(vf => vf.VideoId));

			var items = await db.DownloadQueue.AsNoTracking()
				.Join(
					db.Videos.AsNoTracking(),
					q => q.VideoId,
					v => v.Id,
					(q, v) => new { q.ChannelId, q.VideoId, q.Status, v.PlaylistId })
				.ToListAsync();

			var playlistRows = await db.Playlists.AsNoTracking()
				.OrderBy(p => p.ChannelId)
				.ThenBy(p => p.Id)
				.ToListAsync();

			var playlistNumberByPlaylistId = new Dictionary<int, int>();
			foreach (var group in playlistRows.GroupBy(p => p.ChannelId))
			{
				var playlistNumber = 2; // 1 is synthetic "Videos"
				foreach (var playlist in group)
				{
					playlistNumberByPlaylistId[playlist.Id] = playlistNumber++;
				}
			}

			var list = items.Select(q => new QueueDetailItemDto(
				ChannelId: q.ChannelId,
				VideoId: q.VideoId,
				TrackedDownloadState: TrackedState(q.Status),
				VideoHasFile: videoIdsWithFiles.Contains(q.VideoId),
				PlaylistNumber: q.PlaylistId.HasValue && playlistNumberByPlaylistId.TryGetValue(q.PlaylistId.Value, out var pn)
					? pn
					: 1
			)).ToList();

			if (int.TryParse(channelIdParam, out var channelId) && channelId > 0)
				list = list.Where(x => x.ChannelId == channelId).ToList();

			return Results.Json(list);
		});

		api.MapGet("/queue/status", async (TubeArrDbContext db) =>
		{
			var totalCount = await db.DownloadQueue.CountAsync(q => q.Status == 0 || q.Status == 1);
			return Results.Json(new { totalCount });
		});

		api.MapDelete("/queue", async (TubeArrDbContext db, IRealtimeEventBroadcaster realtime) =>
		{
			var items = await db.DownloadQueue.ToListAsync();
			db.DownloadQueue.RemoveRange(items);
			await db.SaveChangesAsync();

			await RealtimeBroadcastHelper.BroadcastLiveQueueAndHistoryAsync(realtime);
			return Results.Json(new { removed = items.Count });
		});

		api.MapPost("/queue/process", async (TubeArrDbContext db, IServiceScopeFactory scopeFactory, ILogger<Program> logger, IRealtimeEventBroadcaster realtime) =>
		{
			if (DownloadQueueProcessor.IsProcessing)
				return Results.Json(new { started = false, alreadyRunning = true });

			var executablePath = await YtDlpMetadataService.GetExecutablePathAsync(db, default);
			if (string.IsNullOrWhiteSpace(executablePath))
				return Results.Json(new { started = false, reason = "yt-dlp path is not configured. Set it in Settings â†’ Tools â†’ yt-dlp." }, statusCode: 503);

			_ = Task.Run(async () =>
			{
				try
				{
					using var scope = scopeFactory.CreateScope();
					var scopedDb = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
					var scopedRealtime = scope.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>();

					await DownloadQueueProcessor.RunUntilEmptyAsync(
						scopedDb,
						CancellationToken.None,
						logger,
						async ct => await RealtimeBroadcastHelper.BroadcastLiveQueueAndHistoryAsync(scopedRealtime, ct));
				}
				catch (Exception ex)
				{
					logger.LogError(ex, "Download queue processor failed");
				}
			});

			await RealtimeBroadcastHelper.BroadcastLiveQueueAndHistoryAsync(realtime);
			return Results.Json(new { started = true });
		});
	}
}

