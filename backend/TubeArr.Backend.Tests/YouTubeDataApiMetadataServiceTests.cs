using Xunit;
using TubeArr.Backend;

namespace TubeArr.Backend.Tests;

public sealed class YouTubeDataApiMetadataServiceTests
{
	[Theory]
	[InlineData("PT15S", 15)]
	[InlineData("PT3M45S", 225)]
	[InlineData("PT5M13S", 313)]
	[InlineData("PT1H2M10S", 3730)]
	[InlineData("PT1H", 3600)]
	[InlineData("PT45M", 2700)]
	public void ParseIso8601DurationSeconds_maps_contentDetails_duration_to_seconds(string iso8601, int expectedSeconds)
	{
		Assert.Equal(expectedSeconds, YouTubeDataApiMetadataService.ParseIso8601DurationSeconds(iso8601));
	}

	[Fact]
	public void ParseIso8601DurationSeconds_null_or_whitespace_returns_null()
	{
		Assert.Null(YouTubeDataApiMetadataService.ParseIso8601DurationSeconds(null));
		Assert.Null(YouTubeDataApiMetadataService.ParseIso8601DurationSeconds("   "));
	}
}
