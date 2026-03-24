using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TubeArr.Backend;

public sealed record ChannelVideoDiscoveryItem(
	string YoutubeVideoId,
	string? Title,
	string? ThumbnailUrl,
	int? Runtime,
	DateTimeOffset? PublishedUtc = null,
	string? Description = null);

public sealed class ChannelVideoDiscoveryService
{
	readonly IHttpClientFactory _httpClientFactory;
	readonly ILogger<ChannelVideoDiscoveryService> _logger;

	public ChannelVideoDiscoveryService(IHttpClientFactory httpClientFactory, ILogger<ChannelVideoDiscoveryService> logger)
	{
		_httpClientFactory = httpClientFactory;
		_logger = logger;
	}

	public async Task<IReadOnlyList<ChannelVideoDiscoveryItem>> DiscoverVideosAsync(string youtubeChannelId, CancellationToken ct = default)
	{
		if (!ChannelResolveHelper.LooksLikeYouTubeChannelId(youtubeChannelId))
			return Array.Empty<ChannelVideoDiscoveryItem>();

		var client = _httpClientFactory.CreateClient("YouTubePage");
		var url = ChannelResolveHelper.GetCanonicalChannelVideosUrl(youtubeChannelId);
		using var response = await client.GetAsync(url, ct);
		if (!response.IsSuccessStatusCode)
		{
			_logger.LogDebug("Channel videos page request failed status={StatusCode} channelId={ChannelId}", (int)response.StatusCode, youtubeChannelId);
			return Array.Empty<ChannelVideoDiscoveryItem>();
		}

		var html = await response.Content.ReadAsStringAsync(ct);
		return await DiscoverVideosFromHtmlAsync(client, html, ct);
	}

	public static IReadOnlyList<ChannelVideoDiscoveryItem> ParseListingHtml(string html)
	{
		using var initialData = YouTubePageJsonHelper.TryExtractJsonDocument(
			html,
			"var ytInitialData = ",
			"window[\"ytInitialData\"] = ",
			"ytInitialData = ");

		if (initialData is null)
			return Array.Empty<ChannelVideoDiscoveryItem>();

		var items = new List<ChannelVideoDiscoveryItem>();
		var seenVideoIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		CollectVideoItems(initialData.RootElement, items, seenVideoIds);
		return items;
	}

	async Task<IReadOnlyList<ChannelVideoDiscoveryItem>> DiscoverVideosFromHtmlAsync(HttpClient client, string html, CancellationToken ct)
	{
		using var initialData = YouTubePageJsonHelper.TryExtractJsonDocument(
			html,
			"var ytInitialData = ",
			"window[\"ytInitialData\"] = ",
			"ytInitialData = ");
		if (initialData is null)
			return Array.Empty<ChannelVideoDiscoveryItem>();

		var items = new List<ChannelVideoDiscoveryItem>();
		var seenVideoIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		CollectVideoItems(initialData.RootElement, items, seenVideoIds);

		using var ytcfg = YouTubePageJsonHelper.TryExtractJsonDocument(html, "ytcfg.set(");
		var apiKey = YouTubePageJsonHelper.TryExtractInnertubeApiKey(html, ytcfg);
		var contextJson = YouTubePageJsonHelper.TryExtractInnertubeContextJson(ytcfg);

		if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(contextJson))
			return items;

		var continuationToken = ExtractContinuationToken(initialData.RootElement);
		var seenContinuations = new HashSet<string>(StringComparer.Ordinal);

		while (!string.IsNullOrWhiteSpace(continuationToken) && seenContinuations.Add(continuationToken))
		{
			using var request = new HttpRequestMessage(HttpMethod.Post, $"youtubei/v1/browse?key={Uri.EscapeDataString(apiKey)}")
			{
				Content = new StringContent(
					$@"{{""context"":{contextJson},""continuation"":""{JsonEncodedText.Encode(continuationToken).ToString()}""}}",
					Encoding.UTF8,
					"application/json")
			};

			using var response = await client.SendAsync(request, ct);
			if (!response.IsSuccessStatusCode)
			{
				_logger.LogDebug("Channel continuation request failed status={StatusCode}", (int)response.StatusCode);
				break;
			}

			await using var responseStream = await response.Content.ReadAsStreamAsync(ct);
			using var doc = await JsonDocument.ParseAsync(responseStream, cancellationToken: ct);
			CollectVideoItems(doc.RootElement, items, seenVideoIds);
			continuationToken = ExtractContinuationToken(doc.RootElement);
		}

		return items;
	}

	static void CollectVideoItems(JsonElement element, List<ChannelVideoDiscoveryItem> items, HashSet<string> seenVideoIds)
	{
		if (TryCreateVideoItem(element, out var item) &&
			seenVideoIds.Add(item.YoutubeVideoId))
		{
			items.Add(item);
		}

		switch (element.ValueKind)
		{
			case JsonValueKind.Object:
				foreach (var property in element.EnumerateObject())
					CollectVideoItems(property.Value, items, seenVideoIds);
				break;
			case JsonValueKind.Array:
				foreach (var child in element.EnumerateArray())
					CollectVideoItems(child, items, seenVideoIds);
				break;
		}
	}

	static bool TryCreateVideoItem(JsonElement element, out ChannelVideoDiscoveryItem item)
	{
		item = default!;
		if (element.ValueKind != JsonValueKind.Object)
			return false;

		var youtubeVideoId = GetString(element, "videoId");
		if (string.IsNullOrWhiteSpace(youtubeVideoId))
			return false;

		var title = GetTextFromField(element, "title")
			?? GetTextFromField(element, "headline")
			?? GetString(element, "title");
		var thumbnailUrl = GetBestThumbnailUrl(element);
		var runtime = ParseDurationSeconds(
			GetTextFromField(element, "lengthText")
			?? GetTextFromThumbnailOverlay(element)
			?? GetTextFromField(element, "thumbnailOverlayTimeStatusRenderer"));

		if (string.IsNullOrWhiteSpace(title) &&
			string.IsNullOrWhiteSpace(thumbnailUrl) &&
			!HasWatchEndpoint(element, youtubeVideoId))
		{
			return false;
		}

		item = new ChannelVideoDiscoveryItem(
			YoutubeVideoId: youtubeVideoId!,
			Title: string.IsNullOrWhiteSpace(title) ? null : title,
			ThumbnailUrl: string.IsNullOrWhiteSpace(thumbnailUrl) ? null : thumbnailUrl,
			Runtime: runtime);
		return true;
	}

	static bool HasWatchEndpoint(JsonElement element, string youtubeVideoId)
	{
		if (element.TryGetProperty("navigationEndpoint", out var navigationEndpoint) &&
			navigationEndpoint.ValueKind == JsonValueKind.Object &&
			navigationEndpoint.TryGetProperty("watchEndpoint", out var watchEndpoint) &&
			watchEndpoint.ValueKind == JsonValueKind.Object)
		{
			var endpointVideoId = GetString(watchEndpoint, "videoId");
			return string.Equals(endpointVideoId, youtubeVideoId, StringComparison.OrdinalIgnoreCase);
		}

		return false;
	}

	static string? ExtractContinuationToken(JsonElement element)
	{
		if (element.ValueKind == JsonValueKind.Object)
		{
			if (element.TryGetProperty("continuationCommand", out var continuationCommand) &&
				continuationCommand.ValueKind == JsonValueKind.Object)
			{
				var token = GetString(continuationCommand, "token");
				if (!string.IsNullOrWhiteSpace(token))
					return token;
			}

			foreach (var property in element.EnumerateObject())
			{
				var token = ExtractContinuationToken(property.Value);
				if (!string.IsNullOrWhiteSpace(token))
					return token;
			}
		}
		else if (element.ValueKind == JsonValueKind.Array)
		{
			foreach (var child in element.EnumerateArray())
			{
				var token = ExtractContinuationToken(child);
				if (!string.IsNullOrWhiteSpace(token))
					return token;
			}
		}

		return null;
	}

	static string? GetTextFromThumbnailOverlay(JsonElement element)
	{
		if (!element.TryGetProperty("thumbnailOverlays", out var thumbnailOverlays) || thumbnailOverlays.ValueKind != JsonValueKind.Array)
			return null;

		foreach (var overlay in thumbnailOverlays.EnumerateArray())
		{
			var text = GetTextFromField(overlay, "text")
				?? GetTextFromField(overlay, "thumbnailOverlayTimeStatusRenderer")
				?? GetTextFromField(overlay, "thumbnailOverlayBottomPanelRenderer");
			if (!string.IsNullOrWhiteSpace(text))
				return text;
		}

		return null;
	}

	static string? GetTextFromField(JsonElement element, string propertyName)
	{
		if (!element.TryGetProperty(propertyName, out var property))
			return null;

		return GetText(property);
	}

	static string? GetText(JsonElement element)
	{
		return element.ValueKind switch
		{
			JsonValueKind.String => element.GetString(),
			JsonValueKind.Object => GetTextFromObject(element),
			_ => null
		};
	}

	static string? GetTextFromObject(JsonElement element)
	{
		if (element.TryGetProperty("simpleText", out var simpleText) &&
			simpleText.ValueKind == JsonValueKind.String)
			return simpleText.GetString();

		if (element.TryGetProperty("text", out var textElement))
			return GetText(textElement);

		if (element.TryGetProperty("runs", out var runs) && runs.ValueKind == JsonValueKind.Array)
		{
			var builder = new StringBuilder();
			foreach (var run in runs.EnumerateArray())
			{
				var text = GetString(run, "text");
				if (!string.IsNullOrWhiteSpace(text))
					builder.Append(text);
			}

			return builder.Length == 0 ? null : builder.ToString();
		}

		return null;
	}

	static string? GetBestThumbnailUrl(JsonElement element)
	{
		if (element.TryGetProperty("thumbnail", out var thumbnail) &&
			thumbnail.ValueKind == JsonValueKind.Object &&
			thumbnail.TryGetProperty("thumbnails", out var thumbnails) &&
			thumbnails.ValueKind == JsonValueKind.Array)
		{
			return GetLastThumbnailUrl(thumbnails);
		}

		return null;
	}

	static string? GetLastThumbnailUrl(JsonElement thumbnails)
	{
		string? value = null;
		foreach (var thumbnail in thumbnails.EnumerateArray())
		{
			var candidate = GetString(thumbnail, "url");
			if (!string.IsNullOrWhiteSpace(candidate))
				value = candidate;
		}

		return value;
	}

	static int? ParseDurationSeconds(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		var normalized = value.Trim();
		if (normalized.Equals("LIVE", StringComparison.OrdinalIgnoreCase))
			return 0;

		var parts = normalized.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (parts.Length is < 2 or > 3)
			return null;

		if (!parts.All(part => int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
			return null;

		return parts.Length switch
		{
			2 => (int.Parse(parts[0], CultureInfo.InvariantCulture) * 60) + int.Parse(parts[1], CultureInfo.InvariantCulture),
			3 => (int.Parse(parts[0], CultureInfo.InvariantCulture) * 3600)
				+ (int.Parse(parts[1], CultureInfo.InvariantCulture) * 60)
				+ int.Parse(parts[2], CultureInfo.InvariantCulture),
			_ => null
		};
	}

	static string? GetString(JsonElement element, string propertyName)
	{
		if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
			return null;

		return property.GetString()?.Trim();
	}
}
