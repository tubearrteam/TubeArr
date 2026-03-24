using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;
using TubeArr.Backend.Realtime;

namespace TubeArr.Backend;

internal static class ChannelCrudEndpoints
{
	internal static void Map(RouteGroupBuilder api)
	{
		api.MapPost("/channels", async (CreateChannelRequest request, TubeArrDbContext db, HttpContext httpContext, ChannelIngestionOrchestrator ingestionOrchestrator) =>
		{
			var (channel, _, errorMessage) = await ingestionOrchestrator.CreateOrUpdateAsync(request, db, httpContext.RequestAborted);
			if (!string.IsNullOrWhiteSpace(errorMessage))
				return Results.BadRequest(new { message = errorMessage });

			if (channel is null)
				return Results.BadRequest(new { message = "Unable to create channel." });

			var playlists = await db.Playlists.AsNoTracking().Where(p => p.ChannelId == channel.Id).ToListAsync();
			var totalVideoCount = await db.Videos.AsNoTracking().CountAsync(x => x.ChannelId == channel.Id);
			var monitoredVideoCount = await db.Videos.AsNoTracking().CountAsync(x => x.ChannelId == channel.Id && x.Monitored);
			var videoFileStats = await ChannelVideoFileStatistics.GetByChannelIdAsync(db, channel.Id);
			var monitoredVideoFileCount = await ChannelVideoFileStatistics.GetMonitoredByChannelIdAsync(db, channel.Id);
			return Results.Json(ChannelDtoMapper.CreateChannelDto(channel, playlists, monitoredVideoCount, monitoredVideoFileCount, videoFileStats.SizeOnDisk, totalVideoCount));
		});

		api.MapPut("/channels/{id:int}", async (int id, UpdateChannelRequest request, TubeArrDbContext db, IRealtimeEventBroadcaster realtime) =>
		{
			var channel = await db.Channels.FirstOrDefaultAsync(x => x.Id == id);
			if (channel is null)
			{
				return Results.NotFound();
			}

			if (!string.IsNullOrWhiteSpace(request.Title))
			{
				channel.Title = request.Title.Trim();
				channel.TitleSlug = SlugHelper.Slugify(channel.Title);
			}

			if (request.Description is not null)
			{
				channel.Description = request.Description;
			}

			if (!string.IsNullOrWhiteSpace(request.ThumbnailUrl))
			{
				channel.ThumbnailUrl = request.ThumbnailUrl.Trim();
			}

			if (request.Monitored.HasValue)
			{
				channel.Monitored = request.Monitored.Value;
			}

			if (request.QualityProfileId.IsSpecified)
			{
				var qualityProfileId = request.QualityProfileId.Value;
				channel.QualityProfileId = qualityProfileId is > 0 ? qualityProfileId : null;
			}

			if (request.Path is not null)
				channel.Path = request.Path;
			if (request.RootFolderPath is not null)
				channel.RootFolderPath = request.RootFolderPath;
			if (request.Tags is not null)
				channel.Tags = request.Tags;
			if (request.MonitorNewItems.HasValue)
				channel.MonitorNewItems = request.MonitorNewItems;
			if (request.PlaylistFolder.HasValue)
				channel.PlaylistFolder = request.PlaylistFolder;
			if (request.ChannelType is not null)
				channel.ChannelType = request.ChannelType;

			if (request.RoundRobinLatestVideoCount.IsSpecified)
									{
				var roundRobinLatestVideoCount = request.RoundRobinLatestVideoCount.Value;
				channel.RoundRobinLatestVideoCount = roundRobinLatestVideoCount is > 0 ? roundRobinLatestVideoCount : null;
			}

			if (request.FilterOutShorts.HasValue)
				channel.FilterOutShorts = request.FilterOutShorts.Value;
			if (request.FilterOutLivestreams.HasValue)
				channel.FilterOutLivestreams = request.FilterOutLivestreams.Value;

			if (channel.FilterOutShorts || channel.FilterOutLivestreams)
			{
				var shorts = await db.Videos
					.Where(v =>
						v.ChannelId == id &&
						v.Monitored &&
						((channel.FilterOutShorts && v.IsShort) || (channel.FilterOutLivestreams && v.IsLivestream)))
					.ToListAsync();
				foreach (var v in shorts)
					v.Monitored = false;
			}

			await db.SaveChangesAsync();
			await RoundRobinMonitoringHelper.ApplyForChannelAsync(db, id, default);

			var playlists = await db.Playlists.AsNoTracking().Where(p => p.ChannelId == id).ToListAsync();
			var totalVideoCount = await db.Videos.AsNoTracking().CountAsync(x => x.ChannelId == id);
			var monitoredVideoCount = await db.Videos.AsNoTracking().CountAsync(x => x.ChannelId == id && x.Monitored);
			var videoFileStats = await ChannelVideoFileStatistics.GetByChannelIdAsync(db, id);
			var monitoredVideoFileCount = await ChannelVideoFileStatistics.GetMonitoredByChannelIdAsync(db, id);
			var dto = ChannelDtoMapper.CreateChannelDto(channel, playlists, monitoredVideoCount, monitoredVideoFileCount, videoFileStats.SizeOnDisk, totalVideoCount);
			await realtime.BroadcastAsync("channel", new { action = "updated", resource = dto });
			return Results.Json(dto);
		});

		api.MapDelete("/channels/{id:int}", async (int id, TubeArrDbContext db) =>
		{
			var channel = await db.Channels.FirstOrDefaultAsync(x => x.Id == id);
			if (channel is null)
			{
				return Results.NotFound();
			}

			var playlistIds = await db.Playlists
				.Where(x => x.ChannelId == id)
				.Select(x => x.Id)
				.ToListAsync();
			var videoIds = await db.Videos
				.Where(x => x.ChannelId == id)
				.Select(x => x.Id)
				.ToListAsync();

			if (videoIds.Count > 0)
			{
				var videoFiles = await db.VideoFiles
					.Where(x => videoIds.Contains(x.VideoId) || x.ChannelId == id)
					.ToListAsync();
				if (videoFiles.Count > 0)
					db.VideoFiles.RemoveRange(videoFiles);

				var queueItems = await db.DownloadQueue
					.Where(x => videoIds.Contains(x.VideoId) || x.ChannelId == id)
					.ToListAsync();
				if (queueItems.Count > 0)
					db.DownloadQueue.RemoveRange(queueItems);
			}

			var historyItems = await db.DownloadHistory
				.Where(x =>
					x.ChannelId == id ||
					videoIds.Contains(x.VideoId) ||
					(x.PlaylistId.HasValue && playlistIds.Contains(x.PlaylistId.Value)))
				.ToListAsync();
			if (historyItems.Count > 0)
				db.DownloadHistory.RemoveRange(historyItems);

			var videos = await db.Videos
				.Where(x => x.ChannelId == id)
				.ToListAsync();
			if (videos.Count > 0)
				db.Videos.RemoveRange(videos);

			var playlists = await db.Playlists
				.Where(x => x.ChannelId == id)
				.ToListAsync();
			if (playlists.Count > 0)
				db.Playlists.RemoveRange(playlists);

			db.Channels.Remove(channel);
			await db.SaveChangesAsync();
			return Results.Ok();
		});

		api.MapPost("/channels/bulk/monitoring", async (BulkChannelMonitoringRequest request, TubeArrDbContext db, IRealtimeEventBroadcaster realtime) =>
		{
			if (request.ChannelIds is not { Length: > 0 })
				return Results.BadRequest(new { message = "channelIds is required" });
			if (string.IsNullOrWhiteSpace(request.Monitor) ||
			    string.Equals(request.Monitor, "noChange", StringComparison.OrdinalIgnoreCase))
				return Results.BadRequest(new { message = "monitor is required" });

			var monitorKey = request.Monitor.Trim();
			if (string.Equals(monitorKey, "roundRobin", StringComparison.OrdinalIgnoreCase) &&
			    (request.RoundRobinLatestVideoCount is not int rrCount || rrCount <= 0))
				return Results.BadRequest(new { message = "roundRobinLatestVideoCount must be a positive integer when monitor is roundRobin" });

			var updated = await BulkChannelMonitoringHelper.ApplyAsync(
				db,
				request.ChannelIds,
				monitorKey,
				request.RoundRobinLatestVideoCount,
				default);
			await realtime.BroadcastAsync("channel", new { action = "sync" });
			await realtime.BroadcastAsync("video", new { action = "sync" });
			return Results.Json(new { updated = updated.Count, channelIds = updated.ToArray() });
		});
	}
}
