using Xunit;
using TubeArr.Backend;

namespace TubeArr.Backend.Tests;

public sealed class ChannelVideoDiscoveryServiceTests
{
	[Fact]
	public void ShortsFirstPageMirrorsVideosTab_false_when_either_empty()
	{
		Assert.False(ChannelVideoDiscoveryService.ShortsFirstPageMirrorsVideosTab(
			Array.Empty<ChannelVideoDiscoveryItem>(),
			new[] { new ChannelVideoDiscoveryItem("abc", null, null, null) }));
		Assert.False(ChannelVideoDiscoveryService.ShortsFirstPageMirrorsVideosTab(
			new[] { new ChannelVideoDiscoveryItem("abc", null, null, null) },
			Array.Empty<ChannelVideoDiscoveryItem>()));
	}

	[Fact]
	public void ShortsFirstPageMirrorsVideosTab_false_when_counts_differ()
	{
		var a = new[] { new ChannelVideoDiscoveryItem("x", null, null, null) };
		var b = new[]
		{
			new ChannelVideoDiscoveryItem("x", null, null, null),
			new ChannelVideoDiscoveryItem("y", null, null, null)
		};
		Assert.False(ChannelVideoDiscoveryService.ShortsFirstPageMirrorsVideosTab(a, b));
	}

	[Fact]
	public void ShortsFirstPageMirrorsVideosTab_true_when_same_ids_same_order()
	{
		var a = new[]
		{
			new ChannelVideoDiscoveryItem("dQw4w9WgXcQ", null, null, null),
			new ChannelVideoDiscoveryItem("abc123", null, null, null)
		};
		var b = new[]
		{
			new ChannelVideoDiscoveryItem("dQw4w9WgXcQ", null, null, null),
			new ChannelVideoDiscoveryItem("abc123", null, null, null)
		};
		Assert.True(ChannelVideoDiscoveryService.ShortsFirstPageMirrorsVideosTab(a, b));
	}

	[Fact]
	public void ShortsFirstPageMirrorsVideosTab_false_when_order_differs()
	{
		var a = new[]
		{
			new ChannelVideoDiscoveryItem("a", null, null, null),
			new ChannelVideoDiscoveryItem("b", null, null, null)
		};
		var b = new[]
		{
			new ChannelVideoDiscoveryItem("b", null, null, null),
			new ChannelVideoDiscoveryItem("a", null, null, null)
		};
		Assert.False(ChannelVideoDiscoveryService.ShortsFirstPageMirrorsVideosTab(a, b));
	}

	[Fact]
	public void ShortsFirstPageMirrorsVideosTab_case_insensitive_ids()
	{
		var a = new[] { new ChannelVideoDiscoveryItem("AbC", null, null, null) };
		var b = new[] { new ChannelVideoDiscoveryItem("abc", null, null, null) };
		Assert.True(ChannelVideoDiscoveryService.ShortsFirstPageMirrorsVideosTab(a, b));
	}
}
