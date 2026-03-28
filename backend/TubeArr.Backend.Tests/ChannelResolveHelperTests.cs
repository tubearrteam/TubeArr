using Xunit;
using TubeArr.Backend;

namespace TubeArr.Backend.Tests;

public class ChannelResolveHelperTests
{
	[Theory]
	[InlineData("UC1234567890123456789012", true)]   // UC + 22 chars
	[InlineData("UCabcdefghijklmnopqrstuv", true)]  // UC + 22 chars
	[InlineData("UC-A_B1c2D3e4F5g6H7i8J9k", true)]
	[InlineData("", false)]
	[InlineData("UC123", false)]
	[InlineData("UC123456789012345678901", false)]   // only 21 after UC
	[InlineData("UC12345678901234567890123", false)] // 23 after UC
	[InlineData("  UC1234567890123456789012  ", true)]
	[InlineData("ub1234567890123456789012", false)]
	public void IsValidChannelId_detects_UC_format(string input, bool expected)
	{
		Assert.Equal(expected, ChannelResolveHelper.IsValidChannelId(input));
	}

	[Theory]
	[InlineData("https://www.youtube.com/channel/UC1234567890123456789012", "UC1234567890123456789012")]
	[InlineData("youtube.com/channel/UCabcdefghijklmnopqrstuv", "UCabcdefghijklmnopqrstuv")]
	[InlineData("https://m.youtube.com/channel/UC-A_B1c2D3e4F5g6H7i8J9k/", "UC-A_B1c2D3e4F5g6H7i8J9k")]
	[InlineData("https://www.youtube.com/channel/UC123", null)]
	[InlineData("https://www.youtube.com/watch?v=abc", null)]
	[InlineData("", null)]
	public void TryExtractChannelIdFromChannelUrl_extracts_UC_from_channel_url(string input, string? expectedId)
	{
		var result = ChannelResolveHelper.TryExtractChannelIdFromChannelUrl(input);
		Assert.Equal(expectedId, result);
	}

	[Fact]
	public void ExtractChannelIdFromHtml_best_externalId()
	{
		var html = @"<script>""externalId"":""UC1234567890123456789012""</script>";
		Assert.Equal("UC1234567890123456789012", ChannelResolveHelper.ExtractChannelIdFromHtml(html));
	}

	[Fact]
	public void ExtractChannelIdFromHtml_fallback_channelId()
	{
		var html = @"<script>""channelId"":""UC1234567890123456789012""</script>";
		Assert.Equal("UC1234567890123456789012", ChannelResolveHelper.ExtractChannelIdFromHtml(html));
	}

	[Fact]
	public void ExtractChannelIdFromHtml_last_fallback_channel_path()
	{
		var html = @"<a href=""https://www.youtube.com/channel/UCabcdefghijklmnopqrstuv"">Channel</a>";
		Assert.Equal("UCabcdefghijklmnopqrstuv", ChannelResolveHelper.ExtractChannelIdFromHtml(html));
	}

	[Fact]
	public void ExtractChannelIdFromHtml_prefers_externalId_over_channelId()
	{
		var html = @"""channelId"":""UCaaaaaaaaaaaaaaaaaaaaaa"",""externalId"":""UC1234567890123456789012""";
		Assert.Equal("UC1234567890123456789012", ChannelResolveHelper.ExtractChannelIdFromHtml(html));
	}

	[Fact]
	public void ExtractChannelIdFromHtml_returns_null_for_empty_or_invalid()
	{
		Assert.Null(ChannelResolveHelper.ExtractChannelIdFromHtml(""));
		Assert.Null(ChannelResolveHelper.ExtractChannelIdFromHtml("<html>no channel here</html>"));
	}

	[Fact]
	public void LooksLikeYouTubeChannelId_accepts_valid_UC_id()
	{
		Assert.True(ChannelResolveHelper.LooksLikeYouTubeChannelId("UC1234567890123456789012"));
	}

	[Fact]
	public void LooksLikeYouTubeChannelId_rejects_short_or_non_UC()
	{
		Assert.False(ChannelResolveHelper.LooksLikeYouTubeChannelId("UC123"));
		Assert.False(ChannelResolveHelper.LooksLikeYouTubeChannelId(""));
		Assert.False(ChannelResolveHelper.LooksLikeYouTubeChannelId("  "));
	}

	[Fact]
	public void ExtractChannelTitleFromHtml_simpleText()
	{
		var html = @"var ytInitialData = {""metadata"":{""channelMetadataRenderer"":{""title"":{""simpleText"":""Linus Tech Tips""}}}};";
		Assert.Equal("Linus Tech Tips", ChannelResolveHelper.ExtractChannelTitleFromHtml(html));
	}

	[Fact]
	public void ExtractChannelTitleFromHtml_runs()
	{
		var html = @"var ytInitialData = {""metadata"":{""channelMetadataRenderer"":{""title"":{""runs"":[{""text"":""Linus Tech Tips""}]}}}};";
		Assert.Equal("Linus Tech Tips", ChannelResolveHelper.ExtractChannelTitleFromHtml(html));
	}

	[Fact]
	public void ExtractChannelTitleFromHtml_does_not_use_generic_og_title_fallback()
	{
		var html = @"<meta property=""og:title"" content=""Linus Tech Tips - YouTube"" />";
		Assert.Null(ChannelResolveHelper.ExtractChannelTitleFromHtml(html));
	}

	[Fact]
	public void Channel_metadata_fields_are_extracted_from_same_channel_metadata_renderer_object()
	{
		var html = @"var ytInitialData = {""metadata"":{""channelMetadataRenderer"":{
			""title"":{""runs"":[{""text"":""Real Channel Title""}]},
			""description"":""Real Channel Description"",
			""avatar"":{""thumbnails"":[{""url"":""https://img.example/small.jpg""},{""url"":""https://img.example/large.jpg""}]},
			""banner"":{""thumbnails"":[{""url"":""https://img.example/banner-small.jpg""},{""url"":""https://img.example/banner-large.jpg""}]}
		}}};";

		Assert.Equal("Real Channel Title", ChannelResolveHelper.ExtractChannelTitleFromHtml(html));
		Assert.Equal("Real Channel Description", ChannelResolveHelper.ExtractChannelDescriptionFromHtml(html));
		Assert.Equal("https://img.example/large.jpg", ChannelResolveHelper.ExtractChannelLogoFromHtml(html));
		Assert.Equal("https://img.example/banner-large.jpg", ChannelResolveHelper.ExtractChannelBannerFromHtml(html));
	}

	[Fact]
	public void ExtractChannelTitleFromHtml_uses_metadata_channel_metadata_renderer_not_unrelated_nodes()
	{
		var html = @"var ytInitialData = {"
			+ @"""foo"":{""channelMetadataRenderer"":{""title"":{""runs"":[{""text"":""Keyboard shortcuts""}]}}},"
			+ @"""metadata"":{""channelMetadataRenderer"":{""title"":{""runs"":[{""text"":""Real Channel Title""}]}}}"
			+ @"};";

		Assert.Equal("Real Channel Title", ChannelResolveHelper.ExtractChannelTitleFromHtml(html));
	}

	[Fact]
	public void ExtractChannelTitleFromHtml_returns_null_for_empty()
	{
		Assert.Null(ChannelResolveHelper.ExtractChannelTitleFromHtml(""));
		Assert.Null(ChannelResolveHelper.ExtractChannelTitleFromHtml("<html>no title</html>"));
	}

	[Fact]
	public void ExtractChannelRssUrlFromHtml_extracts_rssUrl()
	{
		var html = @"var ytInitialData = {""metadata"":{""channelMetadataRenderer"":{""rssUrl"":""https://www.youtube.com/feeds/videos.xml?playlist_id=UUexample""}}};";
		Assert.Equal("https://www.youtube.com/feeds/videos.xml?playlist_id=UUexample", ChannelResolveHelper.ExtractChannelRssUrlFromHtml(html));
	}

	[Fact]
	public void ExtractChannelRssUrlFromHtml_extracts_from_link_tag()
	{
		var html = @"<html><head><link rel=""alternate"" type=""application/rss+xml"" href=""https://www.youtube.com/feeds/videos.xml?playlist_id=UUexample"" /></head></html>";
		Assert.Equal("https://www.youtube.com/feeds/videos.xml?playlist_id=UUexample", ChannelResolveHelper.ExtractChannelRssUrlFromHtml(html));
	}

	[Fact]
	public void TryDetectShortsTabFromChannelHtml_true_when_title_shorts_in_ytInitialData()
	{
		var html = @"var ytInitialData = {""tabRenderer"":{""title"":""Shorts"",""endpoint"":{""url"":""/channel/UCx/shorts""}}};";
		Assert.True(ChannelResolveHelper.TryDetectShortsTabFromChannelHtml(html));
	}

	[Fact]
	public void TryDetectShortsTabFromChannelHtml_true_when_simpleText_shorts()
	{
		var html = @"var ytInitialData = {""title"":{""simpleText"":""Shorts""}};";
		Assert.True(ChannelResolveHelper.TryDetectShortsTabFromChannelHtml(html));
	}

	[Fact]
	public void TryDetectShortsTabFromChannelHtml_null_when_no_signal()
	{
		var html = @"var ytInitialData = {""metadata"":{""channelMetadataRenderer"":{""title"":{""simpleText"":""Only Videos""}}}};";
		Assert.Null(ChannelResolveHelper.TryDetectShortsTabFromChannelHtml(html));
	}

	[Fact]
	public void TryDetectShortsTabFromChannelHtml_null_when_no_ytInitialData()
	{
		Assert.Null(ChannelResolveHelper.TryDetectShortsTabFromChannelHtml("<html></html>"));
	}
}
