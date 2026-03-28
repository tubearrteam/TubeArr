using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

/// <summary>
/// When set on a channel, only the N newest videos (by upload date, then id) stay monitored;
/// all other videos on that channel are unmonitored. Disabled when channel is not monitored or N is unset.
/// </summary>
public static class RoundRobinMonitoringHelper
{
	public static async Task ApplyForChannelAsync(TubeArrDbContext db, int channelId, CancellationToken ct = default)
	{
		var channel = await db.Channels
			.AsNoTracking()
			.FirstOrDefaultAsync(c => c.Id == channelId, ct);
		if (channel is null)
			return;

		await ApplyForChannelAsync(db, channel, ct);
	}

	public static async Task ApplyForChannelAsync(TubeArrDbContext db, ChannelEntity channel, CancellationToken ct = default)
	{
		var cap = channel.RoundRobinLatestVideoCount;
		if (!channel.Monitored || cap is null or <= 0)
			return;

		var videos = (await db.Videos
			.Where(v => v.ChannelId == channel.Id)
			.ToListAsync(ct))
			.OrderByDescending(v => v.UploadDateUtc)
			.ThenByDescending(v => v.Id)
			.ToList();

		if (videos.Count == 0)
			return;

		IEnumerable<VideoEntity> ranked = videos;
		if (channel.FilterOutShorts && channel.HasShortsTab == true)
			ranked = ranked.Where(v => !v.IsShort);
		if (channel.FilterOutLivestreams)
			ranked = ranked.Where(v => !v.IsLivestream);

		var keepIds = ranked
			.Take(cap.Value)
			.Select(v => v.Id)
			.ToHashSet();
		var changed = false;
		foreach (var video in videos)
		{
			var wantMonitored = keepIds.Contains(video.Id);
			if (video.Monitored == wantMonitored)
				continue;
			video.Monitored = wantMonitored;
			changed = true;
		}

		if (changed)
			await db.SaveChangesAsync(ct);
	}
}
