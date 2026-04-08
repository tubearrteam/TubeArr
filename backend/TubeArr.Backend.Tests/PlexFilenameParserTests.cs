using TubeArr.Backend.Plex;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class PlexFilenameParserTests
{
	[Theory]
	[InlineData(@"C:\TV\Show [UC123abc]\Season 01\Show - s01e001 - Title [dQw4w9WgXcQ].mkv", "dQw4w9WgXcQ")]
	[InlineData(@"/tv/Show/Season 01/Title [dQw4w9WgXcQ].mp4", "dQw4w9WgXcQ")]
	public void parse_videoId_from_filename_brackets(string path, string expected)
	{
		Assert.True(PlexFilenameParser.TryParseYoutubeVideoIdFromPath(path, out var id));
		Assert.Equal(expected, id);
	}

	[Theory]
	[InlineData(@"C:\TV\Show [UC123abc]\Season 01\file.mkv", "UC123abc")]
	[InlineData(@"/tv/Show [UC123abc]/Season 01/file.mkv", "UC123abc")]
	public void parse_channelId_from_path_brackets(string path, string expected)
	{
		Assert.True(PlexFilenameParser.TryParseYoutubeChannelIdFromPath(path, out var id));
		Assert.Equal(expected, id);
	}

	[Theory]
	[InlineData(@"Show - s01e002 - Title.mkv", 1, 2)]
	[InlineData(@"/tv/Show/Season 12/Show - S12E003 - Title.mkv", 12, 3)]
	public void parse_season_episode_from_path(string path, int s, int e)
	{
		Assert.True(PlexFilenameParser.TryParseSeasonEpisodeFromPath(path, out var season, out var ep));
		Assert.Equal(s, season);
		Assert.Equal(e, ep);
	}

	[Theory]
	[InlineData(@"D:\Youtube\Wunba\Season 01\20260126 - Title [tbVmfSyanQw].mp4", "Wunba")]
	[InlineData(@"D:\Youtube\Blitz\Season 01\file.mkv", "Blitz")]
	[InlineData(@"D:/Youtube/Call Me Kevin/Season 10/x.mp4", "Call Me Kevin")]
	[InlineData(@"D:\Youtube\RTGame", "RTGame")]
	public void show_folder_name_from_path(string path, string expected)
	{
		Assert.True(PlexFilenameParser.TryGetShowFolderNameFromPath(path, out var folder));
		Assert.Equal(expected, folder);
	}

}

