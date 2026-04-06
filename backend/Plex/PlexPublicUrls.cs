using System.Text;
using Microsoft.AspNetCore.Http;

namespace TubeArr.Backend.Plex;

/// <summary>Absolute URLs for Plex metadata payloads (Plex requires http(s) for <c>thumb</c>, not local file paths).</summary>
internal static class PlexPublicUrls
{
	internal static string BuildTvProviderRootUrl(HttpRequest req)
	{
		var path = req.Path.Value ?? "";
		const string tv = "/tv";
		int len;
		var idx = path.IndexOf(tv + "/", StringComparison.Ordinal);
		if (idx >= 0)
			len = idx + tv.Length;
		else if (path.EndsWith(tv, StringComparison.Ordinal))
			len = path.Length;
		else
			len = -1;

		var sb = new StringBuilder();
		sb.Append(req.Scheme).Append("://").Append(req.Host);
		if (req.PathBase.HasValue)
			sb.Append(req.PathBase.Value);
		if (len < 0)
			sb.Append(tv);
		else
			sb.Append(path, 0, len);

		return sb.ToString();
	}

	internal static string BuildEpisodeSidecarThumbAbsoluteUrl(HttpRequest req, string youtubeVideoId, long? versionTag = null)
	{
		var url = BuildTvProviderRootUrl(req) + "/artwork/episode-thumb?youtubeVideoId=" + Uri.EscapeDataString(youtubeVideoId);
		if (versionTag.HasValue)
			url += "&v=" + versionTag.Value;
		return url;
	}
}
