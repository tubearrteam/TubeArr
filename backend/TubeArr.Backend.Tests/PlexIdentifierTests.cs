using TubeArr.Backend.Plex;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class PlexIdentifierTests
{
	[Fact]
	public void ratingKey_generation_is_deterministic_and_safe()
	{
		var show = PlexIdentifier.BuildRatingKey(PlexIdentifier.PlexItemKind.Show, "UC123abc");
		var season = PlexIdentifier.BuildRatingKey(PlexIdentifier.PlexItemKind.Season, "PL987xyz");
		var ep = PlexIdentifier.BuildRatingKey(PlexIdentifier.PlexItemKind.Episode, "dQw4w9WgXcQ");

		Assert.Equal("ch_UC123abc", show);
		Assert.Equal("pl_PL987xyz", season);
		Assert.Equal("v_dQw4w9WgXcQ", ep);

		Assert.True(PlexIdentifier.IsSafeRatingKey(show));
		Assert.True(PlexIdentifier.IsSafeRatingKey(season));
		Assert.True(PlexIdentifier.IsSafeRatingKey(ep));
	}

	[Fact]
	public void guid_generation_uses_expected_scheme_and_shape()
	{
		var rk = "v_dQw4w9WgXcQ";
		var guid = PlexIdentifier.BuildGuid(PlexIdentifier.PlexItemKind.Episode, rk);
		Assert.Equal("tv.plex.agents.custom.tubearr://episode/v_dQw4w9WgXcQ", guid);
	}

	[Theory]
	[InlineData("ch_UC123abc", PlexIdentifier.PlexItemKind.Show, "UC123abc")]
	[InlineData("pl_PL987xyz", PlexIdentifier.PlexItemKind.Season, "PL987xyz")]
	[InlineData("v_dQw4w9WgXcQ", PlexIdentifier.PlexItemKind.Episode, "dQw4w9WgXcQ")]
	public void ratingKey_parsing_round_trips(string rk, PlexIdentifier.PlexItemKind kind, string yt)
	{
		Assert.True(PlexIdentifier.TryParseRatingKey(rk, out var parsedKind, out var parsedId));
		Assert.Equal(kind, parsedKind);
		Assert.Equal(yt, parsedId);
	}

	[Theory]
	[InlineData("ch_UC/123")]
	[InlineData("v_..")]
	[InlineData("")]
	public void ratingKey_parsing_rejects_unsafe_values(string rk)
	{
		Assert.False(PlexIdentifier.TryParseRatingKey(rk, out _, out _));
	}
}

