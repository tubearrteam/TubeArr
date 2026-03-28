using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class ChannelDtoMapper
{
	/// <summary>Max <see cref="VideoEntity.UploadDateUtc"/> per playlist (videos in <see cref="PlaylistVideoEntity"/> for that playlist).</summary>
	internal static async Task<Dictionary<int, DateTimeOffset>> LoadMaxUploadUtcByPlaylistIdsAsync(
		TubeArrDbContext db,
		IEnumerable<int> playlistIds,
		CancellationToken ct = default)
	{
		var ids = playlistIds.Where(id => id > 0).Distinct().ToList();
		if (ids.Count == 0)
			return new Dictionary<int, DateTimeOffset>();

		// SQLite cannot translate Max(DateTimeOffset) in SQL (see provider limitation).
		var rows = await (
			from pv in db.PlaylistVideos.AsNoTracking()
			join v in db.Videos.AsNoTracking() on pv.VideoId equals v.Id
			where ids.Contains(pv.PlaylistId)
			select new { pv.PlaylistId, v.UploadDateUtc }
		).ToListAsync(ct);

		return rows
			.GroupBy(x => x.PlaylistId)
			.ToDictionary(g => g.Key, g => g.Max(x => x.UploadDateUtc));
	}

	/// <summary>Display/path playlist: first id in <paramref name="orderedPlaylistIds"/> that contains the video.</summary>
	internal static int? ResolvePrimaryPlaylistIdForVideo(
		IReadOnlyCollection<int> playlistIdsContainingVideo,
		IReadOnlyList<int> orderedPlaylistIds)
	{
		if (playlistIdsContainingVideo.Count == 0)
			return null;

		var set = playlistIdsContainingVideo as HashSet<int> ?? playlistIdsContainingVideo.ToHashSet();
		foreach (var oid in orderedPlaylistIds)
		{
			if (set.Contains(oid))
				return oid;
		}

		return null;
	}

	internal static async Task<Dictionary<int, int?>> LoadPrimaryPlaylistIdByVideoIdsForChannelAsync(
		TubeArrDbContext db,
		int channelId,
		IReadOnlyCollection<int> videoIds,
		CancellationToken ct = default)
	{
		var list = videoIds.Where(id => id > 0).Distinct().ToList();
		var result = list.ToDictionary(id => id, _ => (int?)null);
		if (list.Count == 0)
			return result;

		var orderedPlaylistIds = await LoadOrderedPlaylistIdsForChannelAsync(db, channelId, ct);
		var pvRows = await db.PlaylistVideos.AsNoTracking()
			.Where(pv => list.Contains(pv.VideoId))
			.Select(pv => new { pv.VideoId, pv.PlaylistId })
			.ToListAsync(ct);

		foreach (var g in pvRows.GroupBy(x => x.VideoId))
		{
			var set = g.Select(x => x.PlaylistId).ToHashSet();
			result[g.Key] = ResolvePrimaryPlaylistIdForVideo(set, orderedPlaylistIds);
		}

		return result;
	}

	internal static async Task<int?> GetPrimaryPlaylistIdForVideoAsync(
		TubeArrDbContext db,
		int channelId,
		int videoId,
		CancellationToken ct = default)
	{
		var map = await LoadPrimaryPlaylistIdByVideoIdsForChannelAsync(db, channelId, [videoId], ct);
		return map.GetValueOrDefault(videoId);
	}

	/// <summary>Max <see cref="VideoEntity.UploadDateUtc"/> per channel (all videos on the channel).</summary>
	internal static async Task<Dictionary<int, DateTimeOffset>> LoadMaxUploadUtcByChannelIdsAsync(
		TubeArrDbContext db,
		IEnumerable<int> channelIds,
		CancellationToken ct = default)
	{
		var ids = channelIds.Where(id => id > 0).Distinct().ToList();
		if (ids.Count == 0)
			return new Dictionary<int, DateTimeOffset>();

		var rows = await db.Videos.AsNoTracking()
			.Where(v => ids.Contains(v.ChannelId))
			.Select(v => new { v.ChannelId, v.UploadDateUtc })
			.ToListAsync(ct);

		return rows
			.GroupBy(x => x.ChannelId)
			.ToDictionary(g => g.Key, g => g.Max(x => x.UploadDateUtc));
	}

	/// <summary>
	/// Oldest air date per channel for &quot;Active Since&quot;: only videos with a non-blank air date participate
	/// (see <see cref="TryGetAirDateUtcForActiveSince"/>). Upload-only rows are ignored.
	/// </summary>
	internal static async Task<Dictionary<int, DateTimeOffset>> LoadMinActiveSinceUtcByChannelIdsAsync(
		TubeArrDbContext db,
		IEnumerable<int> channelIds,
		CancellationToken ct = default)
	{
		var ids = channelIds.Where(id => id > 0).Distinct().ToList();
		if (ids.Count == 0)
			return new Dictionary<int, DateTimeOffset>();

		var rows = await db.Videos.AsNoTracking()
			.Where(v => ids.Contains(v.ChannelId))
			.Select(v => new { v.ChannelId, v.AirDateUtc, v.AirDate })
			.ToListAsync(ct);

		var result = new Dictionary<int, DateTimeOffset>();
		foreach (var g in rows.GroupBy(x => x.ChannelId))
		{
			DateTimeOffset? min = null;
			foreach (var x in g)
			{
				if (!TryGetAirDateUtcForActiveSince(x.AirDateUtc, x.AirDate, out var utc))
					continue;
				if (min is null || utc < min)
					min = utc;
			}

			if (min is not null)
				result[g.Key] = min.Value;
		}

		return result;
	}

	/// <returns>
	/// True when the row has a real air date (not default/Unix epoch sentinel and not empty string).
	/// Matches the idea of &quot;blank&quot; used in <see cref="VideoEndpoints.CreateVideoDto"/>.
	/// </returns>
	private static bool TryGetAirDateUtcForActiveSince(
		DateTimeOffset airDateUtc,
		string? airDate,
		out DateTimeOffset effectiveUtc)
	{
		effectiveUtc = default;
		if (airDateUtc != default && airDateUtc != DateTimeOffset.UnixEpoch)
		{
			effectiveUtc = airDateUtc;
			return true;
		}

		if (string.IsNullOrWhiteSpace(airDate))
			return false;

		if (DateTimeOffset.TryParse(airDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
		{
			effectiveUtc = dto;
			return true;
		}

		if (DateTime.TryParse(airDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
		{
			effectiveUtc = new DateTimeOffset(DateTime.SpecifyKind(dt.ToUniversalTime(), DateTimeKind.Utc));
			return true;
		}

		return false;
	}

	/// <summary>Curated playlists: newest activity first (latest video upload in the playlist), then title.</summary>
	internal static List<PlaylistEntity> OrderPlaylistsByLatestUpload(
		IReadOnlyList<PlaylistEntity> playlists,
		IReadOnlyDictionary<int, DateTimeOffset> maxUploadUtcByPlaylistId)
	{
		if (playlists.Count == 0)
			return new List<PlaylistEntity>();

		return playlists
			.OrderByDescending(p => maxUploadUtcByPlaylistId.GetValueOrDefault(p.Id, DateTimeOffset.MinValue))
			.ThenBy(p => p.Title, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	internal static async Task<List<PlaylistEntity>> LoadPlaylistsOrderedByLatestUploadAsync(
		TubeArrDbContext db,
		int channelId,
		CancellationToken ct = default)
	{
		var playlists = await db.Playlists.AsNoTracking()
			.Where(p => p.ChannelId == channelId)
			.ToListAsync(ct);
		if (playlists.Count == 0)
			return playlists;

		var maxUpload = await LoadMaxUploadUtcByPlaylistIdsAsync(db, playlists.Select(p => p.Id), ct);
		return OrderPlaylistsByLatestUpload(playlists, maxUpload);
	}

	internal static async Task<List<int>> LoadOrderedPlaylistIdsForChannelAsync(
		TubeArrDbContext db,
		int channelId,
		CancellationToken ct = default)
	{
		var ordered = await LoadPlaylistsOrderedByLatestUploadAsync(db, channelId, ct);
		return ordered.Select(p => p.Id).ToList();
	}

	internal static ChannelDto CreateChannelDto(ChannelEntity channel, IReadOnlyList<PlaylistEntity> playlists, int videoCount, int videoFileCount = 0, long sizeOnDisk = 0, int? totalVideoCount = null, IReadOnlyDictionary<int, DateTimeOffset>? maxUploadUtcByPlaylistId = null, DateTimeOffset? lastUploadUtc = null, DateTimeOffset? firstUploadUtc = null)
	{
		var title = channel.Title;
		var titleSlug = string.IsNullOrWhiteSpace(channel.TitleSlug) ? SlugHelper.Slugify(title) : channel.TitleSlug;
		var orderedPlaylists = maxUploadUtcByPlaylistId is not null
			? OrderPlaylistsByLatestUpload(playlists, maxUploadUtcByPlaylistId)
			: playlists.OrderBy(p => p.Id).ToList();

		var playlistDtos = new List<PlaylistDto>();
		playlistDtos.Add(new PlaylistDto(PlaylistNumber: 1, Title: "Videos", Monitored: true));
		var num = 2;
		foreach (var p in orderedPlaylists)
		{
			playlistDtos.Add(new PlaylistDto(PlaylistNumber: num++, Title: p.Title, Monitored: p.Monitored));
		}

		var playlistCount = orderedPlaylists.Count + 1;
		var statistics = new ChannelStatisticsDto(
			VideoCount: videoCount,
			VideoFileCount: videoFileCount,
			PlaylistCount: playlistCount,
			SizeOnDisk: sizeOnDisk,
			TotalVideoCount: totalVideoCount ?? videoCount,
			LastUploadUtc: lastUploadUtc,
			FirstUploadUtc: firstUploadUtc
		);

		return new ChannelDto(
			Id: channel.Id,
			YoutubeChannelId: channel.YoutubeChannelId,
			Title: title,
			TitleSlug: titleSlug,
			Description: channel.Description,
			ThumbnailUrl: channel.ThumbnailUrl,
			BannerUrl: channel.BannerUrl,
			Monitored: channel.Monitored,
			Added: channel.Added,
			QualityProfileId: channel.QualityProfileId ?? 0,
			Playlists: playlistDtos.ToArray(),
			Path: channel.Path,
			RootFolderPath: channel.RootFolderPath,
			Tags: channel.Tags,
			MonitorNewItems: channel.MonitorNewItems,
			PlaylistFolder: channel.PlaylistFolder,
			ChannelType: channel.ChannelType,
			RoundRobinLatestVideoCount: channel.RoundRobinLatestVideoCount,
			FilterOutShorts: channel.FilterOutShorts,
			FilterOutLivestreams: channel.FilterOutLivestreams,
			HasShortsTab: channel.HasShortsTab,
			MonitorPreset: channel.MonitorPreset,
			Statistics: statistics
		);
	}
}
