using TubeArr.Backend.Data;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class PlaylistMultiMatchStrategyTests
{
	[Fact]
	public void OrderPlaylistsForFileOrganization_alphabetical_uses_title()
	{
		var max = new Dictionary<int, DateTimeOffset>
		{
			[1] = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
			[2] = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)
		};
		var playlists = new[]
		{
			new PlaylistEntity { Id = 1, ChannelId = 1, YoutubePlaylistId = "PLb", Title = "Beta", Added = DateTimeOffset.UtcNow },
			new PlaylistEntity { Id = 2, ChannelId = 1, YoutubePlaylistId = "PLa", Title = "Alpha", Added = DateTimeOffset.UtcNow }
		};

		var ordered = ChannelDtoMapper.OrderPlaylistsForFileOrganization(playlists, max, PlaylistMultiMatchStrategy.AlphabeticalByTitle);
		Assert.Equal(new[] { 2, 1 }, ordered.Select(p => p.Id).ToArray());
	}

	[Fact]
	public void OrderPlaylistsForFileOrganization_newest_added_orders_by_Added_desc()
	{
		var max = new Dictionary<int, DateTimeOffset>();
		var t0 = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var t1 = new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var playlists = new[]
		{
			new PlaylistEntity { Id = 10, ChannelId = 1, YoutubePlaylistId = "PL1", Title = "Old", Added = t0 },
			new PlaylistEntity { Id = 11, ChannelId = 1, YoutubePlaylistId = "PL2", Title = "New", Added = t1 }
		};

		var ordered = ChannelDtoMapper.OrderPlaylistsForFileOrganization(playlists, max, PlaylistMultiMatchStrategy.NewestPlaylistAdded);
		Assert.Equal(new[] { 11, 10 }, ordered.Select(p => p.Id).ToArray());
	}

	[Fact]
	public void OrderPlaylistsForFileOrganization_alphabetical_respects_priority_first()
	{
		var max = new Dictionary<int, DateTimeOffset>();
		var playlists = new[]
		{
			new PlaylistEntity { Id = 1, ChannelId = 1, YoutubePlaylistId = "PLa", Title = "Alpha", Priority = 2, Added = DateTimeOffset.UtcNow },
			new PlaylistEntity { Id = 2, ChannelId = 1, YoutubePlaylistId = "PLb", Title = "Beta", Priority = 1, Added = DateTimeOffset.UtcNow }
		};

		var ordered = ChannelDtoMapper.OrderPlaylistsForFileOrganization(playlists, max, PlaylistMultiMatchStrategy.AlphabeticalByTitle);
		Assert.Equal(new[] { 2, 1 }, ordered.Select(p => p.Id).ToArray());
	}

	[Fact]
	public void OrderPlaylistsForFileOrganization_lexicographic_applies_strategies_in_order()
	{
		var max = new Dictionary<int, DateTimeOffset>
		{
			[1] = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
			[2] = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
		};
		var t0 = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var playlists = new[]
		{
			new PlaylistEntity { Id = 1, ChannelId = 1, YoutubePlaylistId = "PLb", Title = "Beta", Added = t0 },
			new PlaylistEntity { Id = 2, ChannelId = 1, YoutubePlaylistId = "PLa", Title = "Alpha", Added = t0 }
		};

		var latestFirst = ChannelDtoMapper.OrderPlaylistsForFileOrganization(
			playlists,
			max,
			new[]
			{
				PlaylistMultiMatchStrategy.LatestPlaylistActivity,
				PlaylistMultiMatchStrategy.AlphabeticalByTitle
			});
		Assert.Equal(new[] { 1, 2 }, latestFirst.Select(p => p.Id).ToArray());

		var alphaFirst = ChannelDtoMapper.OrderPlaylistsForFileOrganization(
			playlists,
			max,
			new[]
			{
				PlaylistMultiMatchStrategy.AlphabeticalByTitle,
				PlaylistMultiMatchStrategy.LatestPlaylistActivity
			});
		Assert.Equal(new[] { 2, 1 }, alphaFirst.Select(p => p.Id).ToArray());
	}
}
