using System.Linq;
using System.Text.Json;
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

	internal static string Episode(VideoEntity video, int episodeIndex, string? nfoEpisodeTitle = null)
	{
		var title = (video.Title ?? "").Trim();
		if (IsHumanEpisodeTitle(title, video.YoutubeVideoId))
			return title;

		var nfo = (nfoEpisodeTitle ?? "").Trim();
		if (nfo.Length > 0)
			return nfo;

		if (TryReadSnippetTitleFromYouTubeVideoResourceJson(video.YouTubeDataApiVideoResourceJson, out var fromApi))
			return fromApi;

		var fromOverview = FirstLine(video.Overview);
		if (fromOverview.Length > 0)
			return fromOverview;

		var fromDescription = FirstLine(video.Description);
		if (fromDescription.Length > 0)
			return fromDescription;

		var yt = (video.YoutubeVideoId ?? "").Trim();
		if (yt.Length > 0)
			return yt;

		return "Episode " + episodeIndex;
	}

	/// <summary>
	/// False when <paramref name="title"/> is empty or only mirrors the YouTube id (discovery sometimes stores the id as a placeholder).
	/// </summary>
	static bool IsHumanEpisodeTitle(string title, string? youtubeVideoId)
	{
		if (title.Length == 0)
			return false;
		var id = (youtubeVideoId ?? "").Trim();
		if (id.Length > 0 && string.Equals(title, id, StringComparison.OrdinalIgnoreCase))
			return false;
		return true;
	}

	/// <summary>
	/// Reads <c>snippet.title</c> (then <c>snippet.localized.title</c>) from persisted <see cref="VideoEntity.YouTubeDataApiVideoResourceJson"/> (videos.list fragments).
	/// </summary>
	static bool TryReadSnippetTitleFromYouTubeVideoResourceJson(string? json, out string title)
	{
		title = "";
		var raw = (json ?? "").Trim();
		if (raw.Length == 0)
			return false;

		try
		{
			using var doc = JsonDocument.Parse(raw);
			if (!doc.RootElement.TryGetProperty("snippet", out var snippet) || snippet.ValueKind != JsonValueKind.Object)
				return false;
			if (TryReadNonEmptyStringProperty(snippet, "title", out title))
				return true;
			if (snippet.TryGetProperty("localized", out var localized) && localized.ValueKind == JsonValueKind.Object &&
			    TryReadNonEmptyStringProperty(localized, "title", out title))
				return true;
			return false;
		}
		catch (JsonException)
		{
			return false;
		}
	}

	static bool TryReadNonEmptyStringProperty(JsonElement obj, string name, out string value)
	{
		value = "";
		if (!obj.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
			return false;
		var t = (el.GetString() ?? "").Trim();
		if (t.Length == 0)
			return false;
		value = t;
		return true;
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
