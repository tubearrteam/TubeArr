using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class ChannelVideoFileStatistics
{
	internal static Task<Dictionary<int, (int VideoFileCount, long SizeOnDisk)>> GetByChannelIdsAsync(TubeArrDbContext db, IReadOnlyCollection<int> channelIds)
	{
		if (channelIds.Count == 0)
			return Task.FromResult(new Dictionary<int, (int VideoFileCount, long SizeOnDisk)>());

		return db.VideoFiles.AsNoTracking()
			.Where(vf => channelIds.Contains(vf.ChannelId))
			.GroupBy(vf => vf.ChannelId)
			.Select(g => new
			{
				ChannelId = g.Key,
				VideoFileCount = g.Count(),
				SizeOnDisk = g.Sum(vf => vf.Size)
			})
			.ToDictionaryAsync(
				x => x.ChannelId,
				x => (x.VideoFileCount, x.SizeOnDisk));
	}

	internal static async Task<(int VideoFileCount, long SizeOnDisk)> GetByChannelIdAsync(TubeArrDbContext db, int channelId)
	{
		var stats = await db.VideoFiles.AsNoTracking()
			.Where(vf => vf.ChannelId == channelId)
			.GroupBy(vf => vf.ChannelId)
			.Select(g => new
			{
				VideoFileCount = g.Count(),
				SizeOnDisk = g.Sum(vf => vf.Size)
			})
			.FirstOrDefaultAsync();

		return stats is null ? (0, 0) : (stats.VideoFileCount, stats.SizeOnDisk);
	}

	internal static Task<Dictionary<int, int>> GetMonitoredByChannelIdsAsync(TubeArrDbContext db, IReadOnlyCollection<int> channelIds)
	{
		if (channelIds.Count == 0)
			return Task.FromResult(new Dictionary<int, int>());

		return (
			from vf in db.VideoFiles.AsNoTracking()
			join v in db.Videos.AsNoTracking() on vf.VideoId equals v.Id
			where channelIds.Contains(v.ChannelId) && v.Monitored
			group vf by v.ChannelId into g
			select new
			{
				ChannelId = g.Key,
				VideoFileCount = g.Count()
			})
			.ToDictionaryAsync(x => x.ChannelId, x => x.VideoFileCount);
	}

	internal static async Task<int> GetMonitoredByChannelIdAsync(TubeArrDbContext db, int channelId)
	{
		return await (
			from vf in db.VideoFiles.AsNoTracking()
			join v in db.Videos.AsNoTracking() on vf.VideoId equals v.Id
			where v.ChannelId == channelId && v.Monitored
			select vf.Id
		).CountAsync();
	}
}