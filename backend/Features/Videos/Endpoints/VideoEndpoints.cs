using System.Collections.Generic;
using System.Linq;
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
			TubeArrDbContext db,
			CancellationToken ct) =>
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

			var items = await query.ToListAsync(ct);
			var channelIds = items.Select(v => v.ChannelId).Distinct().ToList();
			var itemVideoIds = items.Select(v => v.Id).ToList();
			var videoIdsByChannel = items
				.GroupBy(v => v.ChannelId)
				.ToDictionary(g => g.Key, g => (IReadOnlyCollection<int>)g.Select(v => v.Id).ToList());
			var primaryPlaylistByVideoId = await ChannelDtoMapper.LoadPrimaryPlaylistIdByVideoIdsBatchedAsync(db, videoIdsByChannel, ct);
			var playlistRows = await db.Playlists.AsNoTracking()
				.Where(p => channelIds.Contains(p.ChannelId))
				.OrderBy(p => p.ChannelId)
				.ThenBy(p => p.Id)
				.ToListAsync(ct);
			var allCustomPlaylists = await db.ChannelCustomPlaylists.AsNoTracking()
				.Where(c => channelIds.Contains(c.ChannelId))
				.ToListAsync(ct);
			var customByChannelId = allCustomPlaylists
				.GroupBy(c => c.ChannelId)
				.ToDictionary(g => g.Key, g => (IReadOnlyList<ChannelCustomPlaylistEntity>)g.OrderBy(x => x.Priority).ThenBy(x => x.Id).ToList());
			var maxUploadByPlaylist = await ChannelDtoMapper.LoadMaxUploadUtcByPlaylistIdsAsync(db, playlistRows.Select(p => p.Id), ct);
			var videoFiles = itemVideoIds.Count == 0
				? new List<VideoFileEntity>()
				: await db.VideoFiles.AsNoTracking()
					.Where(vf => itemVideoIds.Contains(vf.VideoId))
					.ToListAsync(ct);
			var videoFileByVideoId = videoFiles
				.GroupBy(vf => vf.VideoId)
				.ToDictionary(g => g.Key, g => g.OrderByDescending(vf => vf.DateAdded).First());
			var mergedYoutubePlaylistIdToNumber = new Dictionary<int, int>();
			foreach (var group in playlistRows.GroupBy(p => p.ChannelId))
			{
				var ordered = ChannelDtoMapper.OrderPlaylistsByLatestUpload(group.ToList(), maxUploadByPlaylist);
				if (!customByChannelId.TryGetValue(group.Key, out var customForChannel))
					customForChannel = Array.Empty<ChannelCustomPlaylistEntity>();
				var (ytMap, _) = ChannelDtoMapper.BuildMergedCuratedPlaylistNumberMaps(ordered, customForChannel);
				foreach (var kv in ytMap)
					mergedYoutubePlaylistIdToNumber[kv.Key] = kv.Value;
			}

			var pvRows = await db.PlaylistVideos.AsNoTracking()
				.Where(pv => itemVideoIds.Contains(pv.VideoId))
				.Select(pv => new { pv.VideoId, pv.PlaylistId })
				.ToListAsync(ct);
			var curatedIdsByVideoId = pvRows
				.GroupBy(x => x.VideoId)
				.ToDictionary(g => g.Key, g => g.Select(x => x.PlaylistId).ToList());

			var projectionByChannel = new Dictionary<int, ChannelVideoPlaylistProjection>();
			foreach (var chId in channelIds)
			{
				var pls = playlistRows.Where(p => p.ChannelId == chId).ToList();
				var ordered = ChannelDtoMapper.OrderPlaylistsByLatestUpload(pls, maxUploadByPlaylist);
				if (!customByChannelId.TryGetValue(chId, out var customOrdered))
					customOrdered = Array.Empty<ChannelCustomPlaylistEntity>();
				var playlistById = pls.ToDictionary(p => p.Id);
				var (_, customIdToNum) = ChannelDtoMapper.BuildMergedCuratedPlaylistNumberMaps(ordered, customOrdered);
				projectionByChannel[chId] = new ChannelVideoPlaylistProjection(playlistById, customOrdered, customIdToNum);
			}

			var sortedList = new List<VideoDto>(items.Count);
			foreach (var video in items.OrderByDescending(x => x.UploadDateUtc).ThenByDescending(x => x.Id))
			{
				var playlistNumber = 1;
				if (primaryPlaylistByVideoId.TryGetValue(video.Id, out var ppid) &&
				    ppid.HasValue &&
				    mergedYoutubePlaylistIdToNumber.TryGetValue(ppid.Value, out var mappedNumber))
					playlistNumber = mappedNumber;
				curatedIdsByVideoId.TryGetValue(video.Id, out var curatedPlaylistIds);
				var curatedPlaylistNumbers = MapPlaylistIdsToUiNumbers(curatedPlaylistIds, mergedYoutubePlaylistIdToNumber);
				var file = videoFileByVideoId.GetValueOrDefault(video.Id);
				var hasFile = file is not null && !string.IsNullOrWhiteSpace(file.Path) && File.Exists(file.Path);
				if (!projectionByChannel.TryGetValue(video.ChannelId, out var proj))
				{
					sortedList.Add(CreateVideoDto(video, playlistNumber, curatedPlaylistNumbers, Array.Empty<int>(), file?.Id, hasFile));
					continue;
				}

				curatedIdsByVideoId.TryGetValue(video.Id, out var allPid);
				var primaryPid = primaryPlaylistByVideoId.TryGetValue(video.Id, out var pp) ? pp : null;
				IReadOnlyCollection<int> allPlaylistIds = allPid is not null ? allPid : Array.Empty<int>();
				var customNums = ChannelCustomPlaylistVideoMatching.ComputeCustomPlaylistNumbers(
					video,
					primaryPid,
					allPlaylistIds,
					proj.PlaylistById,
					proj.CustomOrdered,
					proj.CustomPlaylistIdToPlaylistNumber);

				sortedList.Add(CreateVideoDto(video, playlistNumber, curatedPlaylistNumbers, customNums, file?.Id, hasFile));
			}

			var sorted = sortedList.ToArray();

			httpContext.Response.Headers["X-Total-Count"] = sorted.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
			return Results.Json(sorted);
		});

		api.MapPut("/videos/{id:int}", async (int id, UpdateVideoRequest request, TubeArrDbContext db, IRealtimeEventBroadcaster realtime, CancellationToken ct) =>
		{
			var video = await db.Videos.FirstOrDefaultAsync(x => x.Id == id, ct);
			if (video is null)
			{
				return Results.NotFound();
			}

			if (request.Monitored.HasValue)
			{
				video.Monitored = request.Monitored.Value;
				var filterOutShorts = await db.Channels.AsNoTracking()
					.Where(c => c.Id == video.ChannelId)
					.Select(c => new { c.FilterOutShorts, c.FilterOutLivestreams, c.HasShortsTab })
					.FirstAsync(ct);
				FilterOutShortsMonitoringHelper.ClampVideoMonitored(
					video,
					filterOutShorts.FilterOutShorts,
					filterOutShorts.FilterOutLivestreams,
					filterOutShorts.HasShortsTab);
			}

			await db.SaveChangesAsync(ct);
			var videoFile = await db.VideoFiles.AsNoTracking().FirstOrDefaultAsync(vf => vf.VideoId == video.Id, ct);
			var hasFile = videoFile is not null && !string.IsNullOrWhiteSpace(videoFile.Path) && File.Exists(videoFile.Path);
			var playlistRowsOne = await db.Playlists.AsNoTracking()
				.Where(p => p.ChannelId == video.ChannelId)
				.ToListAsync(ct);
			var maxUploadOne = await ChannelDtoMapper.LoadMaxUploadUtcByPlaylistIdsAsync(db, playlistRowsOne.Select(p => p.Id), ct);
			var customRows = await db.ChannelCustomPlaylists.AsNoTracking()
				.Where(c => c.ChannelId == video.ChannelId)
				.OrderBy(c => c.Priority)
				.ThenBy(c => c.Id)
				.ToListAsync(ct);
			var orderedOne = ChannelDtoMapper.OrderPlaylistsByLatestUpload(playlistRowsOne, maxUploadOne);
			var (mergedYtOne, customIdToNum) = ChannelDtoMapper.BuildMergedCuratedPlaylistNumberMaps(orderedOne, customRows);

			var playlistNumber = 1;
			var primaryPid = await ChannelDtoMapper.GetPrimaryPlaylistIdForVideoAsync(db, video.ChannelId, video.Id, ct);
			if (primaryPid.HasValue && mergedYtOne.TryGetValue(primaryPid.Value, out var mappedPn))
				playlistNumber = mappedPn;

			var curatedPidRows = await db.PlaylistVideos.AsNoTracking()
				.Where(pv => pv.VideoId == video.Id)
				.Select(pv => pv.PlaylistId)
				.ToListAsync(ct);
			var curatedPlaylistNumbers = MapPlaylistIdsToUiNumbers(curatedPidRows, mergedYtOne);
			var playlistByIdOne = playlistRowsOne.ToDictionary(p => p.Id);
			var customNums = ChannelCustomPlaylistVideoMatching.ComputeCustomPlaylistNumbers(
				video,
				primaryPid,
				curatedPidRows,
				playlistByIdOne,
				customRows,
				customIdToNum);

			var dto = CreateVideoDto(video, playlistNumber, curatedPlaylistNumbers, customNums, videoFile?.Id, hasFile);
			await realtime.BroadcastAsync("video", new { action = "updated", resource = dto });
			return Results.Json(dto);
		});

		api.MapPut("/videos/monitor", async (MonitorVideosRequest request, TubeArrDbContext db, IRealtimeEventBroadcaster realtime) =>
		{
			if (request.VideoIds is not { Length: > 0 })
			{
				return ApiErrorResults.BadRequest(TubeArrErrorCodes.InvalidInput, "videoIds is required");
			}

			var videos = await db.Videos.Where(x => request.VideoIds.Contains(x.Id)).ToListAsync();
			var channelIds = videos.Select(v => v.ChannelId).Distinct().ToList();
			var filterMap = await db.Channels.AsNoTracking()
				.Where(c => channelIds.Contains(c.Id))
				.ToDictionaryAsync(c => c.Id, c => new { c.FilterOutShorts, c.FilterOutLivestreams, c.HasShortsTab });
			foreach (var video in videos)
			{
				video.Monitored = request.Monitored;
				if (filterMap.TryGetValue(video.ChannelId, out var fo))
					FilterOutShortsMonitoringHelper.ClampVideoMonitored(video, fo.FilterOutShorts, fo.FilterOutLivestreams, fo.HasShortsTab);
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

	readonly record struct ChannelVideoPlaylistProjection(
		Dictionary<int, PlaylistEntity> PlaylistById,
		IReadOnlyList<ChannelCustomPlaylistEntity> CustomOrdered,
		Dictionary<int, int> CustomPlaylistIdToPlaylistNumber);

	internal static VideoDto CreateVideoDto(
		VideoEntity video,
		int playlistNumber = 1,
		int[]? curatedPlaylistNumbers = null,
		int[]? customPlaylistNumbers = null,
		int? videoFileId = null,
		bool hasFile = false)
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
			CuratedPlaylistNumbers: curatedPlaylistNumbers ?? Array.Empty<int>(),
			CustomPlaylistNumbers: customPlaylistNumbers ?? Array.Empty<int>(),
			VideoFileId: videoFileId,
			HasFile: hasFile,
			IsShort: video.IsShort,
			IsLivestream: video.IsLivestream,
			YouTubeDataApiVideoResourceJson: video.YouTubeDataApiVideoResourceJson
		);
	}

	static int[] MapPlaylistIdsToUiNumbers(
		IReadOnlyCollection<int>? playlistIds,
		IReadOnlyDictionary<int, int> playlistNumberByPlaylistId)
	{
		if (playlistIds is null || playlistIds.Count == 0)
			return Array.Empty<int>();

		var nums = new HashSet<int>();
		foreach (var pid in playlistIds)
		{
			if (playlistNumberByPlaylistId.TryGetValue(pid, out var n))
				nums.Add(n);
		}

		var arr = nums.ToArray();
		Array.Sort(arr);
		return arr;
	}
}
