using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Data;

namespace TubeArr.Backend.Media;

internal static class StableTvNumbering
{
	internal const int ChannelOnlySeasonIndex = 1;
	internal const int FirstPlaylistSeasonIndex = 2;

	internal static async Task<int> EnsurePlaylistSeasonIndexAsync(TubeArrDbContext db, int playlistId, CancellationToken ct)
	{
		var playlist = await db.Playlists.FirstOrDefaultAsync(p => p.Id == playlistId, ct);
		if (playlist is null)
			return FirstPlaylistSeasonIndex;

		if (playlist.SeasonIndex.HasValue && playlist.SeasonIndex.Value > 0)
			return playlist.SeasonIndex.Value;

		var channelId = playlist.ChannelId;
		var max = await db.Playlists.AsNoTracking()
			.Where(p => p.ChannelId == channelId && p.SeasonIndex.HasValue && p.SeasonIndex.Value >= FirstPlaylistSeasonIndex)
			.Select(p => (int?)p.SeasonIndex!.Value)
			.MaxAsync(ct);

		var next = (max.HasValue && max.Value >= FirstPlaylistSeasonIndex)
			? max.Value + 1
			: FirstPlaylistSeasonIndex;

		playlist.SeasonIndex = next;
		playlist.SeasonIndexLocked = true;
		await db.SaveChangesAsync(ct);

		return next;
	}

	internal static async Task EnsureVideoPlexIndicesAsync(
		TubeArrDbContext db,
		int channelId,
		IReadOnlyCollection<int> videoIds,
		CancellationToken ct)
	{
		if (videoIds.Count == 0)
			return;

		var videos = await db.Videos
			.Where(v => v.ChannelId == channelId && videoIds.Contains(v.Id))
			.ToListAsync(ct);

		var needs = videos.Where(v => !v.PlexSeasonIndex.HasValue || !v.PlexEpisodeIndex.HasValue).ToList();
		if (needs.Count == 0)
			return;

		var primaryByVideoId = await ChannelDtoMapper.LoadPrimaryPlaylistIdByVideoIdsForChannelAsync(
			db,
			channelId,
			videoIds.ToList(),
			ct);

		foreach (var v in videos)
		{
			if (v.PlexSeasonIndex.HasValue && v.PlexEpisodeIndex.HasValue)
				continue;

			var primaryPlaylistId = primaryByVideoId.GetValueOrDefault(v.Id);
			v.PlexPrimaryPlaylistId = primaryPlaylistId;
		}

		var playlistIds = videos
			.Select(v => v.PlexPrimaryPlaylistId)
			.Where(x => x.HasValue && x.Value > 0)
			.Select(x => x!.Value)
			.Distinct()
			.ToList();

		var seasonIndexByPlaylistId = new Dictionary<int, int>();
		foreach (var pid in playlistIds)
		{
			var season = await EnsurePlaylistSeasonIndexAsync(db, pid, ct);
			seasonIndexByPlaylistId[pid] = season;
		}

		foreach (var v in videos)
		{
			if (v.PlexSeasonIndex.HasValue && v.PlexEpisodeIndex.HasValue)
				continue;

			if (v.PlexPrimaryPlaylistId.HasValue && v.PlexPrimaryPlaylistId.Value > 0 &&
			    seasonIndexByPlaylistId.TryGetValue(v.PlexPrimaryPlaylistId.Value, out var seasonIndex))
			{
				v.PlexSeasonIndex = seasonIndex;
			}
			else
			{
				v.PlexSeasonIndex = ChannelOnlySeasonIndex;
				v.PlexPrimaryPlaylistId = null;
			}
		}

		await db.SaveChangesAsync(ct);

		// Now assign episode indices deterministically.
		// For playlist seasons, expand to the full playlist membership so indices are stable regardless of which video triggered the assignment.
		foreach (var pid in playlistIds)
		{
			if (!seasonIndexByPlaylistId.TryGetValue(pid, out var seasonIndex))
				continue;
			await EnsurePlaylistSeasonEpisodeIndicesAsync(db, channelId, pid, seasonIndex, ct);
		}

		// Channel-only season (uploads): stabilize based on upload date for all channel videos mapped to season 01.
		await EnsureEpisodeIndicesForSeasonAsync(db, channelId, ChannelOnlySeasonIndex, ct);
	}

	static async Task EnsurePlaylistSeasonEpisodeIndicesAsync(TubeArrDbContext db, int channelId, int playlistId, int seasonIndex, CancellationToken ct)
	{
		var playlistVideoIds = await db.PlaylistVideos.AsNoTracking()
			.Where(pv => pv.PlaylistId == playlistId)
			.Select(pv => pv.VideoId)
			.Distinct()
			.ToListAsync(ct);

		if (playlistVideoIds.Count == 0)
			return;

		var primaryByVideoId = await ChannelDtoMapper.LoadPrimaryPlaylistIdByVideoIdsForChannelAsync(db, channelId, playlistVideoIds, ct);

		var toUpdate = await db.Videos
			.Where(v => v.ChannelId == channelId && playlistVideoIds.Contains(v.Id))
			.ToListAsync(ct);

		var changed = false;
		foreach (var v in toUpdate)
		{
			if (v.PlexIndexLocked)
				continue;

			var primary = primaryByVideoId.GetValueOrDefault(v.Id);
			if (primary != playlistId)
				continue;

			if (v.PlexPrimaryPlaylistId != playlistId)
			{
				v.PlexPrimaryPlaylistId = playlistId;
				changed = true;
			}
			if (v.PlexSeasonIndex != seasonIndex)
			{
				v.PlexSeasonIndex = seasonIndex;
				changed = true;
			}
		}

		if (changed)
			await db.SaveChangesAsync(ct);

		// Now that primary videos are mapped to the season, assign missing episode indices for that season.
		await EnsureEpisodeIndicesForSeasonAsync(db, channelId, seasonIndex, ct);
	}

	static async Task EnsureEpisodeIndicesForSeasonAsync(TubeArrDbContext db, int channelId, int seasonIndex, CancellationToken ct)
	{
		var seasonVideos = await db.Videos
			.Where(v => v.ChannelId == channelId && v.PlexSeasonIndex == seasonIndex)
			.Select(v => new { v.Id, v.UploadDateUtc, v.PlexEpisodeIndex, v.PlexPrimaryPlaylistId })
			.ToListAsync(ct);

		if (seasonVideos.Count == 0)
			return;

		if (seasonVideos.All(v => v.PlexEpisodeIndex.HasValue && v.PlexEpisodeIndex.Value > 0))
			return;

		int? playlistId = null;
		if (seasonIndex != ChannelOnlySeasonIndex)
		{
			playlistId = await db.Playlists.AsNoTracking()
				.Where(p => p.ChannelId == channelId && p.SeasonIndex == seasonIndex)
				.Select(p => (int?)p.Id)
				.OrderBy(p => p)
				.FirstOrDefaultAsync(ct);
		}

		List<int> orderedVideoIds;
		if (playlistId.HasValue)
		{
			var rows = await (
				from pv in db.PlaylistVideos.AsNoTracking()
				join v in db.Videos.AsNoTracking() on pv.VideoId equals v.Id
				where pv.PlaylistId == playlistId.Value
				select new { pv.VideoId, pv.Position, pv.AddedAt, v.UploadDateUtc }
			).ToListAsync(ct);

			orderedVideoIds = rows
				.OrderBy(x => x.Position ?? int.MaxValue)
				.ThenBy(x => x.AddedAt ?? DateTimeOffset.MaxValue)
				.ThenBy(x => x.UploadDateUtc)
				.ThenBy(x => x.VideoId)
				.Select(x => x.VideoId)
				.Distinct()
				.ToList();

			// Some videos may be assigned to this season but not currently in playlistVideos (stale membership);
			// keep them at the end in a stable order.
			var remaining = seasonVideos.Select(v => v.Id).Where(id => !orderedVideoIds.Contains(id)).OrderBy(id => id).ToList();
			orderedVideoIds.AddRange(remaining);
		}
		else
		{
			orderedVideoIds = seasonVideos
				.OrderBy(v => v.UploadDateUtc)
				.ThenBy(v => v.Id)
				.Select(v => v.Id)
				.ToList();
		}

		var tracked = await db.Videos
			.Where(v => v.ChannelId == channelId && v.PlexSeasonIndex == seasonIndex)
			.ToListAsync(ct);

		var byId = tracked.ToDictionary(v => v.Id);

		var next = 1;
		for (var i = 0; i < orderedVideoIds.Count; i++)
		{
			var id = orderedVideoIds[i];
			if (!byId.TryGetValue(id, out var v))
				continue;

			if (v.PlexEpisodeIndex.HasValue && v.PlexEpisodeIndex.Value > 0)
				continue;

			while (tracked.Any(x => x.PlexEpisodeIndex == next))
				next++;

			v.PlexEpisodeIndex = next;
			next++;
		}

		await db.SaveChangesAsync(ct);
	}
}

