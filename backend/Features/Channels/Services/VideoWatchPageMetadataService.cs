using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace TubeArr.Backend;

public sealed record VideoWatchPageMetadata(
	string YoutubeVideoId,
	string? Title,
	string? Description,
	string? ThumbnailUrl,
	DateTimeOffset? UploadDateUtc,
	DateTimeOffset? AirDateUtc,
	string? AirDate,
	string? Overview,
	int? Runtime,
	bool? IsShort = null,
	bool? IsLivestream = null);

public sealed class VideoWatchPageMetadataService
{
	readonly IHttpClientFactory _httpClientFactory;
	readonly ILogger<VideoWatchPageMetadataService> _logger;

	public VideoWatchPageMetadataService(IHttpClientFactory httpClientFactory, ILogger<VideoWatchPageMetadataService> logger)
	{
		_httpClientFactory = httpClientFactory;
		_logger = logger;
	}

	public async Task<VideoWatchPageMetadata?> GetMetadataAsync(string youtubeVideoId, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(youtubeVideoId))
			return null;

		var client = _httpClientFactory.CreateClient("YouTubePage");
		using var response = await client.GetAsync($"https://www.youtube.com/watch?v={Uri.EscapeDataString(youtubeVideoId)}", ct);
		if (!response.IsSuccessStatusCode)
		{
			_logger.LogDebug("Watch page request failed status={StatusCode} videoId={VideoId}", (int)response.StatusCode, youtubeVideoId);
			return null;
		}

		var html = await response.Content.ReadAsStringAsync(ct);
		return ParseFromHtml(youtubeVideoId, html);
	}

	public static VideoWatchPageMetadata? ParseFromHtml(string youtubeVideoId, string html)
	{
		if (string.IsNullOrWhiteSpace(youtubeVideoId) || string.IsNullOrWhiteSpace(html))
			return null;

		string? title = null;
		string? description = null;
		string? thumbnailUrl = null;
		DateTimeOffset? uploadDateUtc = null;
		int? runtime = null;
		bool? isShort = null;
		bool? isLivestream = null;

		using var playerResponse = YouTubePageJsonHelper.TryExtractJsonDocument(
			html,
			"var ytInitialPlayerResponse = ",
			"window[\"ytInitialPlayerResponse\"] = ",
			"ytInitialPlayerResponse = ");

		if (playerResponse is not null &&
			playerResponse.RootElement.ValueKind == JsonValueKind.Object)
		{
			if (playerResponse.RootElement.TryGetProperty("videoDetails", out var videoDetails) &&
				videoDetails.ValueKind == JsonValueKind.Object)
			{
				title = GetString(videoDetails, "title");
				description = GetString(videoDetails, "shortDescription");
				runtime = ParseRuntimeSeconds(GetString(videoDetails, "lengthSeconds"));
				thumbnailUrl = GetBestThumbnail(videoDetails, "thumbnail");
				isShort ??= TryGetJsonBoolean(videoDetails, "isShortFormContent")
					?? TryGetJsonBoolean(videoDetails, "isShort");
				isLivestream ??= TryGetJsonBoolean(videoDetails, "isLiveContent")
					?? TryGetJsonBoolean(videoDetails, "isLive")
					?? TryGetJsonBoolean(videoDetails, "isUpcoming");
			}

			if (playerResponse.RootElement.TryGetProperty("microformat", out var microformat) &&
				microformat.ValueKind == JsonValueKind.Object &&
				microformat.TryGetProperty("playerMicroformatRenderer", out var playerMicroformatRenderer) &&
				playerMicroformatRenderer.ValueKind == JsonValueKind.Object)
			{
				title ??= GetText(playerMicroformatRenderer, "title");
				description ??= GetString(playerMicroformatRenderer, "description");
				thumbnailUrl ??= GetBestThumbnail(playerMicroformatRenderer, "thumbnail");

				var uploadDateValue = GetString(playerMicroformatRenderer, "uploadDate")
					?? GetString(playerMicroformatRenderer, "publishDate");
				uploadDateUtc ??= ParseDateUtc(uploadDateValue);
				if (playerMicroformatRenderer.TryGetProperty("liveBroadcastDetails", out var liveBroadcastDetails) &&
				    liveBroadcastDetails.ValueKind == JsonValueKind.Object)
					isLivestream ??= true;
			}
		}

		title ??= TryExtractMetaContent(html, "og:title");
		description ??= TryExtractMetaContent(html, "og:description") ?? TryExtractMetaNameContent(html, "description");
		thumbnailUrl ??= TryExtractMetaContent(html, "og:image");
		uploadDateUtc ??= ParseDateUtc(
			TryExtractMetaItemPropContent(html, "uploadDate")
			?? TryExtractMetaItemPropContent(html, "datePublished"));

		var airDateUtc = uploadDateUtc;
		var airDate = airDateUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		var overview = string.IsNullOrWhiteSpace(description) ? null : description;

		return new VideoWatchPageMetadata(
			YoutubeVideoId: youtubeVideoId,
			Title: string.IsNullOrWhiteSpace(title) ? null : title,
			Description: string.IsNullOrWhiteSpace(description) ? null : description,
			ThumbnailUrl: string.IsNullOrWhiteSpace(thumbnailUrl) ? null : thumbnailUrl,
			UploadDateUtc: uploadDateUtc,
			AirDateUtc: airDateUtc,
			AirDate: string.IsNullOrWhiteSpace(airDate) ? null : airDate,
			Overview: overview,
			Runtime: runtime,
			IsShort: isShort,
			IsLivestream: isLivestream);
	}

	static bool? TryGetJsonBoolean(JsonElement element, string propertyName)
	{
		if (!element.TryGetProperty(propertyName, out var property))
			return null;
		return property.ValueKind switch
		{
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			_ => null
		};
	}

	static string? GetText(JsonElement element, string propertyName)
	{
		if (!element.TryGetProperty(propertyName, out var property))
			return null;

		if (property.ValueKind == JsonValueKind.String)
			return property.GetString()?.Trim();

		if (property.ValueKind == JsonValueKind.Object)
		{
			if (property.TryGetProperty("simpleText", out var simpleText) && simpleText.ValueKind == JsonValueKind.String)
				return simpleText.GetString()?.Trim();

			if (property.TryGetProperty("runs", out var runs) && runs.ValueKind == JsonValueKind.Array)
			{
				return string.Concat(
					runs.EnumerateArray()
						.Select(run => GetString(run, "text"))
						.Where(text => !string.IsNullOrWhiteSpace(text)));
			}
		}

		return null;
	}

	static string? GetBestThumbnail(JsonElement element, string propertyName)
	{
		if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
			return null;

		if (!property.TryGetProperty("thumbnails", out var thumbnails) || thumbnails.ValueKind != JsonValueKind.Array)
			return null;

		string? value = null;
		foreach (var thumbnail in thumbnails.EnumerateArray())
		{
			var candidate = GetString(thumbnail, "url");
			if (!string.IsNullOrWhiteSpace(candidate))
				value = candidate;
		}

		return value;
	}

	static DateTimeOffset? ParseDateUtc(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		if (DateTimeOffset.TryParseExact(
			value.Trim(),
			"yyyy-MM-dd",
			CultureInfo.InvariantCulture,
			DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
			out var date))
		{
			return date;
		}

		return null;
	}

	static int? ParseRuntimeSeconds(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
			? parsed
			: null;
	}

	static string? TryExtractMetaContent(string html, string propertyName)
	{
		var match = Regex.Match(
			html,
			$@"<meta[^>]+property=""{Regex.Escape(propertyName)}""[^>]+content=""([^""]*)""",
			RegexOptions.IgnoreCase);
		return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value.Trim()) : null;
	}

	static string? TryExtractMetaNameContent(string html, string metaName)
	{
		var match = Regex.Match(
			html,
			$@"<meta[^>]+name=""{Regex.Escape(metaName)}""[^>]+content=""([^""]*)""",
			RegexOptions.IgnoreCase);
		return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value.Trim()) : null;
	}

	static string? TryExtractMetaItemPropContent(string html, string itemProp)
	{
		var match = Regex.Match(
			html,
			$@"itemprop=""{Regex.Escape(itemProp)}""[^>]+content=""([^""]*)""",
			RegexOptions.IgnoreCase);
		return match.Success ? match.Groups[1].Value.Trim() : null;
	}

	static string? GetString(JsonElement element, string propertyName)
	{
		if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
			return null;

		return property.GetString()?.Trim();
	}
}
