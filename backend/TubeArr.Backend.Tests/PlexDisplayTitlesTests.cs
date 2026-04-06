using TubeArr.Backend.Data;
using TubeArr.Backend.Plex;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class PlexDisplayTitlesTests
{
	[Fact]
	public void Episode_prefers_db_title_then_nfo_then_youtube_json_then_overview_description_id_episode()
	{
		var v = new VideoEntity { Title = "T", Overview = null, Description = null };
		Assert.Equal("T", PlexDisplayTitles.Episode(v, 3));

		v = new VideoEntity { Title = "  ", Overview = "Line\nRest", Description = "Desc" };
		Assert.Equal("Line", PlexDisplayTitles.Episode(v, 3));

		v = new VideoEntity { Title = "", Overview = "", Description = "Only desc\n" };
		Assert.Equal("Only desc", PlexDisplayTitles.Episode(v, 7));

		v = new VideoEntity { Title = "", Overview = "", Description = "", YoutubeVideoId = "" };
		Assert.Equal("Episode 7", PlexDisplayTitles.Episode(v, 7));

		v = new VideoEntity { Title = "", Overview = "", Description = "", YoutubeVideoId = "dQw4w9WgXcQ" };
		Assert.Equal("dQw4w9WgXcQ", PlexDisplayTitles.Episode(v, 7));

		v = new VideoEntity { Title = "", Overview = "", Description = "" };
		Assert.Equal("From NFO", PlexDisplayTitles.Episode(v, 1, "From NFO"));

		v = new VideoEntity
		{
			Title = "",
			YouTubeDataApiVideoResourceJson = """{"snippet":{"title":"From persisted API"}}""",
			Overview = "",
			Description = ""
		};
		Assert.Equal("From persisted API", PlexDisplayTitles.Episode(v, 2));

		v = new VideoEntity
		{
			Title = "",
			YouTubeDataApiVideoResourceJson = """{"snippet":{"title":"API title"}}"""
		};
		Assert.Equal("NFO wins", PlexDisplayTitles.Episode(v, 1, "NFO wins"));

		v = new VideoEntity { Title = "DB wins", Overview = "", Description = "" };
		Assert.Equal("DB wins", PlexDisplayTitles.Episode(v, 1, "From NFO"));

		// Discovery sometimes stores the video id as Title; do not treat that as a real title when API JSON has snippet.title.
		v = new VideoEntity
		{
			Title = "dQw4w9WgXcQ",
			YoutubeVideoId = "dQw4w9WgXcQ",
			YouTubeDataApiVideoResourceJson = """{"snippet":{"title":"Real upload title"}}""",
			Overview = "",
			Description = ""
		};
		Assert.Equal("Real upload title", PlexDisplayTitles.Episode(v, 1));
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
