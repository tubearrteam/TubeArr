using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Data;
using TubeArr.Backend.Media;

namespace TubeArr.Backend.Media.Nfo;

internal static class NfoPlaylistEpisodeResolver
{
	/// <summary>
	/// 1-based episode index: <see cref="PlaylistVideoEntity.Position"/> order when present, else upload date then id
	/// (matches stable playlist ordering).
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

		// Prefer stored stable numbering.
		var v2 = await db.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == videoId, ct);
		if (v2?.PlexEpisodeIndex is { } ep && ep > 0)
			return ep;

		if (!primaryPlaylistId.HasValue)
			return await ResolveEpisodeForChannelOnlyVideosAsync(db, videoId, ct);

		var rows = await (
			from pv in db.PlaylistVideos.AsNoTracking()
			join v in db.Videos.AsNoTracking() on pv.VideoId equals v.Id
			where pv.PlaylistId == primaryPlaylistId.Value
			select new { pv.VideoId, pv.Position, v.UploadDateUtc }
		).ToListAsync(ct);

		if (rows.Count == 0)
			return 1;

		var ordered = rows
			.OrderBy(x => x.Position ?? int.MaxValue)
			.ThenBy(x => x.UploadDateUtc)
			.ThenBy(x => x.VideoId)
			.ToList();

		for (var i = 0; i < ordered.Count; i++)
		{
			if (ordered[i].VideoId == videoId)
				return i + 1;
		}

		return 1;
	}

	static async Task<int> ResolveEpisodeForChannelOnlyVideosAsync(TubeArrDbContext db, int videoId, CancellationToken ct)
	{
		var channelId = await db.Videos.AsNoTracking()
			.Where(v => v.Id == videoId)
			.Select(v => v.ChannelId)
			.FirstOrDefaultAsync(ct);
		if (channelId == 0)
			return 1;

		// No curated playlist: same ordering as channel-wide "Videos" — oldest upload first, then id.
		// SQLite provider doesn't support ordering by DateTimeOffset server-side reliably; order in-memory.
		var rows = await db.Videos.AsNoTracking()
			.Where(v => v.ChannelId == channelId)
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
