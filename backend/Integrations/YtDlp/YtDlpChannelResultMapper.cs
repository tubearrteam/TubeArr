using System.Text.Json;

namespace TubeArr.Backend;

/// <summary>
/// Maps yt-dlp JSON output to channel result DTOs. Uses ordered fallbacks for field names.
/// Do not confuse video IDs with channel IDs; prefer channel_id / uploader_id.
/// </summary>
public static class YtDlpChannelResultMapper
{
	/// <summary>Map a single JSON object (video or channel entry) to channel result fields.</summary>
	public static ChannelResultMap MapFromEntry(JsonElement root, Func<string, string> slugify)
	{
		var youtubeChannelId = GetChannelId(root);
		var title = GetTitle(root);
		var titleSlug = string.IsNullOrWhiteSpace(title) ? "" : slugify(title);
		var description = GetString(root, "description");
		var thumbnailUrl = GetThumbnailUrl(root);
		var channelUrl = GetChannelUrl(root, youtubeChannelId);
		var handle = GetHandle(root, channelUrl);
		long? subscriberCount = GetOptionalLong(root, "channel_follower_count") ?? GetOptionalLong(root, "subscriber_count");
		long? videoCount = GetOptionalLong(root, "playlist_count") ?? GetOptionalLong(root, "channel_video_count");

		return new ChannelResultMap(
			YoutubeChannelId: youtubeChannelId ?? "",
			Title: title ?? "Channel",
			TitleSlug: titleSlug,
			Description: description,
			ThumbnailUrl: thumbnailUrl,
			ChannelUrl: channelUrl,
			Handle: handle,
			SubscriberCount: subscriberCount,
			VideoCount: videoCount
		);
	}

	/// <summary>Channel ID: prefer channel_id, then uploader_id. Do not use id (often video id).</summary>
	static string? GetChannelId(JsonElement root)
	{
		if (root.TryGetProperty("channel_id", out var cid) && cid.ValueKind == JsonValueKind.String)
			return cid.GetString();
		if (root.TryGetProperty("uploader_id", out var uid) && uid.ValueKind == JsonValueKind.String)
			return uid.GetString();
		return null;
	}

	/// <summary>Title: channel, then uploader, then title for channel-page extraction.</summary>
	static string? GetTitle(JsonElement root)
	{
		if (root.TryGetProperty("channel", out var ch) && ch.ValueKind == JsonValueKind.String)
			return ch.GetString()?.Trim();
		if (root.TryGetProperty("uploader", out var up) && up.ValueKind == JsonValueKind.String)
			return up.GetString()?.Trim();
		if (root.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String)
			return t.GetString()?.Trim();
		return null;
	}

	static string? GetString(JsonElement root, string name)
	{
		if (root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String)
			return p.GetString();
		return null;
	}

	/// <summary>Thumbnail: thumbnail, or best from thumbnails array.</summary>
	static string? GetThumbnailUrl(JsonElement root)
	{
		if (root.TryGetProperty("thumbnail", out var thumb) && thumb.ValueKind == JsonValueKind.String)
			return thumb.GetString()?.Trim();
		if (root.TryGetProperty("thumbnails", out var thumbs) && thumbs.ValueKind == JsonValueKind.Array && thumbs.GetArrayLength() > 0)
		{
			// Prefer last (often highest resolution)
			var idx = thumbs.GetArrayLength() - 1;
			var first = thumbs[idx];
			if (first.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String)
				return u.GetString()?.Trim();
		}
		return null;
	}

	/// <summary>Channel URL: channel_url, uploader_url, or build from channel ID.</summary>
	static string? GetChannelUrl(JsonElement root, string? channelId)
	{
		if (root.TryGetProperty("channel_url", out var cu) && cu.ValueKind == JsonValueKind.String)
			return cu.GetString()?.Trim();
		if (root.TryGetProperty("uploader_url", out var uu) && uu.ValueKind == JsonValueKind.String)
			return uu.GetString()?.Trim();
		if (!string.IsNullOrWhiteSpace(channelId) && channelId.StartsWith("UC", StringComparison.OrdinalIgnoreCase))
			return $"https://www.youtube.com/channel/{channelId}";
		return null;
	}

	/// <summary>Handle: extract from channel_url if form /@... or channel_url contains /@.</summary>
	static string? GetHandle(JsonElement root, string? channelUrl)
	{
		if (string.IsNullOrWhiteSpace(channelUrl)) return null;
		var idx = channelUrl.IndexOf("/@", StringComparison.OrdinalIgnoreCase);
		if (idx < 0) return null;
		var after = channelUrl.AsSpan(idx + 2);
		var end = after.IndexOfAny('/', '?', '#');
		return end < 0 ? after.ToString() : after.Slice(0, end).ToString();
	}

	static long? GetOptionalLong(JsonElement root, string name)
	{
		if (!root.TryGetProperty(name, out var p)) return null;
		if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var v))
			return v;
		return null;
	}

	public readonly record struct ChannelResultMap(
		string YoutubeChannelId,
		string Title,
		string TitleSlug,
		string? Description,
		string? ThumbnailUrl,
		string? ChannelUrl,
		string? Handle,
		long? SubscriberCount,
		long? VideoCount
	);
}
