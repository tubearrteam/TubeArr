using TubeArr.Backend.Media.Nfo;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class EpisodeNfoReaderTests
{
	[Fact]
	public void TryParseEpisodeTitleFromXml_reads_episodedetails_title()
	{
		var xml = """
			<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
			<episodedetails>
			  <title>Hello &amp; world</title>
			  <season>1</season>
			  <episode>2</episode>
			</episodedetails>
			""";
		Assert.Equal("Hello & world", EpisodeNfoReader.TryParseEpisodeTitleFromXml(xml));
	}

	[Fact]
	public void TryParseEpisodeTitleFromXml_returns_null_for_empty_title()
	{
		var xml = "<episodedetails><title>  </title></episodedetails>";
		Assert.Null(EpisodeNfoReader.TryParseEpisodeTitleFromXml(xml));
	}
}
