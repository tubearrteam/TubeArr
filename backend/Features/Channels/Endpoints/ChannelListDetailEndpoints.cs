using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class ChannelListDetailEndpoints
{
	internal static void Map(RouteGroupBuilder api)
	{
		api.MapGet("/channels", async (TubeArrDbContext db, CancellationToken ct) =>
		{
			var channels = await db.Channels.AsNoTracking().OrderBy(x => x.Title).ToListAsync(ct);
			var channelIds = channels.Select(c => c.Id).ToList();
			var allPlaylists = await db.Playlists.AsNoTracking().Where(p => channelIds.Contains(p.ChannelId)).ToListAsync(ct);
			var playlistsByChannelId = allPlaylists.GroupBy(p => p.ChannelId).ToDictionary(g => g.Key, g => g.ToList());
			var maxUploadByPlaylist = await ChannelDtoMapper.LoadMaxUploadUtcByPlaylistIdsAsync(db, allPlaylists.Select(p => p.Id), ct);
			var totalVideoCountsByChannelId = await db.Videos.AsNoTracking()
				.Where(v => channelIds.Contains(v.ChannelId))
				.GroupBy(v => v.ChannelId)
				.Select(g => new { ChannelId = g.Key, Count = g.Count() })
				.ToDictionaryAsync(x => x.ChannelId, x => x.Count, ct);
			var monitoredVideoCountsByChannelId = await db.Videos.AsNoTracking()
				.Where(v => channelIds.Contains(v.ChannelId) && v.Monitored)
				.GroupBy(v => v.ChannelId)
				.Select(g => new { ChannelId = g.Key, Count = g.Count() })
				.ToDictionaryAsync(x => x.ChannelId, x => x.Count, ct);
			var videoFileStatsByChannelId = await ChannelVideoFileStatistics.GetByChannelIdsAsync(db, channelIds);
			var monitoredVideoFileCountsByChannelId = await ChannelVideoFileStatistics.GetMonitoredByChannelIdsAsync(db, channelIds);
			var result = channels.Select(c =>
			{
				var videoFileStats = videoFileStatsByChannelId.GetValueOrDefault(c.Id);
					return ChannelDtoMapper.CreateChannelDto(
					c,
					(IReadOnlyList<PlaylistEntity>?)playlistsByChannelId.GetValueOrDefault(c.Id) ?? Array.Empty<PlaylistEntity>(),
					monitoredVideoCountsByChannelId.GetValueOrDefault(c.Id, 0),
					videoFileCount: monitoredVideoFileCountsByChannelId.GetValueOrDefault(c.Id, 0),
					sizeOnDisk: videoFileStats.SizeOnDisk,
					totalVideoCount: totalVideoCountsByChannelId.GetValueOrDefault(c.Id, 0),
					maxUploadUtcByPlaylistId: maxUploadByPlaylist);
			}).ToArray();
			return Results.Json(result);
		});

		api.MapGet("/channels/{id:int}", async (int id, TubeArrDbContext db, CancellationToken ct) =>
		{
			var channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
			if (channel is null)
				return Results.NotFound();
			var playlists = await db.Playlists.AsNoTracking().Where(p => p.ChannelId == id).ToListAsync(ct);
			var maxUploadByPlaylist = await ChannelDtoMapper.LoadMaxUploadUtcByPlaylistIdsAsync(db, playlists.Select(p => p.Id), ct);
			var totalVideoCount = await db.Videos.AsNoTracking().CountAsync(x => x.ChannelId == id, ct);
			var monitoredVideoCount = await db.Videos.AsNoTracking().CountAsync(x => x.ChannelId == id && x.Monitored, ct);
			var videoFileStats = await ChannelVideoFileStatistics.GetByChannelIdAsync(db, id);
			var monitoredVideoFileCount = await ChannelVideoFileStatistics.GetMonitoredByChannelIdAsync(db, id);
			return Results.Json(ChannelDtoMapper.CreateChannelDto(channel, playlists, monitoredVideoCount, monitoredVideoFileCount, videoFileStats.SizeOnDisk, totalVideoCount, maxUploadByPlaylist));
		});

		api.MapGet("/channels/editor", () => Results.Json(new Dictionary<string, object?>()));
	}
}
