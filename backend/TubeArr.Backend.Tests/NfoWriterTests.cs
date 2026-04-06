using System.IO;
using System.Xml.Linq;
using TubeArr.Backend.Media.Nfo;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class NfoWriterTests
{
	[Fact]
	public void BuildTvShowDocument_minimal_shape_and_omits_empty_plot()
	{
		var xml = NfoWriter.BuildTvShowDocument(new TvShowNfoContent("Ch", Year: 2021, Plot: null));
		var doc = XDocument.Parse(xml);
		var root = doc.Root!;
		Assert.Equal("tvshow", root.Name.LocalName);
		Assert.Equal("Ch", root.Element("title")?.Value);
		Assert.Equal("2021", root.Element("year")?.Value);
		Assert.Null(root.Element("plot"));
		Assert.Equal("UTF-8", doc.Declaration?.Encoding);
		Assert.Equal("yes", doc.Declaration?.Standalone);
	}

	[Fact]
	public void BuildTvShowDocument_includes_plot_when_present()
	{
		var xml = NfoWriter.BuildTvShowDocument(new TvShowNfoContent("X", Year: null, Plot: "About & stuff"));
		var doc = XDocument.Parse(xml);
		Assert.Contains("About &amp; stuff", xml, StringComparison.Ordinal);
		Assert.Equal("About & stuff", doc.Root?.Element("plot")?.Value);
		Assert.Null(doc.Root?.Element("year"));
	}

	[Fact]
	public void BuildSeasonDocument_seasonnumber_title_year()
	{
		var xml = NfoWriter.BuildSeasonDocument(new SeasonNfoContent(2, "PL", Year: 2024));
		var doc = XDocument.Parse(xml);
		var root = doc.Root!;
		Assert.Equal("2", root.Element("seasonnumber")?.Value);
		Assert.Equal("PL", root.Element("title")?.Value);
		Assert.Equal("2024", root.Element("year")?.Value);
	}

	[Fact]
	public void BuildSeasonDocument_omits_year_when_null()
	{
		var xml = NfoWriter.BuildSeasonDocument(new SeasonNfoContent(1, "S", Year: null));
		var doc = XDocument.Parse(xml);
		Assert.Null(doc.Root?.Element("year"));
	}

	[Fact]
	public void BuildEpisodeDocument_maps_season_episode_aired_plot()
	{
		var xml = NfoWriter.BuildEpisodeDocument(new EpisodeNfoContent(
			Title: "Vid",
			Season: 1,
			Episode: 3,
			Plot: "Desc",
			Aired: "2024-06-18"));
		var doc = XDocument.Parse(xml);
		var r = doc.Root!;
		Assert.Equal("Vid", r.Element("title")?.Value);
		Assert.Equal("1", r.Element("season")?.Value);
		Assert.Equal("3", r.Element("episode")?.Value);
		Assert.Equal("Desc", r.Element("plot")?.Value);
		Assert.Equal("2024-06-18", r.Element("aired")?.Value);
	}

	[Fact]
	public void BuildEpisodeDocument_omits_plot_and_aired_when_null()
	{
		var xml = NfoWriter.BuildEpisodeDocument(new EpisodeNfoContent("T", 2, 5, Plot: null, Aired: null));
		var doc = XDocument.Parse(xml);
		Assert.Null(doc.Root?.Element("plot"));
		Assert.Null(doc.Root?.Element("aired"));
	}

	[Fact]
	public void BuildEpisodeDocument_includes_youtube_uniqueid_when_present()
	{
		var xml = NfoWriter.BuildEpisodeDocument(new EpisodeNfoContent(
			"Vid", 1, 1, null, null, YoutubeVideoId: "dQw4w9WgXcQ"));
		var doc = XDocument.Parse(xml);
		var uid = doc.Root?.Element("uniqueid");
		Assert.NotNull(uid);
		Assert.Equal("youtube", uid!.Attribute("type")?.Value);
		Assert.Equal("true", uid.Attribute("default")?.Value);
		Assert.Equal("dQw4w9WgXcQ", uid.Value);
	}

	[Fact]
	public async Task WriteEpisodeNfoAsync_writes_basename_next_to_media()
	{
		var dir = Path.Combine(Path.GetTempPath(), "tubearr-nfo-" + Guid.NewGuid().ToString("N"));
		try
		{
			var media = Path.Combine(dir, "My Episode.mkv");
			Directory.CreateDirectory(dir);
			await File.WriteAllTextAsync(media, "");

			await NfoWriter.WriteEpisodeNfoAsync(
				media,
				new EpisodeNfoContent("E", 1, 1, null, null));

			var nfoPath = Path.Combine(dir, "My Episode.nfo");
			Assert.True(File.Exists(nfoPath));
			var text = await File.ReadAllTextAsync(nfoPath);
			XDocument.Parse(text);
		}
		finally
		{
			try
			{
				if (Directory.Exists(dir))
					Directory.Delete(dir, recursive: true);
			}
			catch
			{
				// ignore
			}
		}
	}
}
