using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
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
			var customPlaylistsForDto = await db.ChannelCustomPlaylists.AsNoTracking()
				.Where(c => c.ChannelId == channel.Id)
				.OrderBy(c => c.Priority)
				.ThenBy(c => c.Id)
				.ToListAsync(httpContext.RequestAborted);
			var maxUploadByPlaylist = await ChannelDtoMapper.LoadMaxUploadUtcByPlaylistIdsAsync(db, playlists.Select(p => p.Id), httpContext.RequestAborted);
			var totalVideoCount = await db.Videos.AsNoTracking().CountAsync(x => x.ChannelId == channel.Id);
			var monitoredVideoCount = await db.Videos.AsNoTracking().CountAsync(x => x.ChannelId == channel.Id && x.Monitored);
			var videoFileStats = await ChannelVideoFileStatistics.GetByChannelIdAsync(db, channel.Id);
			var monitoredVideoFileCount = await ChannelVideoFileStatistics.GetMonitoredByChannelIdAsync(db, channel.Id);
			var maxUploadByChannel = await ChannelDtoMapper.LoadMaxUploadUtcByChannelIdsAsync(db, new[] { channel.Id }, httpContext.RequestAborted);
			var minActiveSinceByChannel = await ChannelDtoMapper.LoadMinActiveSinceUtcByChannelIdsAsync(db, new[] { channel.Id }, httpContext.RequestAborted);
			DateTimeOffset? lastUploadUtc = maxUploadByChannel.TryGetValue(channel.Id, out var lu) ? lu : null;
			DateTimeOffset? firstUploadUtc = minActiveSinceByChannel.TryGetValue(channel.Id, out var fu) ? fu : null;
			return Results.Json(ChannelDtoMapper.CreateChannelDto(channel, playlists, customPlaylistsForDto, monitoredVideoCount, monitoredVideoFileCount, videoFileStats.SizeOnDisk, totalVideoCount, maxUploadByPlaylist, lastUploadUtc: lastUploadUtc, firstUploadUtc: firstUploadUtc));
		});

		api.MapPut("/channels/{id:int}", async (int id, UpdateChannelRequest request, TubeArrDbContext db, HttpContext httpContext, IRealtimeEventBroadcaster realtime) =>
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
			if (request.PlaylistMultiMatchStrategyOrder is not null)
			{
				var normalized = ChannelDtoMapper.NormalizePlaylistMultiMatchStrategyOrder(request.PlaylistMultiMatchStrategyOrder.Trim());
				if (normalized is not null)
				{
					channel.PlaylistMultiMatchStrategyOrder = normalized;
					channel.PlaylistMultiMatchStrategy = normalized[0] - '0';
				}
			}
			else if (request.PlaylistMultiMatchStrategy.HasValue)
			{
				var s = request.PlaylistMultiMatchStrategy.Value;
				channel.PlaylistMultiMatchStrategy = s is >= 0 and <= 3 ? s : 0;
				channel.PlaylistMultiMatchStrategyOrder = ChannelDtoMapper.DerivePlaylistMultiMatchStrategyOrderFromLegacy(channel.PlaylistMultiMatchStrategy);
			}

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

			if (request.MonitorPreset.IsSpecified)
			{
				var raw = request.MonitorPreset.Value;
				if (string.IsNullOrWhiteSpace(raw))
					channel.MonitorPreset = null;
				else if (string.Equals(raw.Trim(), "specificVideos", StringComparison.OrdinalIgnoreCase))
					channel.MonitorPreset = "specificVideos";
				else if (string.Equals(raw.Trim(), "specificPlaylists", StringComparison.OrdinalIgnoreCase))
					channel.MonitorPreset = "specificPlaylists";
				else
					channel.MonitorPreset = null;
			}

			if (request.CustomPlaylists is not null)
			{
				var existingCustom = await db.ChannelCustomPlaylists.Where(x => x.ChannelId == id).ToListAsync();
				var incomingIds = request.CustomPlaylists
					.Where(x => x.Id is int i && i > 0)
					.Select(x => (int)x.Id!)
					.ToHashSet();
				foreach (var e in existingCustom)
				{
					if (!incomingIds.Contains(e.Id))
						db.ChannelCustomPlaylists.Remove(e);
				}

				var maxYtP = await db.Playlists.Where(p => p.ChannelId == id).MaxAsync(p => (int?)p.Priority) ?? -1;
				var maxCP = await db.ChannelCustomPlaylists.Where(c => c.ChannelId == id).MaxAsync(c => (int?)c.Priority) ?? -1;
				var nextNewCustomPriority = Math.Max(maxYtP, maxCP) + 1;
				var newCustomInsertIndex = 0;

				foreach (var row in request.CustomPlaylists)
				{
					if (string.IsNullOrWhiteSpace(row.Name))
						return Results.BadRequest(new { message = "customPlaylist name is required" });
					var rules = (row.Rules ?? Array.Empty<ChannelCustomPlaylistRuleDto>())
						.Select(SaveDtoToRule)
						.ToList();
					var err = ChannelCustomPlaylistRulesHelper.ValidateRules(rules);
					if (err is not null)
						return Results.BadRequest(new { message = err });
					var json = ChannelCustomPlaylistRulesHelper.SerializeRules(rules);
					var mt = row.MatchType is 1 ? 1 : 0;
					if (row.Id is int rid && rid > 0)
					{
						var ent = existingCustom.FirstOrDefault(x => x.Id == rid);
						if (ent is null)
							return Results.BadRequest(new { message = "customPlaylist id not found" });
						ent.Name = row.Name.Trim();
						ent.Enabled = row.Enabled;
						ent.Priority = row.Priority;
						ent.MatchType = mt;
						ent.RulesJson = json;
					}
					else
					{
						db.ChannelCustomPlaylists.Add(new ChannelCustomPlaylistEntity
						{
							ChannelId = id,
							Name = row.Name.Trim(),
							Enabled = row.Enabled,
							Priority = nextNewCustomPriority + newCustomInsertIndex++,
							MatchType = mt,
							RulesJson = json
						});
					}
				}
			}

			if (request.Playlists is { Length: > 0 })
			{
				var youtubePlaylists = await db.Playlists.Where(p => p.ChannelId == id).ToListAsync();
				var maxUpload = await ChannelDtoMapper.LoadMaxUploadUtcByPlaylistIdsAsync(db, youtubePlaylists.Select(p => p.Id), httpContext.RequestAborted);
				var ordered = ChannelDtoMapper.OrderPlaylistsByLatestUpload(youtubePlaylists, maxUpload);
				var customRows = await db.ChannelCustomPlaylists.Where(c => c.ChannelId == id).OrderBy(c => c.Priority).ThenBy(c => c.Id).ToListAsync();
				var n = 2;
				var numberToYoutube = new Dictionary<int, PlaylistEntity>();
				foreach (var p in ordered)
					numberToYoutube[n++] = p;
				var numberToCustom = new Dictionary<int, ChannelCustomPlaylistEntity>();
				foreach (var c in customRows)
					numberToCustom[n++] = c;

				foreach (var pl in request.Playlists)
				{
					if (pl.PlaylistNumber <= 1)
						continue;
					if (pl.IsCustom)
					{
						if (pl.CustomPlaylistId is int cid)
						{
							var ent = customRows.FirstOrDefault(x => x.Id == cid);
							if (ent is not null)
							{
								ent.Enabled = pl.Monitored;
								if (pl.Priority.HasValue)
									ent.Priority = pl.Priority.Value;
							}
						}
						else if (numberToCustom.TryGetValue(pl.PlaylistNumber, out var ce))
						{
							ce.Enabled = pl.Monitored;
							if (pl.Priority.HasValue)
								ce.Priority = pl.Priority.Value;
						}
					}
					else if (pl.PlaylistId is int yPid && youtubePlaylists.FirstOrDefault(x => x.Id == yPid) is { } ypById)
					{
						ypById.Monitored = pl.Monitored;
						if (pl.Priority.HasValue)
							ypById.Priority = pl.Priority.Value;
					}
					else if (numberToYoutube.TryGetValue(pl.PlaylistNumber, out var yp))
					{
						yp.Monitored = pl.Monitored;
						if (pl.Priority.HasValue)
							yp.Priority = pl.Priority.Value;
					}
				}
			}

			if ((channel.FilterOutShorts && channel.HasShortsTab == true) || channel.FilterOutLivestreams)
			{
				var shorts = await db.Videos
					.Where(v =>
						v.ChannelId == id &&
						v.Monitored &&
						((channel.FilterOutShorts && channel.HasShortsTab == true && v.IsShort) || (channel.FilterOutLivestreams && v.IsLivestream)))
					.ToListAsync();
				foreach (var v in shorts)
					v.Monitored = false;
			}

			await db.SaveChangesAsync();
			await RoundRobinMonitoringHelper.ApplyForChannelAsync(db, id, default);

			var playlists = await db.Playlists.AsNoTracking().Where(p => p.ChannelId == id).ToListAsync();
			var maxUploadByPlaylist = await ChannelDtoMapper.LoadMaxUploadUtcByPlaylistIdsAsync(db, playlists.Select(p => p.Id), httpContext.RequestAborted);
			var totalVideoCount = await db.Videos.AsNoTracking().CountAsync(x => x.ChannelId == id);
			var monitoredVideoCount = await db.Videos.AsNoTracking().CountAsync(x => x.ChannelId == id && x.Monitored);
			var videoFileStats = await ChannelVideoFileStatistics.GetByChannelIdAsync(db, id);
			var monitoredVideoFileCount = await ChannelVideoFileStatistics.GetMonitoredByChannelIdAsync(db, id);
			var maxUploadByChannel = await ChannelDtoMapper.LoadMaxUploadUtcByChannelIdsAsync(db, new[] { id }, httpContext.RequestAborted);
			var minActiveSinceByChannel = await ChannelDtoMapper.LoadMinActiveSinceUtcByChannelIdsAsync(db, new[] { id }, httpContext.RequestAborted);
			DateTimeOffset? lastUploadUtc = maxUploadByChannel.TryGetValue(id, out var lu) ? lu : null;
			DateTimeOffset? firstUploadUtc = minActiveSinceByChannel.TryGetValue(id, out var fu) ? fu : null;
			var customPlaylistsDto = await db.ChannelCustomPlaylists.AsNoTracking()
				.Where(c => c.ChannelId == id)
				.OrderBy(c => c.Priority)
				.ThenBy(c => c.Id)
				.ToListAsync(httpContext.RequestAborted);
			var dto = ChannelDtoMapper.CreateChannelDto(channel, playlists, customPlaylistsDto, monitoredVideoCount, monitoredVideoFileCount, videoFileStats.SizeOnDisk, totalVideoCount, maxUploadByPlaylist, lastUploadUtc: lastUploadUtc, firstUploadUtc: firstUploadUtc);
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

	static ChannelCustomPlaylistRule SaveDtoToRule(ChannelCustomPlaylistRuleDto dto) =>
		new() { Field = dto.Field ?? "", Operator = dto.Operator ?? "", Value = dto.Value };
}
