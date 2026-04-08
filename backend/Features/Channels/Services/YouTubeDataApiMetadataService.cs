using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
	public const string LivestreamIdentification = "livestreamIdentification";

	public static readonly string[] All =
	[
		ChannelSearch,
		ChannelResolve,
		ChannelMetadata,
		VideoListing,
		VideoDetails,
		LivestreamIdentification
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

public sealed partial class YouTubeDataApiMetadataService
{
	internal const string VideosListParts = "snippet,contentDetails,statistics,status,liveStreamingDetails";

	static readonly string[] VideosListPersistedPartNames =
		["snippet", "contentDetails", "statistics", "status", "liveStreamingDetails"];

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

	/// <summary>
	/// When <see cref="YouTubeConfigEntity.UseYouTubeApi"/> is true, probes the Data API with
	/// <c>channels.list</c> (1 quota unit) using Google's sample channel id.
	/// </summary>
	public async Task<Dictionary<string, object?>?> TryBuildHealthCheckAsync(TubeArrDbContext db, CancellationToken ct = default)
	{
		var config = await db.YouTubeConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		if (config is null || !config.UseYouTubeApi)
			return null;

		if (string.IsNullOrWhiteSpace(config.ApiKey))
		{
			return new Dictionary<string, object?>
			{
				["type"] = "YouTubeDataApi",
				["status"] = "warn",
				["message"] = "YouTube Data API is enabled but no API key is configured."
			};
		}

		const string probeChannelId = "UC_x5XG1OV2P6uZZ5FSM9Ttw";

		try
		{
			var client = _httpClientFactory.CreateClient("YouTubeDataApi");
			var url =
				$"channels?part=id&id={Uri.EscapeDataString(probeChannelId)}&key={Uri.EscapeDataString(config.ApiKey.Trim())}";
			using var response = await client.GetAsync(url, ct);
			using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
			var root = document.RootElement;

			if (response.IsSuccessStatusCode)
			{
				if (root.TryGetProperty("items", out var items) &&
				    items.ValueKind == JsonValueKind.Array &&
				    items.GetArrayLength() > 0)
				{
					return new Dictionary<string, object?>
					{
						["type"] = "YouTubeDataApi",
						["status"] = "ok",
						["message"] = "YouTube Data API responded successfully."
					};
				}

				return new Dictionary<string, object?>
				{
					["type"] = "YouTubeDataApi",
					["status"] = "warn",
					["message"] = "YouTube Data API returned an unexpected response."
				};
			}

			var errText = TryReadYouTubeApiErrorMessage(root) ?? $"HTTP {(int)response.StatusCode}";
			return new Dictionary<string, object?>
			{
				["type"] = "YouTubeDataApi",
				["status"] = "error",
				["message"] = errText
			};
		}
		catch (Exception ex)
		{
			_logger.LogDebug(ex, "YouTube Data API health check failed.");
			return new Dictionary<string, object?>
			{
				["type"] = "YouTubeDataApi",
				["status"] = "error",
				["message"] = ex.Message
			};
		}
	}

	static string? TryReadYouTubeApiErrorMessage(JsonElement root)
	{
		if (!root.TryGetProperty("error", out var err))
			return null;
		if (err.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
			return msg.GetString();
		return null;
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

	/// <summary>
	/// Lists all video ids in a playlist via <c>playlistItems.list</c> (paginated). Returns empty when the API is disabled, the request fails, or the playlist is empty.
	/// </summary>
	public async Task<HashSet<string>> TryGetPlaylistItemVideoIdsAsync(
		TubeArrDbContext db,
		string youtubePlaylistId,
		CancellationToken ct = default)
	{
		var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (string.IsNullOrWhiteSpace(youtubePlaylistId))
			return ids;

		var config = await db.YouTubeConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		if (config is null || !config.UseYouTubeApi || string.IsNullOrWhiteSpace(config.ApiKey))
			return ids;

		try
		{
			var client = _httpClientFactory.CreateClient("YouTubeDataApi");
			var pid = youtubePlaylistId.Trim();
			string? pageToken = null;
			string? previousPageToken = null;
			do
			{
				var url = $"playlistItems?part=snippet,contentDetails&playlistId={Uri.EscapeDataString(pid)}&maxResults=50&key={Uri.EscapeDataString(config.ApiKey)}";
				if (!string.IsNullOrWhiteSpace(pageToken))
					url += $"&pageToken={Uri.EscapeDataString(pageToken)}";

				using var response = await client.GetAsync(url, ct);
				if (!response.IsSuccessStatusCode)
				{
					_logger.LogDebug(
						"YouTube Data API playlistItems failed status={StatusCode} playlistId={PlaylistId}",
						(int)response.StatusCode,
						pid);
					break;
				}

				using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
				CollectPlaylistItemVideoIds(document.RootElement, ids);
				previousPageToken = pageToken;
				pageToken = GetString(document.RootElement, "nextPageToken");
			}
			while (!string.IsNullOrWhiteSpace(pageToken) && !string.Equals(pageToken, previousPageToken, StringComparison.Ordinal));

			return ids;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "YouTube Data API playlistItems failed for playlistId={PlaylistId}", youtubePlaylistId);
			return ids;
		}
	}

	static void CollectPlaylistItemVideoIds(JsonElement root, HashSet<string> ids)
	{
		if (root.ValueKind != JsonValueKind.Object)
			return;

		if (!root.TryGetProperty("items", out var apiItems) || apiItems.ValueKind != JsonValueKind.Array)
			return;

		foreach (var entry in apiItems.EnumerateArray())
		{
			if (entry.ValueKind != JsonValueKind.Object)
				continue;

			var snippet = GetObject(entry, "snippet");
			var title = GetString(snippet, "title");
			if (string.Equals(title, "Deleted video", StringComparison.OrdinalIgnoreCase) ||
			    string.Equals(title, "Private video", StringComparison.OrdinalIgnoreCase))
				continue;

			var contentDetails = GetObject(entry, "contentDetails");
			var videoId = GetString(contentDetails, "videoId");
			if (!string.IsNullOrWhiteSpace(videoId))
				ids.Add(videoId.Trim());
		}
	}

	/// <summary>Lists playlists owned by the channel via <c>playlists.list</c> (1 quota unit per request page).</summary>
	public async Task<IReadOnlyList<ChannelPlaylistDiscoveryItem>> TryDiscoverChannelPlaylistsAsync(
		TubeArrDbContext db,
		string youtubeChannelId,
		CancellationToken ct = default)
	{
		if (!ChannelResolveHelper.LooksLikeYouTubeChannelId(youtubeChannelId))
			return Array.Empty<ChannelPlaylistDiscoveryItem>();

		var config = await db.YouTubeConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		if (config is null || !config.UseYouTubeApi || string.IsNullOrWhiteSpace(config.ApiKey))
			return Array.Empty<ChannelPlaylistDiscoveryItem>();

		var list = new List<ChannelPlaylistDiscoveryItem>();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			var client = _httpClientFactory.CreateClient("YouTubeDataApi");
			string? pageToken = null;
			string? previousPageToken = null;
			do
			{
				var url =
					$"playlists?part=snippet,contentDetails&channelId={Uri.EscapeDataString(youtubeChannelId)}&maxResults=50&key={Uri.EscapeDataString(config.ApiKey)}";
				if (!string.IsNullOrWhiteSpace(pageToken))
					url += $"&pageToken={Uri.EscapeDataString(pageToken)}";

				using var response = await client.GetAsync(url, ct);
				if (!response.IsSuccessStatusCode)
				{
					_logger.LogDebug(
						"YouTube Data API playlists.list failed status={StatusCode} channelId={ChannelId}",
						(int)response.StatusCode,
						youtubeChannelId);
					break;
				}

				using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
				var root = document.RootElement;
				if (root.TryGetProperty("items", out var itemsEl) && itemsEl.ValueKind == JsonValueKind.Array)
				{
					foreach (var item in itemsEl.EnumerateArray())
					{
						var id = GetString(item, "id");
						if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
							continue;

						var snippet = GetObject(item, "snippet");
						var title = GetString(snippet, "title");
						var description = GetString(snippet, "description");
						var thumb = GetBestThumbnail(snippet);
						list.Add(new ChannelPlaylistDiscoveryItem(id, title, thumb, description));
					}
				}

				previousPageToken = pageToken;
				pageToken = GetString(root, "nextPageToken");
			}
			while (!string.IsNullOrWhiteSpace(pageToken) && !string.Equals(pageToken, previousPageToken, StringComparison.Ordinal));

			return list;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "YouTube Data API playlists.list failed for {ChannelId}", youtubeChannelId);
			return Array.Empty<ChannelPlaylistDiscoveryItem>();
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
				var url = $"videos?part={VideosListParts}&id={joinedIds}&key={Uri.EscapeDataString(config.ApiKey)}";

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
			var url = $"videos?part={VideosListParts}&id={Uri.EscapeDataString(youtubeVideoId)}&key={Uri.EscapeDataString(config.ApiKey)}";
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
		var liveStreamingDetails = GetObject(item, "liveStreamingDetails");

		var title = GetString(snippet, "title");
		var description = GetString(snippet, "description");
		var thumbnail = GetBestThumbnail(snippet);
		var liveBroadcastContent = GetString(snippet, "liveBroadcastContent");
		var snippetIndicatesLiveOrUpcoming =
			liveBroadcastContent is not null &&
			(string.Equals(liveBroadcastContent, "live", StringComparison.OrdinalIgnoreCase) ||
			 string.Equals(liveBroadcastContent, "upcoming", StringComparison.OrdinalIgnoreCase));
		var publishedAt = ParseDateTimeOffset(GetString(snippet, "publishedAt"));
		// videos.list contentDetails.duration — ISO 8601 duration string (e.g. PT5M13S), not seconds.
		var runtime = ParseIso8601DurationSeconds(GetString(contentDetails, "duration"));
		var airDate = publishedAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		var resourceJson = TrySerializeYouTubeVideoListItemParts(item);
		var (thumbW, thumbH) = GetLargestThumbnailDimensions(snippet);

		metadata = new VideoWatchPageMetadata(
			YoutubeVideoId: youtubeVideoId,
			Title: string.IsNullOrWhiteSpace(title) ? null : title,
			Description: string.IsNullOrWhiteSpace(description) ? null : description,
			ThumbnailUrl: string.IsNullOrWhiteSpace(thumbnail) ? null : thumbnail,
			UploadDateUtc: publishedAt,
			AirDateUtc: publishedAt,
			AirDate: string.IsNullOrWhiteSpace(airDate) ? null : airDate,
			Overview: string.IsNullOrWhiteSpace(description) ? null : description,
			Runtime: runtime,
			IsShort: null,
			IsLivestream: snippetIndicatesLiveOrUpcoming || HasLiveStreamingDetailsSignal(liveStreamingDetails)
				? true
				: null,
			Width: thumbW,
			Height: thumbH,
			YouTubeDataApiVideoResourceJson: resourceJson);
		return true;
	}

	/// <summary>Persists the <c>videos.list</c> item fragments we request so future fields stay available without schema churn.</summary>
	static string? TrySerializeYouTubeVideoListItemParts(JsonElement item)
	{
		if (item.ValueKind != JsonValueKind.Object)
			return null;

		using var ms = new MemoryStream();
		using (var writer = new Utf8JsonWriter(ms))
		{
			writer.WriteStartObject();
			foreach (var partName in VideosListPersistedPartNames)
			{
				if (!item.TryGetProperty(partName, out var part) || part.ValueKind != JsonValueKind.Object)
					continue;
				writer.WritePropertyName(partName);
				part.WriteTo(writer);
			}

			writer.WriteEndObject();
		}

		var json = Encoding.UTF8.GetString(ms.ToArray());
		return json == "{}" ? null : json;
	}

	static bool HasLiveStreamingDetailsSignal(JsonElement liveStreamingDetails)
	{
		if (liveStreamingDetails.ValueKind != JsonValueKind.Object)
			return false;

		if (GetString(liveStreamingDetails, "actualStartTime") is not null) return true;
		if (GetString(liveStreamingDetails, "actualEndTime") is not null) return true;
		if (GetString(liveStreamingDetails, "scheduledStartTime") is not null) return true;
		if (GetString(liveStreamingDetails, "scheduledEndTime") is not null) return true;
		if (liveStreamingDetails.TryGetProperty("concurrentViewers", out var concurrentViewers) &&
			concurrentViewers.ValueKind == JsonValueKind.String &&
			!string.IsNullOrWhiteSpace(concurrentViewers.GetString()))
			return true;

		// Presence alone is often enough to indicate stream-origin content.
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

	/// <summary>Uses the same thumbnail preference as <see cref="GetBestThumbnail"/>; returns pixel size when the API provides it.</summary>
	static (int? Width, int? Height) GetLargestThumbnailDimensions(JsonElement snippet)
	{
		if (snippet.ValueKind != JsonValueKind.Object ||
		    !snippet.TryGetProperty("thumbnails", out var thumbnails) ||
		    thumbnails.ValueKind != JsonValueKind.Object)
			return (null, null);

		var order = new[] { "maxres", "standard", "high", "medium", "default" };
		foreach (var key in order)
		{
			if (!thumbnails.TryGetProperty(key, out var thumb) || thumb.ValueKind != JsonValueKind.Object)
				continue;
			var url = GetString(thumb, "url");
			if (string.IsNullOrWhiteSpace(url))
				continue;
			int? w = null;
			int? h = null;
			if (thumb.TryGetProperty("width", out var widthEl) && widthEl.ValueKind == JsonValueKind.Number &&
			    widthEl.TryGetInt32(out var wi) && wi > 0)
				w = wi;
			if (thumb.TryGetProperty("height", out var heightEl) && heightEl.ValueKind == JsonValueKind.Number &&
			    heightEl.TryGetInt32(out var hi) && hi > 0)
				h = hi;
			if (w is > 0 && h is > 0)
				return (w, h);
		}

		return (null, null);
	}

	static DateTimeOffset? ParseDateTimeOffset(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
			? parsed
			: null;
	}

	/// <summary>
	/// Parses YouTube Data API <c>contentDetails.duration</c> (ISO 8601 / XSD duration, e.g. <c>PT5M13S</c>) to whole seconds for video runtime.
	/// </summary>
	internal static int? ParseIso8601DurationSeconds(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		var trimmed = value.Trim();
		try
		{
			var ts = XmlConvert.ToTimeSpan(trimmed);
			if (ts < TimeSpan.Zero)
				return null;
			var total = ts.TotalSeconds;
			if (total > int.MaxValue)
				return int.MaxValue;
			return (int)total;
		}
		catch (FormatException)
		{
			return TryParseYouTubePtDuration(trimmed);
		}
		catch (OverflowException)
		{
			return TryParseYouTubePtDuration(trimmed);
		}
	}

	/// <summary>Subset of ISO 8601 durations returned by the Data API when <see cref="XmlConvert.ToTimeSpan"/> rejects the string.</summary>
	static int? TryParseYouTubePtDuration(string value)
	{
		var m = YoutubeContentDetailsDurationRegex().Match(value);
		if (!m.Success)
			return null;

		double seconds = 0;
		if (m.Groups["h"].Success && int.TryParse(m.Groups["h"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var h))
			seconds += h * 3600d;
		if (m.Groups["m"].Success && int.TryParse(m.Groups["m"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var min))
			seconds += min * 60d;
		if (m.Groups["s"].Success &&
		    double.TryParse(m.Groups["s"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var s))
			seconds += s;

		if (seconds <= 0 && !m.Groups["h"].Success && !m.Groups["m"].Success && !m.Groups["s"].Success)
			return null;
		if (seconds < 0)
			return null;
		if (seconds > int.MaxValue)
			return int.MaxValue;
		return (int)seconds;
	}

	[GeneratedRegex(@"^PT(?:(?<h>\d+)H)?(?:(?<m>\d+)M)?(?:(?<s>\d+(?:\.\d+)?)S)?$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
	private static partial Regex YoutubeContentDetailsDurationRegex();
}
