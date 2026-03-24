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
		api.MapGet("/channels", async (TubeArrDbContext db) =>
		{
			var channels = await db.Channels.AsNoTracking().OrderBy(x => x.Title).ToListAsync();
			var channelIds = channels.Select(c => c.Id).ToList();
			var allPlaylists = await db.Playlists.AsNoTracking().Where(p => channelIds.Contains(p.ChannelId)).ToListAsync();
			var playlistsByChannelId = allPlaylists.GroupBy(p => p.ChannelId).ToDictionary(g => g.Key, g => g.ToList());
			var videoCountsByChannelId = await db.Videos.AsNoTracking()
				.Where(v => channelIds.Contains(v.ChannelId))
				.GroupBy(v => v.ChannelId)
				.Select(g => new { ChannelId = g.Key, Count = g.Count() })
				.ToDictionaryAsync(x => x.ChannelId, x => x.Count);
			var videoFileStatsByChannelId = await ChannelVideoFileStatistics.GetByChannelIdsAsync(db, channelIds);
			var result = channels.Select(c =>
			{
				var videoFileStats = videoFileStatsByChannelId.GetValueOrDefault(c.Id);
					return ChannelDtoMapper.CreateChannelDto(
					c,
					(IReadOnlyList<PlaylistEntity>?)playlistsByChannelId.GetValueOrDefault(c.Id) ?? Array.Empty<PlaylistEntity>(),
					videoCountsByChannelId.GetValueOrDefault(c.Id, 0),
					videoFileCount: videoFileStats.VideoFileCount,
					sizeOnDisk: videoFileStats.SizeOnDisk);
			}).ToArray();
			return Results.Json(result);
		});

		api.MapGet("/channels/{id:int}", async (int id, TubeArrDbContext db) =>
		{
			var channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
			if (channel is null)
				return Results.NotFound();
			var playlists = await db.Playlists.AsNoTracking().Where(p => p.ChannelId == id).ToListAsync();
			var videoCount = await db.Videos.AsNoTracking().CountAsync(x => x.ChannelId == id);
			var videoFileStats = await ChannelVideoFileStatistics.GetByChannelIdAsync(db, id);
			return Results.Json(ChannelDtoMapper.CreateChannelDto(channel, playlists, videoCount, videoFileStats.VideoFileCount, videoFileStats.SizeOnDisk));
		});

		api.MapGet("/channels/editor", () => Results.Json(new Dictionary<string, object?>()));
	}
}
