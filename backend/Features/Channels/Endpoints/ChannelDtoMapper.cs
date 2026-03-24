using System.Collections.Generic;
using System.Linq;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class ChannelDtoMapper
{
	internal static ChannelDto CreateChannelDto(ChannelEntity channel, IReadOnlyList<PlaylistEntity> playlists, int videoCount, int videoFileCount = 0, long sizeOnDisk = 0, int? totalVideoCount = null)
	{
		var title = channel.Title;
		var titleSlug = string.IsNullOrWhiteSpace(channel.TitleSlug) ? SlugHelper.Slugify(title) : channel.TitleSlug;
		var orderedPlaylists = playlists.OrderBy(p => p.Id).ToList();

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
			Statistics: statistics
		);
	}
}
