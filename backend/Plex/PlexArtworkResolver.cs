using Microsoft.AspNetCore.Http;
using TubeArr.Backend.Data;

namespace TubeArr.Backend.Plex;

/// <summary>Plex metadata thumb/art URLs: local sidecar JPEG via TubeArr <c>/tv/artwork/…</c> when present, else YouTube CDN / stored acquisition URLs.</summary>
internal static class PlexArtworkResolver
{
	internal static (string? thumb, string? art) GetShowArtwork(ChannelEntity channel)
	{
		var thumb = (channel.ThumbnailUrl ?? "").Trim();
		var art = (channel.BannerUrl ?? "").Trim();
		return (thumb.Length > 0 ? thumb : null, art.Length > 0 ? art : null);
	}

	internal static string? GetSeasonPoster(PlaylistEntity playlist)
	{
		var t = (playlist.ThumbnailUrl ?? "").Trim();
		return t.Length > 0 ? t : null;
	}

	/// <summary>Uses <see cref="VideoEntity.ThumbnailUrl"/> when set; otherwise a standard i.ytimg.com URL for the video id.</summary>
	internal static string? GetEpisodeThumb(VideoEntity video)
	{
		var url = (video.ThumbnailUrl ?? "").Trim();
		if (url.Length > 0)
			return url;

		var id = (video.YoutubeVideoId ?? "").Trim();
		if (id.Length == 0)
			return null;

		return "https://i.ytimg.com/vi/" + id + "/maxresdefault.jpg";
	}

	/// <summary>When <c>{basename}-thumb.jpg</c> exists next to the primary media file, returns an absolute TubeArr URL Plex can fetch; otherwise <see cref="GetEpisodeThumb"/>.</summary>
	internal static string? ResolveEpisodeThumbForPlex(HttpRequest httpRequest, VideoEntity video, string? primaryFilePath)
	{
		if (PlexEpisodeSidecarPaths.TryGetExistingSidecarPath(primaryFilePath) is not null)
		{
			var id = (video.YoutubeVideoId ?? "").Trim();
			if (id.Length > 0)
				return PlexPublicUrls.BuildEpisodeSidecarThumbAbsoluteUrl(httpRequest, id);
		}

		return GetEpisodeThumb(video);
	}
}
