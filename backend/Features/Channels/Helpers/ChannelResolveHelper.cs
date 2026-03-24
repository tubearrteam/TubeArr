using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TubeArr.Backend;

/// <summary>
/// Helpers for channel add/search: input classification, canonical URL normalization,
/// and HTML extraction of channel ID from YouTube pages.
/// UC... is the canonical stored identifier for YouTube channels.
/// </summary>
public static class ChannelResolveHelper
{
	public static readonly Regex ChannelIdRegex = new(@"^UC[0-9A-Za-z_-]{22}$", RegexOptions.Compiled);
	static readonly Regex ChannelRssUrlRegex = new(
		@"""rssUrl""\s*:\s*""(https?:\/\/[^""]+)""",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);
	static readonly Regex ChannelRssLinkRegex = new(
		@"<link[^>]+type=[""']application/(?:rss|atom)\+xml[""'][^>]+href=[""']([^""']+)[""']",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	public enum ChannelInputKind
	{
		Empty,
		ChannelId,
		Handle,
		ChannelUrl,
		SearchTerm,
		Unknown
	}

	/// <summary>Result of classifying user input for resolve vs search.</summary>
	public readonly record struct ChannelInputClassification(
		ChannelInputKind Kind,
		string? ChannelId,
		string? CanonicalUrl,
		string? Handle
	);

	/// <summary>Classify input for add-channel flow. Authoritative classification for resolve vs search.</summary>
	public static ChannelInputClassification ClassifyInput(string? input)
	{
		var trimmed = (input ?? "").Trim();
		if (string.IsNullOrWhiteSpace(trimmed))
			return new ChannelInputClassification(ChannelInputKind.Empty, null, null, null);

		// Bare channel ID
		if (IsValidChannelId(trimmed))
			return new ChannelInputClassification(ChannelInputKind.ChannelId, trimmed, GetCanonicalChannelVideosUrl(trimmed), null);

		// @handle (no URL)
		if (Regex.IsMatch(trimmed, @"^@[^/?#\s]+$", RegexOptions.IgnoreCase))
		{
			var handle = trimmed.TrimStart('@');
			return new ChannelInputClassification(ChannelInputKind.Handle, null, $"https://www.youtube.com/@{handle}/videos", handle);
		}

		// Ensure URL-like strings have protocol
		var normalized = trimmed;
		if (!normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
		{
			if (normalized.StartsWith("www.", StringComparison.OrdinalIgnoreCase) || normalized.StartsWith("m.", StringComparison.OrdinalIgnoreCase))
				normalized = "https://" + normalized;
			else if (normalized.Contains("youtube.com", StringComparison.OrdinalIgnoreCase))
				normalized = "https://" + normalized;
			else
				return new ChannelInputClassification(ChannelInputKind.SearchTerm, null, null, null);
		}

		if (!normalized.Contains("youtube.com", StringComparison.OrdinalIgnoreCase))
			return new ChannelInputClassification(ChannelInputKind.SearchTerm, null, null, null);

		// /channel/UC...
		var fromChannelUrl = TryExtractChannelIdFromChannelUrl(normalized);
		if (fromChannelUrl != null)
			return new ChannelInputClassification(ChannelInputKind.ChannelUrl, fromChannelUrl, GetCanonicalChannelVideosUrl(fromChannelUrl), null);

		// /@handle
		var handleMatch = Regex.Match(normalized, @"youtube\.com/@([^/?#]+)", RegexOptions.IgnoreCase);
		if (handleMatch.Success)
		{
			var handle = handleMatch.Groups[1].Value;
			var canonical = normalized.Contains("/videos", StringComparison.OrdinalIgnoreCase) || normalized.Contains("/streams", StringComparison.OrdinalIgnoreCase) || normalized.Contains("/playlists", StringComparison.OrdinalIgnoreCase)
				? $"https://www.youtube.com/@{handle}/videos"
				: $"https://www.youtube.com/@{handle}/videos";
			return new ChannelInputClassification(ChannelInputKind.Handle, null, canonical, handle);
		}

		// /user/... or /c/... or other YouTube URL â€“ resolvable via yt-dlp or HTTP
		if (Regex.IsMatch(normalized, @"youtube\.com/(user|c)/", RegexOptions.IgnoreCase))
			return new ChannelInputClassification(ChannelInputKind.ChannelUrl, null, normalized.TrimEnd('/'), null);
		if (Regex.IsMatch(normalized, @"youtube\.com/channel/", RegexOptions.IgnoreCase))
			return new ChannelInputClassification(ChannelInputKind.ChannelUrl, null, normalized.TrimEnd('/') + "/videos", null);
		if (Regex.IsMatch(normalized, @"youtube\.com/@", RegexOptions.IgnoreCase))
			return new ChannelInputClassification(ChannelInputKind.Handle, null, normalized.TrimEnd('/') + "/videos", null);

		// Generic YouTube URL (e.g. landing page)
		if (Regex.IsMatch(normalized, @"youtube\.com", RegexOptions.IgnoreCase) && !Regex.IsMatch(normalized, @"youtube\.com/watch\?", RegexOptions.IgnoreCase) && !Regex.IsMatch(normalized, @"youtu\.be/", RegexOptions.IgnoreCase))
			return new ChannelInputClassification(ChannelInputKind.ChannelUrl, null, normalized, null);

		return new ChannelInputClassification(ChannelInputKind.SearchTerm, null, null, null);
	}

	/// <summary>Canonical channel videos URL for yt-dlp exact resolution (channel ID only).</summary>
	public static string GetCanonicalChannelVideosUrl(string channelId)
	{
		return $"https://www.youtube.com/channel/{(channelId ?? "").Trim()}/videos";
	}

	/// <summary>Canonical channel playlists URL for yt-dlp (channel ID only).</summary>
	public static string GetCanonicalChannelPlaylistsUrl(string channelId)
	{
		return $"https://www.youtube.com/channel/{(channelId ?? "").Trim()}/playlists";
	}

	public static bool IsValidChannelId(string? value)
	{
		return !string.IsNullOrWhiteSpace(value) && ChannelIdRegex.IsMatch(value.Trim());
	}

	public static bool LooksLikeYouTubeChannelId(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return false;
		var trimmed = value.Trim();
		return ChannelIdRegex.IsMatch(trimmed);
	}

	/// <summary>Extract UC... channel ID from a /channel/UC... URL, or null.</summary>
	public static string? TryExtractChannelIdFromChannelUrl(string input)
	{
		var trimmed = (input ?? "").Trim();
		var m = Regex.Match(trimmed, @"youtube\.com/channel/(UC[0-9A-Za-z_-]{22})", RegexOptions.IgnoreCase);
		if (m.Success && IsValidChannelId(m.Groups[1].Value))
			return m.Groups[1].Value;
		return null;
	}

	/// <summary>Extract channel ID from YouTube page HTML. Tries patterns in order: externalId (best), channelId (good fallback), /channel/UC... (last fallback).</summary>
	public static string? ExtractChannelIdFromHtml(string html)
	{
		if (string.IsNullOrWhiteSpace(html))
			return null;

		// YouTube frequently escapes slashes in JSON blobs
		html = html.Replace("\\/", "/");

		// More tolerant patterns (whitespace varies across responses)
		string[] patterns =
		{
			// Best: channel metadata
			@"""externalId""\s*:\s*""(UC[a-zA-Z0-9_-]{22})""",

			// Common in ytInitialData / page source
			@"""browseId""\s*:\s*""(UC[a-zA-Z0-9_-]{22})""",

			// Other metadata locations
			@"""channelId""\s*:\s*""(UC[a-zA-Z0-9_-]{22})""",

			// Canonical or direct channel links
			@"/channel/(UC[a-zA-Z0-9_-]{22})",

			// Fully-qualified canonical URL fallback
			@"https?:\/\/(?:www\.)?youtube\.com\/channel\/(UC[a-zA-Z0-9_-]{22})"
		};

		foreach (var pattern in patterns)
		{
			var m = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
			if (!m.Success)
				continue;
			var id = m.Groups[1].Value;
			if (IsValidChannelId(id))
				return id;
		}

		return null;
	}

	/// <summary>Extract channel title from the channelMetadataRenderer node in YouTube page HTML.</summary>
	public static string? ExtractChannelTitleFromHtml(string html)
	{
		if (string.IsNullOrWhiteSpace(html))
			return null;

		if (!TryGetChannelMetadataRenderer(html, out var initialDataDocument, out var channelMetadataRenderer))
			return null;

		using (initialDataDocument)
		{
			if (!channelMetadataRenderer.TryGetProperty("title", out var titleElement))
				return null;

			return ReadTextValue(titleElement);
		}
	}

	/// <summary>Extract channel description from the channelMetadataRenderer node in YouTube page HTML.</summary>
	public static string? ExtractChannelDescriptionFromHtml(string html)
	{
		if (string.IsNullOrWhiteSpace(html))
			return null;

		if (!TryGetChannelMetadataRenderer(html, out var initialDataDocument, out var channelMetadataRenderer))
			return null;

		using (initialDataDocument)
		{
			if (!channelMetadataRenderer.TryGetProperty("description", out var descriptionElement))
				return null;

			return ReadTextValue(descriptionElement);
		}
	}

	/// <summary>Extract channel logo/avatar from the channelMetadataRenderer node in YouTube page HTML.</summary>
	public static string? ExtractChannelLogoFromHtml(string html)
	{
		if (string.IsNullOrWhiteSpace(html))
			return null;

		if (!TryGetChannelMetadataRenderer(html, out var initialDataDocument, out var channelMetadataRenderer))
			return null;

		using (initialDataDocument)
		{
			if (!channelMetadataRenderer.TryGetProperty("avatar", out var avatarElement))
				return null;

			return ReadBestThumbnailUrl(avatarElement);
		}
	}

	/// <summary>Extract channel banner from the channelMetadataRenderer node in YouTube page HTML.</summary>
	public static string? ExtractChannelBannerFromHtml(string html)
	{
		if (string.IsNullOrWhiteSpace(html))
			return null;

		if (!TryGetChannelMetadataRenderer(html, out var initialDataDocument, out var channelMetadataRenderer))
			return null;

		using (initialDataDocument)
		{
			if (!channelMetadataRenderer.TryGetProperty("banner", out var bannerElement))
				return null;

			return ReadBestThumbnailUrl(bannerElement);
		}
	}

	/// <summary>Extract channel RSS feed URL from channelMetadataRenderer node in YouTube page HTML.</summary>
	public static string? ExtractChannelRssUrlFromHtml(string html)
	{
		if (string.IsNullOrWhiteSpace(html))
			return null;

		// YouTube frequently escapes slashes in JSON blobs.
		html = html.Replace("\\/", "/");

		// Fast path: embedded rssUrl string.
		var quick = ChannelRssUrlRegex.Match(html);
		if (quick.Success)
			return WebUtility.HtmlDecode(quick.Groups[1].Value.Trim());

		// Also present as an actual <link rel="alternate" type="application/rss+xml" href="..."> in some pages.
		var linkMatch = ChannelRssLinkRegex.Match(html);
		if (linkMatch.Success)
			return WebUtility.HtmlDecode(linkMatch.Groups[1].Value.Trim());

		// Structured path: parse ytInitialData / channelMetadataRenderer.
		if (!TryGetChannelMetadataRenderer(html, out var initialDataDocument, out var channelMetadataRenderer))
			return null;

		using (initialDataDocument)
		{
			if (!channelMetadataRenderer.TryGetProperty("rssUrl", out var rssUrlElement))
				return null;

			if (rssUrlElement.ValueKind == JsonValueKind.String)
			{
				var v = rssUrlElement.GetString()?.Trim();
				return string.IsNullOrWhiteSpace(v) ? null : v;
			}

			// Some responses represent strings as text-like objects.
			return ReadTextValue(rssUrlElement);
		}
	}

	static bool TryGetChannelMetadataRenderer(string html, out JsonDocument? initialDataDocument, out JsonElement channelMetadataRenderer)
	{
		initialDataDocument = YouTubePageJsonHelper.TryExtractJsonDocument(
			html,
			"var ytInitialData = ",
			"window[\"ytInitialData\"] = ",
			"ytInitialData = ");

		channelMetadataRenderer = default;
		if (initialDataDocument is null)
		{
			initialDataDocument = YouTubePageJsonHelper.TryExtractJsonDocument(
				html,
				"\"channelMetadataRenderer\":");
			if (initialDataDocument is null || initialDataDocument.RootElement.ValueKind != JsonValueKind.Object)
			{
				initialDataDocument?.Dispose();
				initialDataDocument = null;
				return false;
			}

			channelMetadataRenderer = initialDataDocument.RootElement;
			return true;
		}

		if (!initialDataDocument.RootElement.TryGetProperty("metadata", out var metadataElement) ||
			metadataElement.ValueKind != JsonValueKind.Object ||
			!metadataElement.TryGetProperty("channelMetadataRenderer", out channelMetadataRenderer) ||
			channelMetadataRenderer.ValueKind != JsonValueKind.Object)
		{
			initialDataDocument.Dispose();
			initialDataDocument = null;
			channelMetadataRenderer = default;
			return false;
		}

		return true;
	}

	static string? ReadTextValue(JsonElement element)
	{
		static string? NormalizeExtractedText(string? value)
		{
			var trimmed = value?.Trim();
			if (string.IsNullOrWhiteSpace(trimmed))
				return null;

			return System.Net.WebUtility.HtmlDecode(trimmed);
		}

		if (element.ValueKind == JsonValueKind.String)
			return NormalizeExtractedText(element.GetString());

		if (element.ValueKind != JsonValueKind.Object)
			return null;

		if (element.TryGetProperty("simpleText", out var simpleTextElement) &&
			simpleTextElement.ValueKind == JsonValueKind.String)
		{
			return NormalizeExtractedText(simpleTextElement.GetString());
		}

		if (element.TryGetProperty("runs", out var runsElement) &&
			runsElement.ValueKind == JsonValueKind.Array)
		{
			var pieces = new List<string>();
			foreach (var run in runsElement.EnumerateArray())
			{
				if (run.ValueKind != JsonValueKind.Object)
					continue;

				if (!run.TryGetProperty("text", out var textElement) || textElement.ValueKind != JsonValueKind.String)
					continue;

				var text = textElement.GetString();
				if (!string.IsNullOrWhiteSpace(text))
					pieces.Add(text);
			}

			return NormalizeExtractedText(string.Concat(pieces));
		}

		return null;
	}

	static string? ReadBestThumbnailUrl(JsonElement element)
	{
		if (element.ValueKind != JsonValueKind.Object)
			return null;
		if (!element.TryGetProperty("thumbnails", out var thumbnailsElement) ||
			thumbnailsElement.ValueKind != JsonValueKind.Array)
			return null;

		string? value = null;
		foreach (var thumbnailElement in thumbnailsElement.EnumerateArray())
		{
			if (thumbnailElement.ValueKind != JsonValueKind.Object)
				continue;

			if (!thumbnailElement.TryGetProperty("url", out var urlElement) || urlElement.ValueKind != JsonValueKind.String)
				continue;

			var url = urlElement.GetString()?.Trim();
			if (!string.IsNullOrWhiteSpace(url))
				value = url;
		}

		return value;
	}
}
