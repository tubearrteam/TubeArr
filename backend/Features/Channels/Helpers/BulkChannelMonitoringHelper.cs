using System.IO;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

/// <summary>
/// Applies monitoring presets from the UI (MonitorVideosSelect) to channels and their videos.
/// </summary>
public static class BulkChannelMonitoringHelper
{
	public static async Task<IReadOnlyList<int>> ApplyAsync(
		TubeArrDbContext db,
		IReadOnlyList<int> channelIds,
		string monitor,
		int? roundRobinLatestVideoCount = null,
		CancellationToken ct = default)
	{
		var key = (monitor ?? "").Trim();
		if (string.Equals(key, "noChange", StringComparison.OrdinalIgnoreCase) ||
		    string.IsNullOrWhiteSpace(key))
			return Array.Empty<int>();

		var ids = channelIds.Where(id => id > 0).Distinct().ToList();
		if (ids.Count == 0)
			return Array.Empty<int>();

		var updated = new List<int>();

		foreach (var channelId in ids)
		{
			var channel = await db.Channels.FirstOrDefaultAsync(c => c.Id == channelId, ct);
			if (channel is null)
				continue;

			var videos = await db.Videos.Where(v => v.ChannelId == channelId).ToListAsync(ct);
			var fileRows = await db.VideoFiles.Where(vf => vf.ChannelId == channelId).ToListAsync(ct);

			static bool HasOnDiskFile(int videoId, List<VideoFileEntity> rows)
			{
				foreach (var row in rows)
				{
					if (row.VideoId != videoId)
						continue;
					if (!string.IsNullOrWhiteSpace(row.Path) && File.Exists(row.Path))
						return true;
				}
				return false;
			}

			if (string.Equals(key, "none", StringComparison.OrdinalIgnoreCase))
			{
				channel.Monitored = false;
				channel.MonitorNewItems = 0;
				channel.RoundRobinLatestVideoCount = null;
				foreach (var v in videos)
					v.Monitored = false;
			}
			else if (string.Equals(key, "all", StringComparison.OrdinalIgnoreCase) ||
			         string.Equals(key, "recent", StringComparison.OrdinalIgnoreCase))
			{
				channel.Monitored = true;
				channel.MonitorNewItems = 1;
				channel.RoundRobinLatestVideoCount = null;
				foreach (var v in videos)
					v.Monitored = true;
				FilterOutShortsMonitoringHelper.ApplyChannelPolicyToVideos(channel, videos);
			}
			else if (string.Equals(key, "future", StringComparison.OrdinalIgnoreCase))
			{
				channel.Monitored = true;
				channel.MonitorNewItems = 1;
				channel.RoundRobinLatestVideoCount = null;
				foreach (var v in videos)
					v.Monitored = false;
			}
			else if (string.Equals(key, "missing", StringComparison.OrdinalIgnoreCase))
			{
				channel.Monitored = true;
				channel.RoundRobinLatestVideoCount = null;
				foreach (var v in videos)
					v.Monitored = !HasOnDiskFile(v.Id, fileRows);
				FilterOutShortsMonitoringHelper.ApplyChannelPolicyToVideos(channel, videos);
			}
			else if (string.Equals(key, "existing", StringComparison.OrdinalIgnoreCase))
			{
				channel.Monitored = true;
				channel.RoundRobinLatestVideoCount = null;
				foreach (var v in videos)
					v.Monitored = HasOnDiskFile(v.Id, fileRows);
				FilterOutShortsMonitoringHelper.ApplyChannelPolicyToVideos(channel, videos);
			}
			else if (string.Equals(key, "roundRobin", StringComparison.OrdinalIgnoreCase))
			{
				var cap = roundRobinLatestVideoCount is int c && c > 0 ? c : 0;
				if (cap <= 0)
					continue;

				channel.Monitored = true;
				channel.MonitorNewItems = 1;
				channel.RoundRobinLatestVideoCount = cap;
				updated.Add(channelId);
				continue;
			}
			else
				continue;

			updated.Add(channelId);
		}

		if (updated.Count == 0)
			return updated;

		await db.SaveChangesAsync(ct);

		foreach (var channelId in updated)
			await RoundRobinMonitoringHelper.ApplyForChannelAsync(db, channelId, ct);

		return updated;
	}
}
