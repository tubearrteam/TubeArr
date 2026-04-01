using System;
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

	/// <summary>Order playlists for on-disk path / primary id when a video appears in more than one curated playlist.</summary>
	internal static List<PlaylistEntity> OrderPlaylistsForFileOrganization(
		IReadOnlyList<PlaylistEntity> playlists,
		IReadOnlyDictionary<int, DateTimeOffset> maxUploadUtcByPlaylistId,
		PlaylistMultiMatchStrategy singleStrategy) =>
		OrderPlaylistsForFileOrganization(playlists, maxUploadUtcByPlaylistId, new[] { singleStrategy });

	/// <summary>Same as single-strategy overload, but tie-breakers are applied in the given order (after <see cref="PlaylistEntity.Priority"/>).</summary>
	internal static List<PlaylistEntity> OrderPlaylistsForFileOrganization(
		IReadOnlyList<PlaylistEntity> playlists,
		IReadOnlyDictionary<int, DateTimeOffset> maxUploadUtcByPlaylistId,
		IReadOnlyList<PlaylistMultiMatchStrategy> strategyOrder)
	{
		if (playlists.Count == 0)
			return new List<PlaylistEntity>();

		var order = strategyOrder.Count > 0
			? strategyOrder
			: new[] { PlaylistMultiMatchStrategy.LatestPlaylistActivity };

		var arr = playlists.ToArray();
		Array.Sort(arr, (a, b) => ComparePlaylistsForFileOrganization(a, b, maxUploadUtcByPlaylistId, order));
		return arr.ToList();
	}

	static int ComparePlaylistsForFileOrganization(
		PlaylistEntity a,
		PlaylistEntity b,
		IReadOnlyDictionary<int, DateTimeOffset> maxUpload,
		IReadOnlyList<PlaylistMultiMatchStrategy> strategyOrder)
	{
		var pr = a.Priority.CompareTo(b.Priority);
		if (pr != 0)
			return pr;

		foreach (var s in strategyOrder)
		{
			var c = ComparePlaylistTieBreak(a, b, s, maxUpload);
			if (c != 0)
				return c;
		}

		return a.Id.CompareTo(b.Id);
	}

	static int ComparePlaylistTieBreak(
		PlaylistEntity a,
		PlaylistEntity b,
		PlaylistMultiMatchStrategy strategy,
		IReadOnlyDictionary<int, DateTimeOffset> maxUpload)
	{
		switch (strategy)
		{
			case PlaylistMultiMatchStrategy.AlphabeticalByTitle:
			{
				var ct = string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
				if (ct != 0)
					return ct;
				return a.Id.CompareTo(b.Id);
			}
			case PlaylistMultiMatchStrategy.NewestPlaylistAdded:
			{
				var cd = b.Added.CompareTo(a.Added);
				if (cd != 0)
					return cd;
				return a.Id.CompareTo(b.Id);
			}
			case PlaylistMultiMatchStrategy.OldestPlaylistAdded:
			{
				var cd = a.Added.CompareTo(b.Added);
				if (cd != 0)
					return cd;
				return a.Id.CompareTo(b.Id);
			}
			default:
			{
				var ma = maxUpload.GetValueOrDefault(a.Id, DateTimeOffset.MinValue);
				var mb = maxUpload.GetValueOrDefault(b.Id, DateTimeOffset.MinValue);
				var cd = mb.CompareTo(ma);
				if (cd != 0)
					return cd;
				return string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
			}
		}
	}

	internal static string? NormalizePlaylistMultiMatchStrategyOrder(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw) || raw.Length != 4)
			return null;

		var seen = new HashSet<char>();
		foreach (var ch in raw)
		{
			if (ch is < '0' or > '3')
				return null;
			if (!seen.Add(ch))
				return null;
		}

		return seen.Count == 4 ? raw : null;
	}

	internal static string DerivePlaylistMultiMatchStrategyOrderFromLegacy(int legacy)
	{
		var first = legacy is >= 0 and <= 3 ? legacy : 0;
		var rest = Enumerable.Range(0, 4).Where(x => x != first).OrderBy(x => x);
		return string.Concat(new[] { first }.Concat(rest).Select(x => x.ToString(CultureInfo.InvariantCulture)));
	}

	internal static IReadOnlyList<PlaylistMultiMatchStrategy> ParsePlaylistMultiMatchStrategyOrder(string? order, int legacyFallback)
	{
		var normalized = NormalizePlaylistMultiMatchStrategyOrder(order);
		if (normalized is null)
			normalized = DerivePlaylistMultiMatchStrategyOrderFromLegacy(legacyFallback);

		return normalized.Select(ch => (PlaylistMultiMatchStrategy)(ch - '0')).ToList();
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

		var orderedPlaylistIds = await LoadOrderedPlaylistIdsForFileOrganizationAsync(db, channelId, ct);
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

	/// <summary>
	/// Resolves primary playlist per video for many channels in one round-trip (playlist rows, max upload, playlist-videos).
	/// </summary>
	internal static async Task<Dictionary<int, int?>> LoadPrimaryPlaylistIdByVideoIdsBatchedAsync(
		TubeArrDbContext db,
		IReadOnlyDictionary<int, IReadOnlyCollection<int>> videoIdsByChannelId,
		CancellationToken ct = default)
	{
		var normalized = new Dictionary<int, List<int>>();
		foreach (var kv in videoIdsByChannelId)
		{
			var list = kv.Value.Where(id => id > 0).Distinct().ToList();
			if (list.Count > 0)
				normalized[kv.Key] = list;
		}

		var result = new Dictionary<int, int?>();
		foreach (var kv in normalized)
		{
			foreach (var vid in kv.Value)
				result[vid] = null;
		}

		if (normalized.Count == 0)
			return result;

		var channelIds = normalized.Keys.Where(id => id > 0).Distinct().ToList();
		var playlists = await db.Playlists.AsNoTracking()
			.Where(p => channelIds.Contains(p.ChannelId))
			.ToListAsync(ct);

		var maxUploadByPlaylistId = await LoadMaxUploadUtcByPlaylistIdsAsync(db, playlists.Select(p => p.Id), ct);

		var channelOrderRows = await db.Channels.AsNoTracking()
			.Where(c => channelIds.Contains(c.Id))
			.Select(c => new { c.Id, c.PlaylistMultiMatchStrategy, c.PlaylistMultiMatchStrategyOrder })
			.ToDictionaryAsync(c => c.Id, c => c, ct);

		var orderedPlaylistIdsByChannel = new Dictionary<int, List<int>>();
		foreach (var channelId in channelIds)
		{
			var forChannel = playlists.Where(p => p.ChannelId == channelId).ToList();
			var row = channelOrderRows.GetValueOrDefault(channelId);
			var strategyOrder = row is null
				? ParsePlaylistMultiMatchStrategyOrder(null, 0)
				: ParsePlaylistMultiMatchStrategyOrder(row.PlaylistMultiMatchStrategyOrder, row.PlaylistMultiMatchStrategy);
			var ordered = OrderPlaylistsForFileOrganization(forChannel, maxUploadByPlaylistId, strategyOrder);
			orderedPlaylistIdsByChannel[channelId] = ordered.Select(p => p.Id).ToList();
		}

		var allVideoIds = normalized.Values.SelectMany(v => v).ToList();
		var pvRows = await db.PlaylistVideos.AsNoTracking()
			.Where(pv => allVideoIds.Contains(pv.VideoId))
			.Select(pv => new { pv.VideoId, pv.PlaylistId })
			.ToListAsync(ct);

		var playlistIdsByVideoId = pvRows
			.GroupBy(x => x.VideoId)
			.ToDictionary(g => g.Key, g => g.Select(x => x.PlaylistId).ToHashSet());

		foreach (var (channelId, videoIds) in normalized)
		{
			if (!orderedPlaylistIdsByChannel.TryGetValue(channelId, out var orderedIds))
				orderedIds = new List<int>();

			foreach (var videoId in videoIds)
			{
				if (!playlistIdsByVideoId.TryGetValue(videoId, out var containing))
					continue;

				result[videoId] = ResolvePrimaryPlaylistIdForVideo(containing, orderedIds);
			}
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

	/// <summary>Min <see cref="VideoEntity.UploadDateUtc"/> per channel (all videos on the channel).</summary>
	internal static async Task<Dictionary<int, DateTimeOffset>> LoadMinUploadUtcByChannelIdsAsync(
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
			.ToDictionary(g => g.Key, g => g.Min(x => x.UploadDateUtc));
	}

	/// <summary>Min <see cref="VideoEntity.UploadDateUtc"/> per playlist (via <see cref="PlaylistVideoEntity"/>).</summary>
	internal static async Task<Dictionary<int, DateTimeOffset>> LoadMinUploadUtcByPlaylistIdsAsync(
		TubeArrDbContext db,
		IEnumerable<int> playlistIds,
		CancellationToken ct = default)
	{
		var ids = playlistIds.Where(id => id > 0).Distinct().ToList();
		if (ids.Count == 0)
			return new Dictionary<int, DateTimeOffset>();

		var rows = await (
			from pv in db.PlaylistVideos.AsNoTracking()
			join v in db.Videos.AsNoTracking() on pv.VideoId equals v.Id
			where ids.Contains(pv.PlaylistId)
			select new { pv.PlaylistId, v.UploadDateUtc }
		).ToListAsync(ct);

		return rows
			.GroupBy(x => x.PlaylistId)
			.ToDictionary(g => g.Key, g => g.Min(x => x.UploadDateUtc));
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

	/// <summary>Curated playlists: <see cref="PlaylistEntity.Priority"/> first (ascending), then newest activity (latest video upload in the playlist), then title.</summary>
	internal static List<PlaylistEntity> OrderPlaylistsByLatestUpload(
		IReadOnlyList<PlaylistEntity> playlists,
		IReadOnlyDictionary<int, DateTimeOffset> maxUploadUtcByPlaylistId)
	{
		if (playlists.Count == 0)
			return new List<PlaylistEntity>();

		return playlists
			.OrderBy(p => p.Priority)
			.ThenByDescending(p => maxUploadUtcByPlaylistId.GetValueOrDefault(p.Id, DateTimeOffset.MinValue))
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

	/// <summary>Playlist order for file paths and primary playlist id (respects multi-match strategy order).</summary>
	internal static async Task<List<int>> LoadOrderedPlaylistIdsForFileOrganizationAsync(
		TubeArrDbContext db,
		int channelId,
		CancellationToken ct = default)
	{
		var ch = await db.Channels.AsNoTracking()
			.Where(c => c.Id == channelId)
			.Select(c => new { c.PlaylistMultiMatchStrategy, c.PlaylistMultiMatchStrategyOrder })
			.FirstOrDefaultAsync(ct);
		var strategyOrder = ParsePlaylistMultiMatchStrategyOrder(ch?.PlaylistMultiMatchStrategyOrder, ch?.PlaylistMultiMatchStrategy ?? 0);

		var playlists = await db.Playlists.AsNoTracking()
			.Where(p => p.ChannelId == channelId)
			.ToListAsync(ct);
		if (playlists.Count == 0)
			return new List<int>();

		var maxUpload = await LoadMaxUploadUtcByPlaylistIdsAsync(db, playlists.Select(p => p.Id), ct);
		var ordered = OrderPlaylistsForFileOrganization(playlists, maxUpload, strategyOrder);
		return ordered.Select(p => p.Id).ToList();
	}

	/// <summary>
	/// UI playlist numbers 2+ for curated rows, matching <see cref="CreateChannelDto"/> interleaving of
	/// YouTube playlists and <see cref="ChannelCustomPlaylistEntity"/> by priority (then id).
	/// </summary>
	internal static (Dictionary<int, int> YoutubePlaylistIdToNumber, Dictionary<int, int> CustomPlaylistIdToNumber)
		BuildMergedCuratedPlaylistNumberMaps(
			IReadOnlyList<PlaylistEntity> ytSorted,
			IReadOnlyList<ChannelCustomPlaylistEntity> customPlaylists)
	{
		var ytMap = new Dictionary<int, int>();
		var customMap = new Dictionary<int, int>();
		var customOrdered = customPlaylists.OrderBy(c => c.Priority).ThenBy(c => c.Id).ToList();
		if (ytSorted.Count == 0 && customOrdered.Count == 0)
			return (ytMap, customMap);

		var merged = new List<(bool IsCustom, PlaylistEntity? Yt, ChannelCustomPlaylistEntity? Cust)>();
		var yi = 0;
		var ci = 0;
		while (yi < ytSorted.Count && ci < customOrdered.Count)
		{
			var y = ytSorted[yi];
			var c = customOrdered[ci];
			if (y.Priority < c.Priority || y.Priority == c.Priority)
			{
				merged.Add((false, y, null));
				yi++;
			}
			else
			{
				merged.Add((true, null, c));
				ci++;
			}
		}

		while (yi < ytSorted.Count)
			merged.Add((false, ytSorted[yi++], null));
		while (ci < customOrdered.Count)
			merged.Add((true, null, customOrdered[ci++]));

		var num = 2;
		foreach (var row in merged)
		{
			if (!row.IsCustom && row.Yt is { } p)
				ytMap[p.Id] = num++;
			else if (row.IsCustom && row.Cust is { } c)
				customMap[c.Id] = num++;
		}

		return (ytMap, customMap);
	}

	internal static ChannelDto CreateChannelDto(
		ChannelEntity channel,
		IReadOnlyList<PlaylistEntity> playlists,
		IReadOnlyList<ChannelCustomPlaylistEntity> customPlaylists,
		int videoCount,
		int videoFileCount = 0,
		long sizeOnDisk = 0,
		int? totalVideoCount = null,
		IReadOnlyDictionary<int, DateTimeOffset>? maxUploadUtcByPlaylistId = null,
		DateTimeOffset? lastUploadUtc = null,
		DateTimeOffset? firstUploadUtc = null)
	{
		var title = channel.Title;
		var titleSlug = string.IsNullOrWhiteSpace(channel.TitleSlug) ? SlugHelper.Slugify(title) : channel.TitleSlug;
		var ytSorted = maxUploadUtcByPlaylistId is not null
			? OrderPlaylistsByLatestUpload(playlists.ToList(), maxUploadUtcByPlaylistId)
			: playlists.OrderBy(p => p.Priority).ThenBy(p => p.Id).ToList();

		var customOrdered = customPlaylists.OrderBy(c => c.Priority).ThenBy(c => c.Id).ToList();

		var merged = new List<(bool IsCustom, PlaylistEntity? Yt, ChannelCustomPlaylistEntity? Cust)>();
		var yi = 0;
		var ci = 0;
		while (yi < ytSorted.Count && ci < customOrdered.Count)
		{
			var y = ytSorted[yi];
			var c = customOrdered[ci];
			if (y.Priority < c.Priority || y.Priority == c.Priority)
			{
				merged.Add((false, y, null));
				yi++;
			}
			else
			{
				merged.Add((true, null, c));
				ci++;
			}
		}

		while (yi < ytSorted.Count)
			merged.Add((false, ytSorted[yi++], null));
		while (ci < customOrdered.Count)
			merged.Add((true, null, customOrdered[ci++]));

		var playlistDtos = new List<PlaylistDto>();
		playlistDtos.Add(new PlaylistDto(PlaylistNumber: 1, Title: "Videos", Monitored: true, IsCustom: false, CustomPlaylistId: null, PlaylistId: null, Priority: 0));
		var num = 2;
		var customDetailDtos = new List<ChannelCustomPlaylistDto>();
		foreach (var row in merged)
		{
			if (!row.IsCustom && row.Yt is { } p)
			{
				playlistDtos.Add(new PlaylistDto(PlaylistNumber: num++, Title: p.Title, Monitored: p.Monitored, IsCustom: false, CustomPlaylistId: null, PlaylistId: p.Id, Priority: p.Priority));
			}
			else if (row.IsCustom && row.Cust is { } c)
			{
				var plNum = num++;
				playlistDtos.Add(new PlaylistDto(PlaylistNumber: plNum, Title: c.Name, Monitored: c.Enabled, IsCustom: true, CustomPlaylistId: c.Id, PlaylistId: null, Priority: c.Priority));
				customDetailDtos.Add(ToChannelCustomPlaylistDto(c, plNum));
			}
		}

		var playlistCount = merged.Count + 1;
		var statistics = new ChannelStatisticsDto(
			VideoCount: videoCount,
			VideoFileCount: videoFileCount,
			PlaylistCount: playlistCount,
			SizeOnDisk: sizeOnDisk,
			TotalVideoCount: totalVideoCount ?? videoCount,
			LastUploadUtc: lastUploadUtc,
			FirstUploadUtc: firstUploadUtc
		);

		var orderOut = NormalizePlaylistMultiMatchStrategyOrder(channel.PlaylistMultiMatchStrategyOrder)
			?? DerivePlaylistMultiMatchStrategyOrderFromLegacy(channel.PlaylistMultiMatchStrategy);

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
			PlaylistMultiMatchStrategy: channel.PlaylistMultiMatchStrategy,
			PlaylistMultiMatchStrategyOrder: orderOut,
			ChannelType: channel.ChannelType,
			RoundRobinLatestVideoCount: channel.RoundRobinLatestVideoCount,
			FilterOutShorts: channel.FilterOutShorts,
			FilterOutLivestreams: channel.FilterOutLivestreams,
			HasShortsTab: channel.HasShortsTab,
			HasStreamsTab: channel.HasStreamsTab,
			MonitorPreset: channel.MonitorPreset,
			Statistics: statistics,
			CustomPlaylists: customDetailDtos.ToArray()
		);
	}

	static ChannelCustomPlaylistDto ToChannelCustomPlaylistDto(ChannelCustomPlaylistEntity e, int playlistNumber)
	{
		var rules = ChannelCustomPlaylistRulesHelper.ParseRules(e.RulesJson);
		var ruleDtos = rules
			.Select(r => new ChannelCustomPlaylistRuleDto(r.Field, r.Operator, r.Value))
			.ToArray();
		return new ChannelCustomPlaylistDto(
			e.Id,
			e.ChannelId,
			e.Name,
			e.Enabled,
			e.Priority,
			e.MatchType,
			ruleDtos,
			playlistNumber);
	}
}
