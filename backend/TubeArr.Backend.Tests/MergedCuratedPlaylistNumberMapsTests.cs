using TubeArr.Backend.Data;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class MergedCuratedPlaylistNumberMapsTests
{
	[Fact]
	public void Custom_before_Youtube_gets_lower_playlist_number_than_youtube()
	{
		var yt = new[]
		{
			new PlaylistEntity { Id = 177, ChannelId = 1, Priority = 5, Title = "ARK", Added = default }
		};
		var custom = new[]
		{
			new ChannelCustomPlaylistEntity { Id = 1, ChannelId = 1, Priority = 0, Name = "Hermitcraft" }
		};

		var (ytMap, customMap) = ChannelDtoMapper.BuildMergedCuratedPlaylistNumberMaps(yt, custom);

		Assert.Equal(3, ytMap[177]);
		Assert.Equal(2, customMap[1]);
	}

	[Fact]
	public void Youtube_before_custom_matches_sequential_merge()
	{
		var yt = new[]
		{
			new PlaylistEntity { Id = 10, ChannelId = 1, Priority = 0, Title = "A", Added = default }
		};
		var custom = new[]
		{
			new ChannelCustomPlaylistEntity { Id = 20, ChannelId = 1, Priority = 5, Name = "B" }
		};

		var (ytMap, customMap) = ChannelDtoMapper.BuildMergedCuratedPlaylistNumberMaps(yt, custom);

		Assert.Equal(2, ytMap[10]);
		Assert.Equal(3, customMap[20]);
	}
}
