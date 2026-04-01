using TubeArr.Backend.Data;
using TubeArr.Backend.Media;

namespace TubeArr.Backend.Plex;

internal static class PlexPayloadBuilder
{
	internal static object BuildShowMetadata(ChannelEntity channel, bool includeChildren, IReadOnlyList<object>? children, string? thumbUrl = null, string? artUrl = null)
	{
		var ratingKey = PlexIdentifier.BuildRatingKey(PlexIdentifier.PlexItemKind.Show, channel.YoutubeChannelId);
		var guid = PlexIdentifier.BuildGuid(PlexIdentifier.PlexItemKind.Show, ratingKey);
		var key = PlexKeys.LibraryMetadataChildren(ratingKey);

		var title = PlexDisplayTitles.Channel(channel);
		var summary = (channel.Description ?? "").Trim();
		var showYear = channel.Added.Year;

		var meta = new Dictionary<string, object?>
		{
			["type"] = "show",
			["ratingKey"] = ratingKey,
			["guid"] = guid,
			["key"] = key,
			["title"] = title,
			["titleSort"] = title,
			["summary"] = summary,
			["originallyAvailableAt"] = channel.Added.ToString("yyyy-MM-dd"),
			["year"] = showYear
		};

		if (!string.IsNullOrWhiteSpace(thumbUrl))
			meta["thumb"] = thumbUrl;
		if (!string.IsNullOrWhiteSpace(artUrl))
			meta["art"] = artUrl;

		if (includeChildren && children is not null)
		{
			meta["Children"] = new
			{
				size = children.Count,
				Metadata = children
			};
		}

		return meta;
	}

	internal static object BuildSeasonMetadata(ChannelEntity channel, PlaylistEntity playlist, int seasonIndex, bool includeChildren, IReadOnlyList<object>? children, string? thumbUrl = null, string? parentThumbUrl = null, string? parentArtUrl = null)
	{
		var showRatingKey = PlexIdentifier.BuildRatingKey(PlexIdentifier.PlexItemKind.Show, channel.YoutubeChannelId);
		var showGuid = PlexIdentifier.BuildGuid(PlexIdentifier.PlexItemKind.Show, showRatingKey);
		var showKey = PlexKeys.LibraryMetadata(showRatingKey);

		var ratingKey = PlexIdentifier.BuildRatingKey(PlexIdentifier.PlexItemKind.Season, playlist.YoutubePlaylistId);
		var guid = PlexIdentifier.BuildGuid(PlexIdentifier.PlexItemKind.Season, ratingKey);
		var key = PlexKeys.LibraryMetadataChildren(ratingKey);
		var aired = playlist.Added.ToString("yyyy-MM-dd");
		var airedYear = playlist.Added.Year;

		var seasonTitle = PlexDisplayTitles.Season(playlist, seasonIndex, channelOnlySeason: false);
		var meta = new Dictionary<string, object?>
		{
			["type"] = "season",
			["ratingKey"] = ratingKey,
			["guid"] = guid,
			["key"] = key,
			["title"] = seasonTitle,
			["titleSort"] = seasonTitle,
			["summary"] = (playlist.Description ?? "").Trim(),
			["index"] = seasonIndex,
			["originallyAvailableAt"] = aired,
			["year"] = airedYear,

			["parentType"] = "show",
			["parentRatingKey"] = showRatingKey,
			["parentGuid"] = showGuid,
			["parentKey"] = showKey,
			["parentTitle"] = PlexDisplayTitles.Channel(channel)
		};

		if (!string.IsNullOrWhiteSpace(thumbUrl))
			meta["thumb"] = thumbUrl;
		if (!string.IsNullOrWhiteSpace(parentThumbUrl))
			meta["parentThumb"] = parentThumbUrl;
		if (!string.IsNullOrWhiteSpace(parentArtUrl))
			meta["parentArt"] = parentArtUrl;

		if (includeChildren && children is not null)
		{
			meta["Children"] = new
			{
				size = children.Count,
				Metadata = children
			};
		}

		return meta;
	}

	internal static object BuildEpisodeMetadata(ChannelEntity channel, PlaylistEntity? playlist, VideoEntity video, int seasonIndex, int episodeIndex, string? primaryFilePath = null, string? nfoEpisodeTitle = null, string? episodeThumbUrl = null, string? parentThumbUrl = null, string? grandparentThumbUrl = null, string? grandparentArtUrl = null)
	{
		var showRatingKey = PlexIdentifier.BuildRatingKey(PlexIdentifier.PlexItemKind.Show, channel.YoutubeChannelId);
		var showGuid = PlexIdentifier.BuildGuid(PlexIdentifier.PlexItemKind.Show, showRatingKey);
		var showKey = PlexKeys.LibraryMetadata(showRatingKey);

		string seasonRatingKey;
		string seasonGuid;
		string seasonKey;
		string seasonTitle;

		if (playlist is not null)
		{
			seasonRatingKey = PlexIdentifier.BuildRatingKey(PlexIdentifier.PlexItemKind.Season, playlist.YoutubePlaylistId);
			seasonGuid = PlexIdentifier.BuildGuid(PlexIdentifier.PlexItemKind.Season, seasonRatingKey);
			seasonKey = PlexKeys.LibraryMetadata(seasonRatingKey);
			seasonTitle = PlexDisplayTitles.Season(playlist, seasonIndex, channelOnlySeason: false);
		}
		else
		{
			// Channel-only season has no playlist ratingKey; model as a synthetic season under the show.
			seasonRatingKey = showRatingKey + "_s1";
			seasonGuid = PlexConstants.Scheme + "://season/" + seasonRatingKey;
			seasonKey = PlexKeys.LibraryMetadata(seasonRatingKey);
			seasonTitle = PlexDisplayTitles.Season(playlist: null, seasonIndex, channelOnlySeason: true);
		}

		var ratingKey = PlexIdentifier.BuildRatingKey(PlexIdentifier.PlexItemKind.Episode, video.YoutubeVideoId);
		var guid = PlexIdentifier.BuildGuid(PlexIdentifier.PlexItemKind.Episode, ratingKey);
		var key = PlexKeys.LibraryMetadata(ratingKey);

		var aired = FormatDate(video.UploadDateUtc);
		var year = video.UploadDateUtc.Year;
		var durationMs = video.Runtime > 0 ? video.Runtime * 1000 : 0;
		var episodeTitle = PlexDisplayTitles.Episode(video, episodeIndex, primaryFilePath, nfoEpisodeTitle);
		var channelTitle = PlexDisplayTitles.Channel(channel);

		var ep = new Dictionary<string, object?>
		{
			["type"] = "episode",
			["ratingKey"] = ratingKey,
			["guid"] = guid,
			["key"] = key,
			["title"] = episodeTitle,
			["titleSort"] = episodeTitle,
			["summary"] = (video.Description ?? "").Trim(),
			["originallyAvailableAt"] = aired,
			["year"] = year,

			["index"] = episodeIndex,
			["parentIndex"] = seasonIndex,

			["parentType"] = "season",
			["parentRatingKey"] = seasonRatingKey,
			["parentGuid"] = seasonGuid,
			["parentKey"] = seasonKey,
			["parentTitle"] = seasonTitle,

			["grandparentType"] = "show",
			["grandparentRatingKey"] = showRatingKey,
			["grandparentGuid"] = showGuid,
			["grandparentKey"] = showKey,
			["grandparentTitle"] = channelTitle
		};

		if (durationMs > 0)
			ep["duration"] = durationMs;

		if (!string.IsNullOrWhiteSpace(episodeThumbUrl))
		{
			ep["thumb"] = episodeThumbUrl;
			ep["Image"] = new[]
			{
				new { type = "snapshot", url = episodeThumbUrl, alt = episodeTitle }
			};
		}

		if (!string.IsNullOrWhiteSpace(parentThumbUrl))
			ep["parentThumb"] = parentThumbUrl;
		if (!string.IsNullOrWhiteSpace(grandparentThumbUrl))
			ep["grandparentThumb"] = grandparentThumbUrl;
		if (!string.IsNullOrWhiteSpace(grandparentArtUrl))
			ep["grandparentArt"] = grandparentArtUrl;

		return ep;
	}

	static string FormatDate(DateTimeOffset dt) => dt.ToString("yyyy-MM-dd");
}

