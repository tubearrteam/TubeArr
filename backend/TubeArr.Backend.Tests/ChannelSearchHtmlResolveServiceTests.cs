using Xunit;
using TubeArr.Backend;

namespace TubeArr.Backend.Tests;

public sealed class ChannelSearchHtmlResolveServiceTests
{
	[Fact]
	public void ExtractChannelCandidatesFromSearchResultsHtml_uses_quick_regex_fallback()
	{
		var html = @"<html><script>{""channelRenderer"":{""channelId"":""UC1234567890123456789012""}}</script></html>";
		var candidates = ChannelSearchHtmlResolveService.ExtractChannelCandidatesFromSearchResultsHtml(html, maxResults: 5);
		Assert.Single(candidates);
		Assert.Equal("UC1234567890123456789012", candidates[0].YoutubeChannelId);
		Assert.Equal("UC1234567890123456789012", candidates[0].Title);
	}

	[Fact]
	public void ExtractChannelCandidatesFromSearchResultsHtml_extracts_from_twoColumnSearchResultsRenderer()
	{
		var html = @"
var ytInitialData = {
  ""contents"": {
    ""twoColumnSearchResultsRenderer"": {
      ""primaryContents"": {
        ""sectionListRenderer"": {
          ""contents"": [
            {
              ""itemSectionRenderer"": {
                ""contents"": [
                  {
                    ""channelRenderer"": {
                      ""channelId"": ""UCabcdefghijklmnopqrstuv"",
                      ""title"": { ""simpleText"": ""Real Channel Title"" },
                      ""descriptionSnippet"": { ""runs"": [ { ""text"": ""Channel short overview"" } ] },
                      ""channelThumbnailSupportedRenderers"": {
                        ""channelThumbnailWithLinkRenderer"": {
                          ""thumbnail"": { ""thumbnails"": [ { ""url"": ""https://img.example/small.jpg"" } ] }
                        }
                      }
                    }
                  }
                ]
              }
            }
          ]
        }
      }
    }
  }
};";

		var candidates = ChannelSearchHtmlResolveService.ExtractChannelCandidatesFromSearchResultsHtml(html, maxResults: 10);
		Assert.Single(candidates);
		Assert.Equal("UCabcdefghijklmnopqrstuv", candidates[0].YoutubeChannelId);
		Assert.Equal("Real Channel Title", candidates[0].Title);
    Assert.Equal("Channel short overview", candidates[0].Description);
		Assert.Equal("https://img.example/small.jpg", candidates[0].ThumbnailUrl);
	}

	[Fact]
	public void ExtractChannelCandidatesFromSearchResultsHtml_extracts_from_continuation_items()
	{
		var html = @"
var ytInitialData = {
  ""onResponseReceivedCommands"": [
    {
      ""appendContinuationItemsAction"": {
        ""continuationItems"": [
          {
              ""channelRenderer"": {
              ""channelId"": ""UC-A_B1c2D3e4F5g6H7i8J9k"",
              ""title"": { ""simpleText"": ""From Continuation"" }
            }
          }
        ]
      }
    }
  ]
};";

		var candidates = ChannelSearchHtmlResolveService.ExtractChannelCandidatesFromSearchResultsHtml(html, maxResults: 5);
		Assert.Single(candidates);
		Assert.Equal("UC-A_B1c2D3e4F5g6H7i8J9k", candidates[0].YoutubeChannelId);
		Assert.Equal("From Continuation", candidates[0].Title);
	}
}

