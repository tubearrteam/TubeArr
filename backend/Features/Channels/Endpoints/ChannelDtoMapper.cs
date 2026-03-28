using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class ChannelDtoMapper
{
	/// <summary>Max <see cref="VideoEntity.UploadDateUtc"/> per playlist (videos currently assigned to that playlist).</summary>
	internal static async Task<Dictionary<int, DateTimeOffset>> LoadMaxUploadUtcByPlaylistIdsAsync(
		TubeArrDbContext db,
		IEnumerable<int> playlistIds,
		CancellationToken ct = default)
	{
		var ids = playlistIds.Where(id => id > 0).Distinct().ToList();
		if (ids.Count == 0)
			return new Dictionary<int, DateTimeOffset>();

		// SQLite cannot translate Max(DateTimeOffset) in SQL (see provider limitation).
		var rows = await db.Videos.AsNoTracking()
			.Where(v => v.PlaylistId != null && ids.Contains(v.PlaylistId.Value))
			.Select(v => new { PlaylistId = v.PlaylistId!.Value, v.UploadDateUtc })
			.ToListAsync(ct);

		return rows
			.GroupBy(x => x.PlaylistId)
			.ToDictionary(g => g.Key, g => g.Max(x => x.UploadDateUtc));
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

	internal static ChannelDto CreateChannelDto(ChannelEntity channel, IReadOnlyList<PlaylistEntity> playlists, int videoCount, int videoFileCount = 0, long sizeOnDisk = 0, int? totalVideoCount = null, IReadOnlyDictionary<int, DateTimeOffset>? maxUploadUtcByPlaylistId = null)
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
			TotalVideoCount: totalVideoCount ?? videoCount
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
