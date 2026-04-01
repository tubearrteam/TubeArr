using System.Text.Json;
using TubeArr.Backend;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class ChannelCustomPlaylistEvaluatorTests
{
	[Fact]
	public void Matches_All_TitleContains()
	{
		var rules = new List<ChannelCustomPlaylistRule>
		{
			new()
			{
				Field = "title",
				Operator = "contains",
				Value = JsonSerializer.SerializeToElement("foo")
			}
		};
		var ctx = new CustomPlaylistVideoContext(
			Title: "Foo review",
			Description: "",
			PrimarySourcePlaylistYoutubeId: null,
			PrimarySourcePlaylistName: null,
			AllSourcePlaylistYoutubeIds: Array.Empty<string>(),
			AllSourcePlaylistNames: Array.Empty<string>(),
			PublishedAtUtc: DateTimeOffset.UtcNow,
			DurationSeconds: 60);
		Assert.True(ChannelCustomPlaylistEvaluator.Matches(rules, ChannelCustomPlaylistMatchType.All, ctx));
	}

	[Fact]
	public void Matches_Any_OneRulePasses()
	{
		var rules = new List<ChannelCustomPlaylistRule>
		{
			new() { Field = "title", Operator = "equals", Value = JsonSerializer.SerializeToElement("x") },
			new() { Field = "title", Operator = "contains", Value = JsonSerializer.SerializeToElement("bar") }
		};
		var ctx = new CustomPlaylistVideoContext(
			Title: "foo bar",
			Description: "",
			null,
			null,
			Array.Empty<string>(),
			Array.Empty<string>(),
			DateTimeOffset.UtcNow,
			120);
		Assert.True(ChannelCustomPlaylistEvaluator.Matches(rules, ChannelCustomPlaylistMatchType.Any, ctx));
	}
}
