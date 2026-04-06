using Microsoft.EntityFrameworkCore;
using TubeArr.Backend;
using TubeArr.Backend.Data;
using TubeArr.Backend.Media;

namespace TubeArr.Backend.Media.Nfo;

internal static class NfoPlaylistEpisodeResolver
{
	/// <summary>
	/// 1-based episode index for NFO and <c>{Playlist Index}</c>. Uses the same ordering as <see cref="StableTvNumbering"/>
	/// (playlist rows: position, added-at, upload date, id, with duplicate <see cref="PlaylistVideoEntity"/> rows collapsed
	/// like <c>Distinct()</c> on video id). Does not trust <see cref="VideoEntity.PlexEpisodeIndex"/> when unlocked — that
	/// value can stay stale after membership changes because <see cref="StableTvNumbering.EnsureVideoPlexIndicesAsync"/>
	/// may no-op once every row has indices. <see cref="VideoEntity.PlexIndexLocked"/> still wins so manual overrides hold.
	/// Rule-based custom playlists (<see cref="NfoLibraryExporter.CustomPlaylistSeasonRangeStart"/>+1, …): same order as
	/// <see cref="StableTvNumbering.EnsureEpisodeIndicesForSeasonAsync"/> when no native playlist maps to that season —
	/// upload date then id for all videos sharing that <see cref="VideoEntity.PlexSeasonIndex"/>, not native primary playlist order.
	/// Channel-only (no primary playlist): season 01 scope — <see cref="VideoEntity.PlexSeasonIndex"/> ==
	/// <see cref="StableTvNumbering.ChannelOnlySeasonIndex"/>, upload date then id.
	/// </summary>
	internal static async Task<int> ResolveEpisodeNumberAsync(
		TubeArrDbContext db,
		int? primaryPlaylistId,
		int videoId,
		CancellationToken ct)
	{
		var video = await db.Videos.FirstOrDefaultAsync(v => v.Id == videoId, ct);
		if (video is null)
			return 1;

		await StableTvNumbering.EnsureVideoPlexIndicesAsync(db, video.ChannelId, [videoId], ct);

		var placed = await db.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == videoId, ct);
		if (placed?.PlexIndexLocked == true && placed.PlexEpisodeIndex is { } lockedEp && lockedEp > 0)
			return lockedEp;

		if (placed?.PlexSeasonIndex is { } customSeason && customSeason > NfoLibraryExporter.CustomPlaylistSeasonRangeStart)
			return await ResolveEpisodeForCustomSeasonVideosAsync(db, videoId, customSeason, ct);

		if (!primaryPlaylistId.HasValue)
			return await ResolveEpisodeForChannelOnlyVideosAsync(db, videoId, ct);

		var rows = await (
			from pv in db.PlaylistVideos.AsNoTracking()
			join v in db.Videos.AsNoTracking() on pv.VideoId equals v.Id
			where pv.PlaylistId == primaryPlaylistId.Value
			select new { pv.VideoId, pv.Position, pv.AddedAt, v.UploadDateUtc }
		).ToListAsync(ct);

		if (rows.Count == 0)
			return 1;

		var orderedIds = rows
			.OrderBy(x => x.Position ?? int.MaxValue)
			.ThenBy(x => x.AddedAt ?? DateTimeOffset.MaxValue)
			.ThenBy(x => x.UploadDateUtc)
			.ThenBy(x => x.VideoId)
			.Select(x => x.VideoId)
			.Distinct()
			.ToList();

		return Index1(orderedIds, videoId);
	}

	/// <summary>
	/// Custom playlist seasons have no <see cref="PlaylistEntity.SeasonIndex"/> row; stable numbering orders episodes by upload date then id.
	/// </summary>
	static async Task<int> ResolveEpisodeForCustomSeasonVideosAsync(TubeArrDbContext db, int videoId, int seasonIndex, CancellationToken ct)
	{
		var channelId = await db.Videos.AsNoTracking()
			.Where(v => v.Id == videoId)
			.Select(v => v.ChannelId)
			.FirstOrDefaultAsync(ct);
		if (channelId == 0)
			return 1;

		var rows = await db.Videos.AsNoTracking()
			.Where(v => v.ChannelId == channelId && v.PlexSeasonIndex == seasonIndex)
			.Select(v => new { v.Id, v.UploadDateUtc })
			.ToListAsync(ct);

		var list = rows
			.OrderBy(v => v.UploadDateUtc)
			.ThenBy(v => v.Id)
			.Select(v => v.Id)
			.ToList();

		return Index1(list, videoId);
	}

	static async Task<int> ResolveEpisodeForChannelOnlyVideosAsync(TubeArrDbContext db, int videoId, CancellationToken ct)
	{
		var channelId = await db.Videos.AsNoTracking()
			.Where(v => v.Id == videoId)
			.Select(v => v.ChannelId)
			.FirstOrDefaultAsync(ct);
		if (channelId == 0)
			return 1;

		var allIds = await db.Videos.AsNoTracking()
			.Where(v => v.ChannelId == channelId)
			.Select(v => v.Id)
			.ToListAsync(ct);
		await StableTvNumbering.EnsureVideoPlexIndicesAsync(db, channelId, allIds, ct);

		var rows = await db.Videos.AsNoTracking()
			.Where(v => v.ChannelId == channelId && v.PlexSeasonIndex == StableTvNumbering.ChannelOnlySeasonIndex)
			.Select(v => new { v.Id, v.UploadDateUtc })
			.ToListAsync(ct);

		var list = rows
			.OrderBy(v => v.UploadDateUtc)
			.ThenBy(v => v.Id)
			.Select(v => v.Id)
			.ToList();

		return Index1(list, videoId);
	}

	static int Index1(IReadOnlyList<int> orderedIds, int videoId)
	{
		for (var i = 0; i < orderedIds.Count; i++)
		{
			if (orderedIds[i] == videoId)
				return i + 1;
		}

		return 1;
	}
}
