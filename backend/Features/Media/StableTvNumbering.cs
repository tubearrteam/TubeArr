using System.Linq;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend;
using TubeArr.Backend.Data;

namespace TubeArr.Backend.Media;

internal static class StableTvNumbering
{
	internal const int ChannelOnlySeasonIndex = 1;
	internal const int FirstPlaylistSeasonIndex = 2;

	/// <summary>
	/// Assigns <see cref="PlaylistEntity.SeasonIndex"/> 2,3,4,… in the same order as
	/// <see cref="ChannelDtoMapper.LoadOrderedPlaylistIdsForFileOrganizationAsync"/> (primary playlist / on-disk season folders):
	/// <see cref="PlaylistEntity.Priority"/> first, then the channel's playlist multi-match strategy chain.
	/// Using <see cref="ChannelDtoMapper.OrderPlaylistsByLatestUpload"/> here caused Plex seasons to disagree with
	/// file layout when tie-breakers differed. Updates <see cref="VideoEntity.PlexSeasonIndex"/> for curated primaries.
	/// </summary>
	internal static async Task EnsureChannelPlaylistSeasonIndicesMatchPriorityAsync(TubeArrDbContext db, int channelId, CancellationToken ct)
	{
		var playlists = await db.Playlists.Where(p => p.ChannelId == channelId).ToListAsync(ct);
		if (playlists.Count == 0)
			return;

		var ch = await db.Channels.AsNoTracking()
			.Where(c => c.Id == channelId)
			.Select(c => new { c.PlaylistMultiMatchStrategy, c.PlaylistMultiMatchStrategyOrder })
			.FirstOrDefaultAsync(ct);
		var strategyOrder = ChannelDtoMapper.ParsePlaylistMultiMatchStrategyOrder(
			ch?.PlaylistMultiMatchStrategyOrder,
			ch?.PlaylistMultiMatchStrategy ?? 0);

		var maxUpload = await ChannelDtoMapper.LoadMaxUploadUtcByPlaylistIdsAsync(db, playlists.Select(p => p.Id), ct);
		var ordered = ChannelDtoMapper.OrderPlaylistsForFileOrganization(playlists, maxUpload, strategyOrder);

		var expectedSeasonByPlaylistId = new Dictionary<int, int>();
		for (var i = 0; i < ordered.Count; i++)
			expectedSeasonByPlaylistId[ordered[i].Id] = FirstPlaylistSeasonIndex + i;

		var playlistAligned = playlists.All(p =>
			expectedSeasonByPlaylistId.TryGetValue(p.Id, out var exp) && p.SeasonIndex == exp);

		var videos = await db.Videos.Where(v => v.ChannelId == channelId).ToListAsync(ct);
		var videoAligned = videos.All(v =>
		{
			if (v.PlexPrimaryCustomPlaylistId is > 0)
				return true;
			if (v.PlexPrimaryPlaylistId is null or <= 0)
				return true;
			if (!expectedSeasonByPlaylistId.TryGetValue(v.PlexPrimaryPlaylistId.Value, out var exp))
				return true;
			return v.PlexSeasonIndex == exp;
		});

		if (playlistAligned && videoAligned)
			return;

		foreach (var p in playlists)
		{
			if (expectedSeasonByPlaylistId.TryGetValue(p.Id, out var season))
			{
				p.SeasonIndex = season;
				p.SeasonIndexLocked = true;
			}
		}

		foreach (var v in videos)
		{
			if (v.PlexPrimaryCustomPlaylistId is > 0)
				continue;
			if (v.PlexPrimaryPlaylistId is > 0 &&
			    expectedSeasonByPlaylistId.TryGetValue(v.PlexPrimaryPlaylistId.Value, out var season))
				v.PlexSeasonIndex = season;
		}

		await db.SaveChangesAsync(ct);
	}

	internal static async Task<int> EnsurePlaylistSeasonIndexAsync(TubeArrDbContext db, int playlistId, CancellationToken ct)
	{
		var playlist = await db.Playlists.FirstOrDefaultAsync(p => p.Id == playlistId, ct);
		if (playlist is null)
			return FirstPlaylistSeasonIndex;

		await EnsureChannelPlaylistSeasonIndicesMatchPriorityAsync(db, playlist.ChannelId, ct);

		var refreshed = await db.Playlists.AsNoTracking().FirstOrDefaultAsync(p => p.Id == playlistId, ct);
		return refreshed?.SeasonIndex is int s && s > 0 ? s : FirstPlaylistSeasonIndex;
	}

	/// <summary>
	/// Clears Plex placement on non-locked videos, re-runs native playlist season numbering, then reassigns season/episode/custom-primary from NFO rules.
	/// </summary>
	internal static async Task<(int VideosCleared, int VideosTotal)> RebuildChannelPlexIndicesAsync(TubeArrDbContext db, int channelId, CancellationToken ct)
	{
		await EnsureChannelPlaylistSeasonIndicesMatchPriorityAsync(db, channelId, ct);

		var videos = await db.Videos.Where(v => v.ChannelId == channelId).ToListAsync(ct);
		var cleared = 0;
		foreach (var v in videos)
		{
			if (v.PlexIndexLocked)
				continue;
			if (!v.PlexSeasonIndex.HasValue && !v.PlexEpisodeIndex.HasValue &&
			    v.PlexPrimaryPlaylistId is null or <= 0 && v.PlexPrimaryCustomPlaylistId is null or <= 0)
				continue;

			v.PlexSeasonIndex = null;
			v.PlexEpisodeIndex = null;
			v.PlexPrimaryPlaylistId = null;
			v.PlexPrimaryCustomPlaylistId = null;
			cleared++;
		}

		if (cleared > 0)
			await db.SaveChangesAsync(ct);

		var allIds = videos.Select(v => v.Id).ToList();
		if (allIds.Count > 0)
			await EnsureVideoPlexIndicesAsync(db, channelId, allIds, ct);

		return (cleared, videos.Count);
	}

	internal static async Task EnsureVideoPlexIndicesAsync(
		TubeArrDbContext db,
		int channelId,
		IReadOnlyCollection<int> videoIds,
		CancellationToken ct)
	{
		if (videoIds.Count == 0)
			return;

		await EnsureChannelPlaylistSeasonIndicesMatchPriorityAsync(db, channelId, ct);

		var videos = await db.Videos
			.Where(v => v.ChannelId == channelId && videoIds.Contains(v.Id))
			.ToListAsync(ct);

		var needs = videos.Where(v => !v.PlexSeasonIndex.HasValue || !v.PlexEpisodeIndex.HasValue).ToList();
		if (needs.Count == 0)
		{
			// Rows already have season+episode, but season 01 (channel uploads) is renumbered whenever this runs for
			// missing assignments — skipping entirely left stale PlexEpisodeIndex vs filename/NFO after ordering changes.
			await EnsureEpisodeIndicesForSeasonAsync(db, channelId, ChannelOnlySeasonIndex, ct);
			return;
		}

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
			var (folderSeason, customSrc) = await NfoLibraryExporter.ResolveSeasonNumberForPlaylistFolderAsync(
				db, channelId, v, primaryPlaylistId > 0 ? primaryPlaylistId : null, ct);

			v.PlexSeasonIndex = folderSeason;
			if (customSrc is not null)
			{
				v.PlexPrimaryCustomPlaylistId = customSrc.Id;
				v.PlexPrimaryPlaylistId = null;
			}
			else
			{
				v.PlexPrimaryCustomPlaylistId = null;
				v.PlexPrimaryPlaylistId = primaryPlaylistId > 0 ? primaryPlaylistId : null;
			}
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

		await db.SaveChangesAsync(ct);

		// Now assign episode indices deterministically.
		// For playlist seasons, expand to the full playlist membership so indices are stable regardless of which video triggered the assignment.
		foreach (var pid in playlistIds)
		{
			if (!seasonIndexByPlaylistId.TryGetValue(pid, out var seasonIndex))
				continue;
			await EnsurePlaylistSeasonEpisodeIndicesAsync(db, channelId, pid, seasonIndex, ct);
		}

		var customSeasonIndexes = videos
			.Where(v => v.PlexSeasonIndex.HasValue && v.PlexSeasonIndex.Value > NfoLibraryExporter.CustomPlaylistSeasonRangeStart)
			.Select(v => v.PlexSeasonIndex!.Value)
			.Distinct()
			.ToList();
		foreach (var s in customSeasonIndexes)
			await EnsureEpisodeIndicesForSeasonAsync(db, channelId, s, ct);

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

		int? playlistId = null;
		if (seasonIndex != ChannelOnlySeasonIndex)
		{
			playlistId = await db.Playlists.AsNoTracking()
				.Where(p => p.ChannelId == channelId && p.SeasonIndex == seasonIndex)
				.Select(p => (int?)p.Id)
				.OrderBy(p => p)
				.FirstOrDefaultAsync(ct);
		}

		if (playlistId.HasValue && seasonVideos.All(v => v.PlexEpisodeIndex.HasValue && v.PlexEpisodeIndex.Value > 0))
			return;

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

		// Season 01 (channel uploads): keep PlexEpisodeIndex contiguous 1..n in upload order so DB matches NFO and {Playlist Index}.
		if (seasonIndex == ChannelOnlySeasonIndex)
		{
			for (var i = 0; i < orderedVideoIds.Count; i++)
			{
				if (!byId.TryGetValue(orderedVideoIds[i], out var v))
					continue;
				if (v.PlexIndexLocked)
					continue;
				var want = i + 1;
				if (v.PlexEpisodeIndex != want)
					v.PlexEpisodeIndex = want;
			}

			await db.SaveChangesAsync(ct);
			return;
		}

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

