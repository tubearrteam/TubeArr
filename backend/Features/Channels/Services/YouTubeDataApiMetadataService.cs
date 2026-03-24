using System.Globalization;
using System.Text.Json;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

public static class YouTubeApiMetadataPriorityItems
{
	public const string ChannelSearch = "channelSearch";
	public const string ChannelResolve = "channelResolve";
	public const string ChannelMetadata = "channelMetadata";
	public const string VideoListing = "videoListing";
	public const string VideoDetails = "videoDetails";

	public static readonly string[] All =
	[
		ChannelSearch,
		ChannelResolve,
		ChannelMetadata,
		VideoListing,
		VideoDetails
	];
}

public sealed record YouTubeApiMetadataPreference(
	bool UseYouTubeApi,
	HashSet<string> PriorityItems)
{
	public bool IsPrioritized(string item)
	{
		return PriorityItems.Contains(item);
	}
}

public sealed record YouTubeApiChannelVideoDiscoveryResult(
	IReadOnlyList<ChannelVideoDiscoveryItem> Items,
	int PlaylistItemsPageCount,
	int VideosListBatchCallCount,
	bool EnrichmentSkipped);

public sealed record YouTubeApiVideoMetadataBatchResult(
	IReadOnlyDictionary<string, VideoWatchPageMetadata> MetadataByYoutubeId,
	int BatchCallCount);

public sealed class YouTubeDataApiMetadataService
{
	readonly IHttpClientFactory _httpClientFactory;
	readonly ILogger<YouTubeDataApiMetadataService> _logger;

	public YouTubeDataApiMetadataService(IHttpClientFactory httpClientFactory, ILogger<YouTubeDataApiMetadataService> logger)
	{
		_httpClientFactory = httpClientFactory;
		_logger = logger;
	}

	public static string SerializePriorityItems(IEnumerable<string>? items)
	{
		if (items is null)
			return JsonSerializer.Serialize(YouTubeApiMetadataPriorityItems.All);

		var normalized = items
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Select(x => x.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Where(x => YouTubeApiMetadataPriorityItems.All.Contains(x, StringComparer.OrdinalIgnoreCase))
			.ToArray();

		if (normalized.Length == 0)
			return JsonSerializer.Serialize(Array.Empty<string>());

		return JsonSerializer.Serialize(normalized);
	}

	public static IReadOnlyList<string> ParsePriorityItems(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
			return Array.Empty<string>();

		try
		{
			var values = JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
			var normalized = values
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.Select(x => x.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Where(x => YouTubeApiMetadataPriorityItems.All.Contains(x, StringComparer.OrdinalIgnoreCase))
				.ToArray();

			return normalized.Length == 0 ? Array.Empty<string>() : normalized;
		}
		catch
		{
			return Array.Empty<string>();
		}
	}

	public async Task<YouTubeApiMetadataPreference> GetPreferenceAsync(TubeArrDbContext db, CancellationToken ct = default)
	{
		var config = await db.YouTubeConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		var priorityItems = ParsePriorityItems(config?.ApiPriorityMetadataItemsJson);

		return new YouTubeApiMetadataPreference(
			UseYouTubeApi: config?.UseYouTubeApi ?? false,
			PriorityItems: new HashSet<string>(priorityItems, StringComparer.OrdinalIgnoreCase));
	}

	public async Task<ChannelPageMetadata?> TryGetChannelMetadataAsync(
		TubeArrDbContext db,
		string youtubeChannelId,
		CancellationToken ct = default)
	{
		if (!ChannelResolveHelper.LooksLikeYouTubeChannelId(youtubeChannelId))
			return null;

		var config = await db.YouTubeConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		if (config is null || !config.UseYouTubeApi || string.IsNullOrWhiteSpace(config.ApiKey))
			return null;

		try
		{
			var client = _httpClientFactory.CreateClient("YouTubeDataApi");
			var url = $"channels?part=snippet,brandingSettings&id={Uri.EscapeDataString(youtubeChannelId)}&key={Uri.EscapeDataString(config.ApiKey)}";
			using var response = await client.GetAsync(url, ct);
			if (!response.IsSuccessStatusCode)
			{
				_logger.LogDebug("YouTube Data API channel metadata request failed status={StatusCode} channelId={ChannelId}", (int)response.StatusCode, youtubeChannelId);
				return null;
			}

			using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
			if (!TryGetFirstItem(document.RootElement, out var item))
				return null;

			var snippet = GetObject(item, "snippet");
			var brandingSettings = GetObject(item, "brandingSettings");
			var image = GetObject(brandingSettings, "image");

			var title = GetString(snippet, "title");
			var description = GetString(snippet, "description");
			var thumbnail = GetBestThumbnail(snippet);
			var banner = GetString(image, "bannerExternalUrl");

			return new ChannelPageMetadata(
				YoutubeChannelId: youtubeChannelId,
				Title: string.IsNullOrWhiteSpace(title) ? null : title,
				Description: string.IsNullOrWhiteSpace(description) ? null : description,
				ThumbnailUrl: string.IsNullOrWhiteSpace(thumbnail) ? null : thumbnail,
				BannerUrl: string.IsNullOrWhiteSpace(banner) ? null : banner,
				CanonicalUrl: $"https://www.youtube.com/channel/{youtubeChannelId}");
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "YouTube Data API channel metadata request failed for {ChannelId}", youtubeChannelId);
			return null;
		}
	}

	public async Task<YouTubeApiChannelVideoDiscoveryResult> TryDiscoverChannelVideosAsync(
		TubeArrDbContext db,
		string youtubeChannelId,
		CancellationToken ct = default)
	{
		if (!ChannelResolveHelper.LooksLikeYouTubeChannelId(youtubeChannelId))
			return new YouTubeApiChannelVideoDiscoveryResult(Array.Empty<ChannelVideoDiscoveryItem>(), 0, 0, true);

		var config = await db.YouTubeConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		if (config is null || !config.UseYouTubeApi || string.IsNullOrWhiteSpace(config.ApiKey))
			return new YouTubeApiChannelVideoDiscoveryResult(Array.Empty<ChannelVideoDiscoveryItem>(), 0, 0, true);

		try
		{
			var client = _httpClientFactory.CreateClient("YouTubeDataApi");
			var uploadsPlaylistId = await TryGetUploadsPlaylistIdAsync(client, youtubeChannelId, config.ApiKey, ct);
			if (string.IsNullOrWhiteSpace(uploadsPlaylistId))
				return new YouTubeApiChannelVideoDiscoveryResult(Array.Empty<ChannelVideoDiscoveryItem>(), 0, 0, true);

			var items = new List<ChannelVideoDiscoveryItem>();
			var seenVideoIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var pageCount = 0;
			string? pageToken = null;
			string? previousPageToken = null;

			do
			{
				var url = $"playlistItems?part=snippet,contentDetails&playlistId={Uri.EscapeDataString(uploadsPlaylistId)}&maxResults=50&key={Uri.EscapeDataString(config.ApiKey)}";
				if (!string.IsNullOrWhiteSpace(pageToken))
					url += $"&pageToken={Uri.EscapeDataString(pageToken)}";

				using var response = await client.GetAsync(url, ct);
				if (!response.IsSuccessStatusCode)
				{
					_logger.LogDebug(
						"YouTube Data API playlist items request failed status={StatusCode} channelId={ChannelId} playlistId={PlaylistId}",
						(int)response.StatusCode,
						youtubeChannelId,
						uploadsPlaylistId);
					break;
				}

				using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
				pageCount++;
				CollectPlaylistItems(document.RootElement, items, seenVideoIds);
				previousPageToken = pageToken;
				pageToken = GetString(document.RootElement, "nextPageToken");
			}
			while (!string.IsNullOrWhiteSpace(pageToken) && !string.Equals(pageToken, previousPageToken, StringComparison.Ordinal));

			_logger.LogInformation(
				"YouTube Data API uploads discovery completed for {ChannelId}: playlistItemsPages={PlaylistItemsPages} videosDiscovered={VideosDiscovered} videosListBatchCalls={VideosListBatchCalls} enrichmentSkipped={EnrichmentSkipped}",
				youtubeChannelId,
				pageCount,
				items.Count,
				0,
				true);

			return new YouTubeApiChannelVideoDiscoveryResult(items, pageCount, 0, true);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "YouTube Data API uploads playlist request failed for {ChannelId}", youtubeChannelId);
			return new YouTubeApiChannelVideoDiscoveryResult(Array.Empty<ChannelVideoDiscoveryItem>(), 0, 0, true);
		}
	}

	public async Task<YouTubeApiVideoMetadataBatchResult> TryGetVideoMetadataBatchAsync(
		TubeArrDbContext db,
		IEnumerable<string> youtubeVideoIds,
		CancellationToken ct = default)
	{
		var distinctVideoIds = youtubeVideoIds
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Select(x => x.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();

		if (distinctVideoIds.Length == 0)
			return new YouTubeApiVideoMetadataBatchResult(new Dictionary<string, VideoWatchPageMetadata>(StringComparer.OrdinalIgnoreCase), 0);

		var config = await db.YouTubeConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		if (config is null || !config.UseYouTubeApi || string.IsNullOrWhiteSpace(config.ApiKey))
			return new YouTubeApiVideoMetadataBatchResult(new Dictionary<string, VideoWatchPageMetadata>(StringComparer.OrdinalIgnoreCase), 0);

		var metadataByYoutubeId = new Dictionary<string, VideoWatchPageMetadata>(StringComparer.OrdinalIgnoreCase);
		var batchCallCount = 0;

		try
		{
			var client = _httpClientFactory.CreateClient("YouTubeDataApi");
			foreach (var batch in distinctVideoIds.Chunk(50))
			{
				ct.ThrowIfCancellationRequested();

				var joinedIds = string.Join(',', batch.Select(Uri.EscapeDataString));
				var url = $"videos?part=snippet,contentDetails&id={joinedIds}&key={Uri.EscapeDataString(config.ApiKey)}";

				using var response = await client.GetAsync(url, ct);
				if (!response.IsSuccessStatusCode)
				{
					_logger.LogDebug(
						"YouTube Data API batch video metadata request failed status={StatusCode} videoCount={VideoCount}",
						(int)response.StatusCode,
						batch.Length);
					continue;
				}

				batchCallCount++;
				using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
				CollectVideoMetadataItems(document.RootElement, metadataByYoutubeId);
			}

			_logger.LogInformation(
				"YouTube Data API batch video metadata completed: requestedVideos={RequestedVideos} returnedVideos={ReturnedVideos} videosListBatchCalls={VideosListBatchCalls}",
				distinctVideoIds.Length,
				metadataByYoutubeId.Count,
				batchCallCount);

			return new YouTubeApiVideoMetadataBatchResult(metadataByYoutubeId, batchCallCount);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "YouTube Data API batch video metadata request failed for {VideoCount} video(s)", distinctVideoIds.Length);
			return new YouTubeApiVideoMetadataBatchResult(metadataByYoutubeId, batchCallCount);
		}
	}

	public async Task<VideoWatchPageMetadata?> TryGetVideoMetadataAsync(
		TubeArrDbContext db,
		string youtubeVideoId,
		CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(youtubeVideoId))
			return null;

		var config = await db.YouTubeConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		if (config is null || !config.UseYouTubeApi || string.IsNullOrWhiteSpace(config.ApiKey))
			return null;

		try
		{
			var client = _httpClientFactory.CreateClient("YouTubeDataApi");
			var url = $"videos?part=snippet,contentDetails&id={Uri.EscapeDataString(youtubeVideoId)}&key={Uri.EscapeDataString(config.ApiKey)}";
			using var response = await client.GetAsync(url, ct);
			if (!response.IsSuccessStatusCode)
			{
				_logger.LogDebug("YouTube Data API video metadata request failed status={StatusCode} videoId={VideoId}", (int)response.StatusCode, youtubeVideoId);
				return null;
			}

			using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
			if (!TryGetFirstItem(document.RootElement, out var item))
				return null;

			return TryCreateVideoMetadata(item, out var metadata) ? metadata : null;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "YouTube Data API video metadata request failed for {VideoId}", youtubeVideoId);
			return null;
		}
	}

	async Task<string?> TryGetUploadsPlaylistIdAsync(HttpClient client, string youtubeChannelId, string apiKey, CancellationToken ct)
	{
		var url = $"channels?part=contentDetails&id={Uri.EscapeDataString(youtubeChannelId)}&key={Uri.EscapeDataString(apiKey)}";
		using var response = await client.GetAsync(url, ct);
		if (!response.IsSuccessStatusCode)
		{
			_logger.LogDebug("YouTube Data API uploads playlist resolve failed status={StatusCode} channelId={ChannelId}", (int)response.StatusCode, youtubeChannelId);
			return null;
		}

		using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
		if (!TryGetFirstItem(document.RootElement, out var item))
			return null;

		var contentDetails = GetObject(item, "contentDetails");
		var relatedPlaylists = GetObject(contentDetails, "relatedPlaylists");
		return GetString(relatedPlaylists, "uploads");
	}

	static void CollectPlaylistItems(JsonElement root, List<ChannelVideoDiscoveryItem> items, HashSet<string> seenVideoIds)
	{
		if (root.ValueKind != JsonValueKind.Object)
			return;

		if (!root.TryGetProperty("items", out var apiItems) || apiItems.ValueKind != JsonValueKind.Array)
			return;

		foreach (var entry in apiItems.EnumerateArray())
		{
			if (TryCreateDiscoveryItem(entry, out var item) && seenVideoIds.Add(item.YoutubeVideoId))
				items.Add(item);
		}
	}

	static bool TryCreateDiscoveryItem(JsonElement entry, out ChannelVideoDiscoveryItem item)
	{
		item = default!;
		if (entry.ValueKind != JsonValueKind.Object)
			return false;

		var snippet = GetObject(entry, "snippet");
		var contentDetails = GetObject(entry, "contentDetails");
		var youtubeVideoId = GetString(contentDetails, "videoId");
		if (string.IsNullOrWhiteSpace(youtubeVideoId))
			return false;

		var title = GetString(snippet, "title");
		if (string.Equals(title, "Deleted video", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(title, "Private video", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		var publishedUtc = ParseDateTimeOffset(GetString(contentDetails, "videoPublishedAt"))
			?? ParseDateTimeOffset(GetString(snippet, "publishedAt"));

		item = new ChannelVideoDiscoveryItem(
			YoutubeVideoId: youtubeVideoId,
			Title: string.IsNullOrWhiteSpace(title) ? null : title,
			ThumbnailUrl: GetBestThumbnail(snippet),
			Runtime: null,
			PublishedUtc: publishedUtc,
			Description: GetString(snippet, "description"));
		return true;
	}

	static void CollectVideoMetadataItems(JsonElement root, Dictionary<string, VideoWatchPageMetadata> metadataByYoutubeId)
	{
		if (root.ValueKind != JsonValueKind.Object)
			return;

		if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
			return;

		foreach (var entry in items.EnumerateArray())
		{
			if (TryCreateVideoMetadata(entry, out var metadata))
				metadataByYoutubeId[metadata.YoutubeVideoId] = metadata;
		}
	}

	static bool TryCreateVideoMetadata(JsonElement item, out VideoWatchPageMetadata metadata)
	{
		metadata = default!;
		if (item.ValueKind != JsonValueKind.Object)
			return false;

		var youtubeVideoId = GetString(item, "id");
		if (string.IsNullOrWhiteSpace(youtubeVideoId))
			return false;

		var snippet = GetObject(item, "snippet");
		var contentDetails = GetObject(item, "contentDetails");

		var title = GetString(snippet, "title");
		var description = GetString(snippet, "description");
		var thumbnail = GetBestThumbnail(snippet);
		var publishedAt = ParseDateTimeOffset(GetString(snippet, "publishedAt"));
		var runtime = ParseIso8601DurationSeconds(GetString(contentDetails, "duration"));
		var airDate = publishedAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

		metadata = new VideoWatchPageMetadata(
			YoutubeVideoId: youtubeVideoId,
			Title: string.IsNullOrWhiteSpace(title) ? null : title,
			Description: string.IsNullOrWhiteSpace(description) ? null : description,
			ThumbnailUrl: string.IsNullOrWhiteSpace(thumbnail) ? null : thumbnail,
			UploadDateUtc: publishedAt,
			AirDateUtc: publishedAt,
			AirDate: string.IsNullOrWhiteSpace(airDate) ? null : airDate,
			Overview: string.IsNullOrWhiteSpace(description) ? null : description,
			Runtime: runtime);
		return true;
	}

	static bool TryGetFirstItem(JsonElement root, out JsonElement item)
	{
		item = default;
		if (root.ValueKind != JsonValueKind.Object)
			return false;

		if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
			return false;

		foreach (var entry in items.EnumerateArray())
		{
			if (entry.ValueKind == JsonValueKind.Object)
			{
				item = entry;
				return true;
			}
		}

		return false;
	}

	static JsonElement GetObject(JsonElement element, string name)
	{
		if (element.ValueKind == JsonValueKind.Object &&
			element.TryGetProperty(name, out var value) &&
			value.ValueKind == JsonValueKind.Object)
		{
			return value;
		}

		return default;
	}

	static string? GetString(JsonElement element, string name)
	{
		if (element.ValueKind == JsonValueKind.Object &&
			element.TryGetProperty(name, out var value) &&
			value.ValueKind == JsonValueKind.String)
		{
			return value.GetString()?.Trim();
		}

		return null;
	}

	static string? GetBestThumbnail(JsonElement snippet)
	{
		if (snippet.ValueKind != JsonValueKind.Object ||
			!snippet.TryGetProperty("thumbnails", out var thumbnails) ||
			thumbnails.ValueKind != JsonValueKind.Object)
		{
			return null;
		}

		var order = new[] { "maxres", "standard", "high", "medium", "default" };
		foreach (var key in order)
		{
			if (thumbnails.TryGetProperty(key, out var thumb) && thumb.ValueKind == JsonValueKind.Object)
			{
				var url = GetString(thumb, "url");
				if (!string.IsNullOrWhiteSpace(url))
					return url;
			}
		}

		return null;
	}

	static DateTimeOffset? ParseDateTimeOffset(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
			? parsed
			: null;
	}

	static int? ParseIso8601DurationSeconds(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		try
		{
			return (int)XmlConvert.ToTimeSpan(value).TotalSeconds;
		}
		catch
		{
			return null;
		}
	}
}
