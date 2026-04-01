using TubeArr.Backend.Data;
using TubeArr.Backend.Plex;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class PlexDisplayTitlesTests
{
	[Fact]
	public void Episode_prefers_title_then_overview_first_line_then_description_thenEpisodeN()
	{
		var v = new VideoEntity { Title = "T", Overview = null, Description = null };
		Assert.Equal("T", PlexDisplayTitles.Episode(v, 3));

		v = new VideoEntity { Title = "  ", Overview = "Line\nRest", Description = "Desc" };
		Assert.Equal("Line", PlexDisplayTitles.Episode(v, 3));

		v = new VideoEntity { Title = "", Overview = "", Description = "Only desc\n" };
		Assert.Equal("Only desc", PlexDisplayTitles.Episode(v, 7));

		v = new VideoEntity { Title = "", Overview = "", Description = "" };
		Assert.Equal("Episode 7", PlexDisplayTitles.Episode(v, 7));

		v = new VideoEntity { Title = "", Overview = "", Description = "" };
		Assert.Equal(
			"From File",
			PlexDisplayTitles.Episode(v, 1, @"C:\Lib\Ch [UCx]\Season 01\X - s01e01 - From File [AbCdEfGhIjK].mkv"));

		v = new VideoEntity { Title = "", Overview = "", Description = "" };
		Assert.Equal(
			"From NFO",
			PlexDisplayTitles.Episode(v, 1, @"C:\x.mkv", nfoEpisodeTitle: "From NFO"));

		v = new VideoEntity { Title = "DB wins", Overview = "", Description = "" };
		Assert.Equal("DB wins", PlexDisplayTitles.Episode(v, 1, @"C:\x.mkv", nfoEpisodeTitle: "From NFO"));
	}

	[Fact]
	public void Channel_falls_back_to_youtube_id_when_title_empty()
	{
		var c = new ChannelEntity { YoutubeChannelId = "UCx", Title = "" };
		Assert.Equal("UCx", PlexDisplayTitles.Channel(c));

		c.Title = "Named";
		Assert.Equal("Named", PlexDisplayTitles.Channel(c));
	}

	[Fact]
	public void Season_uses_playlist_or_index_or_channel_uploads()
	{
		var p = new PlaylistEntity { Title = "" };
		Assert.Equal("Season 2", PlexDisplayTitles.Season(p, 2, channelOnlySeason: false));

		p.Title = "My Series";
		Assert.Equal("My Series", PlexDisplayTitles.Season(p, 2, channelOnlySeason: false));

		Assert.Equal("Channel Uploads", PlexDisplayTitles.Season(null, 1, channelOnlySeason: true));
	}
}
