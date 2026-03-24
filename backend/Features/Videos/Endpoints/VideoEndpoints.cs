using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;
using TubeArr.Backend.Realtime;

namespace TubeArr.Backend;

internal static class VideoEndpoints
{
	internal static void Map(RouteGroupBuilder api)
	{
		api.MapGet("/videos", async (
			HttpContext httpContext,
			int? channelId,
			int[]? videoIds,
			TubeArrDbContext db) =>
		{
			IQueryable<VideoEntity> query = db.Videos.AsNoTracking();

			if (channelId.HasValue)
			{
				query = query.Where(x => x.ChannelId == channelId.Value);
			}

			if (videoIds is { Length: > 0 })
			{
				query = query.Where(x => videoIds.Contains(x.Id));
			}

			var items = await query.ToListAsync();
			var channelIds = items.Select(v => v.ChannelId).Distinct().ToList();
			var itemVideoIds = items.Select(v => v.Id).ToList();
			var playlistRows = await db.Playlists.AsNoTracking()
				.Where(p => channelIds.Contains(p.ChannelId))
				.OrderBy(p => p.ChannelId)
				.ThenBy(p => p.Id)
				.ToListAsync();
			var videoFiles = itemVideoIds.Count == 0
				? new List<VideoFileEntity>()
				: await db.VideoFiles.AsNoTracking()
					.Where(vf => itemVideoIds.Contains(vf.VideoId))
					.ToListAsync();
			var videoFileByVideoId = videoFiles
				.GroupBy(vf => vf.VideoId)
				.ToDictionary(g => g.Key, g => g.OrderByDescending(vf => vf.DateAdded).First());
			var playlistNumberByPlaylistId = new Dictionary<int, int>();
			foreach (var group in playlistRows.GroupBy(p => p.ChannelId))
			{
				var playlistNumber = 2;
				foreach (var playlist in group)
				{
					playlistNumberByPlaylistId[playlist.Id] = playlistNumber++;
				}
			}

			var sorted = items
				.OrderByDescending(x => x.UploadDateUtc)
				.ThenByDescending(x => x.Id)
				.Select(video =>
				{
					var playlistNumber = 1;
					if (video.PlaylistId.HasValue && playlistNumberByPlaylistId.TryGetValue(video.PlaylistId.Value, out var mappedNumber))
						playlistNumber = mappedNumber;
					var file = videoFileByVideoId.GetValueOrDefault(video.Id);
					var hasFile = file is not null && !string.IsNullOrWhiteSpace(file.Path) && File.Exists(file.Path);
					return CreateVideoDto(video, playlistNumber, file?.Id, hasFile);
				})
				.ToArray();

			httpContext.Response.Headers["X-Total-Count"] = sorted.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
			return Results.Json(sorted);
		});

		api.MapPut("/videos/{id:int}", async (int id, UpdateVideoRequest request, TubeArrDbContext db, IRealtimeEventBroadcaster realtime) =>
		{
			var video = await db.Videos.FirstOrDefaultAsync(x => x.Id == id);
			if (video is null)
			{
				return Results.NotFound();
			}

			if (request.Monitored.HasValue)
			{
				video.Monitored = request.Monitored.Value;
				var filterOutShorts = await db.Channels.AsNoTracking()
					.Where(c => c.Id == video.ChannelId)
					.Select(c => new { c.FilterOutShorts, c.FilterOutLivestreams })
					.FirstAsync();
				FilterOutShortsMonitoringHelper.ClampVideoMonitored(
					video,
					filterOutShorts.FilterOutShorts,
					filterOutShorts.FilterOutLivestreams);
			}

			await db.SaveChangesAsync();
			var videoFile = await db.VideoFiles.AsNoTracking().FirstOrDefaultAsync(vf => vf.VideoId == video.Id);
			var hasFile = videoFile is not null && !string.IsNullOrWhiteSpace(videoFile.Path) && File.Exists(videoFile.Path);
			var playlistNumber = 1;
			if (video.PlaylistId.HasValue)
			{
				var orderedPlaylists = await db.Playlists.AsNoTracking()
					.Where(p => p.ChannelId == video.ChannelId)
					.OrderBy(p => p.Id)
					.Select(p => p.Id)
					.ToListAsync();
				var idx = orderedPlaylists.IndexOf(video.PlaylistId.Value);
				if (idx >= 0)
					playlistNumber = idx + 2;
			}
			var dto = CreateVideoDto(video, playlistNumber, videoFile?.Id, hasFile);
			await realtime.BroadcastAsync("video", new { action = "updated", resource = dto });
			return Results.Json(dto);
		});

		api.MapPut("/videos/monitor", async (MonitorVideosRequest request, TubeArrDbContext db, IRealtimeEventBroadcaster realtime) =>
		{
			if (request.VideoIds is not { Length: > 0 })
			{
				return Results.BadRequest(new { message = "videoIds is required" });
			}

			var videos = await db.Videos.Where(x => request.VideoIds.Contains(x.Id)).ToListAsync();
			var channelIds = videos.Select(v => v.ChannelId).Distinct().ToList();
			var filterMap = await db.Channels.AsNoTracking()
				.Where(c => channelIds.Contains(c.Id))
				.ToDictionaryAsync(c => c.Id, c => new { c.FilterOutShorts, c.FilterOutLivestreams });
			foreach (var video in videos)
			{
				video.Monitored = request.Monitored;
				if (filterMap.TryGetValue(video.ChannelId, out var fo))
					FilterOutShortsMonitoringHelper.ClampVideoMonitored(video, fo.FilterOutShorts, fo.FilterOutLivestreams);
			}

			await db.SaveChangesAsync();
			foreach (var video in videos)
			{
				await realtime.BroadcastAsync("video", new
				{
					action = "updated",
					resource = new
					{
						id = video.Id,
						channelId = video.ChannelId,
						monitored = video.Monitored
					}
				});
			}
			return Results.Json(new
			{
				updated = videos.Count,
				monitored = request.Monitored,
				videoIds = videos.Select(v => v.Id).ToArray()
			});
		});
	}

	internal static VideoDto CreateVideoDto(VideoEntity video, int playlistNumber = 1, int? videoFileId = null, bool hasFile = false)
	{
		var airDateUtc = video.AirDateUtc == default || video.AirDateUtc == DateTimeOffset.UnixEpoch
			? video.UploadDateUtc
			: video.AirDateUtc;
		var airDate = string.IsNullOrWhiteSpace(video.AirDate)
			? airDateUtc.ToString("yyyy-MM-dd")
			: video.AirDate;
		return new VideoDto(
			Id: video.Id,
			ChannelId: video.ChannelId,
			YoutubeVideoId: video.YoutubeVideoId,
			Title: video.Title,
			Description: video.Description,
			ThumbnailUrl: video.ThumbnailUrl,
			UploadDateUtc: video.UploadDateUtc,
			AirDateUtc: airDateUtc,
			AirDate: airDate,
			Overview: video.Overview ?? video.Description ?? "",
			Runtime: video.Runtime,
			Monitored: video.Monitored,
			Added: video.Added,
			PlaylistNumber: playlistNumber,
			VideoFileId: videoFileId,
			HasFile: hasFile,
			IsShort: video.IsShort,
			IsLivestream: video.IsLivestream
		);
	}
}
