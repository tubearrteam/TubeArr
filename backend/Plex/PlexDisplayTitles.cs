using System.Linq;
using TubeArr.Backend.Data;

namespace TubeArr.Backend.Plex;

internal static class PlexDisplayTitles
{
	internal static string Channel(ChannelEntity channel)
	{
		var t = (channel.Title ?? "").Trim();
		if (t.Length > 0)
			return t;
		var id = (channel.YoutubeChannelId ?? "").Trim();
		return id.Length > 0 ? id : "Show";
	}

	internal static string Season(PlaylistEntity? playlist, int seasonIndex, bool channelOnlySeason)
	{
		if (channelOnlySeason)
			return "Channel Uploads";
		if (playlist is null)
			return "Season " + seasonIndex;
		var t = (playlist.Title ?? "").Trim();
		return t.Length > 0 ? t : "Season " + seasonIndex;
	}

	internal static string Episode(VideoEntity video, int episodeIndex, string? primaryFilePath = null, string? nfoEpisodeTitle = null)
	{
		var title = (video.Title ?? "").Trim();
		if (title.Length > 0)
			return title;

		var nfo = (nfoEpisodeTitle ?? "").Trim();
		if (nfo.Length > 0)
			return nfo;

		if (!string.IsNullOrWhiteSpace(primaryFilePath) &&
		    PlexFilenameParser.TryParseEpisodeDisplayTitleFromPath(primaryFilePath, out var fromFile))
			return fromFile;

		var fromOverview = FirstLine(video.Overview);
		if (fromOverview.Length > 0)
			return fromOverview;

		var fromDescription = FirstLine(video.Description);
		if (fromDescription.Length > 0)
			return fromDescription;

		return "Episode " + episodeIndex;
	}

	static string FirstLine(string? text)
	{
		var s = (text ?? "").Trim();
		if (s.Length == 0)
			return "";
		var line = s.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
		return (line ?? "").Trim();
	}
}
