using Microsoft.Extensions.Logging;

namespace TubeArr.Backend;

public sealed record ChannelPageMetadata(
	string YoutubeChannelId,
	string? Title,
	string? Description,
	string? ThumbnailUrl,
	string? BannerUrl,
	string CanonicalUrl)
{
	public string TitleSlug => SlugHelper.Slugify(string.IsNullOrWhiteSpace(Title) ? YoutubeChannelId : Title);
}

public sealed class ChannelPageMetadataService
{
	readonly IHttpClientFactory _httpClientFactory;
	readonly ILogger<ChannelPageMetadataService> _logger;

	public ChannelPageMetadataService(IHttpClientFactory httpClientFactory, ILogger<ChannelPageMetadataService> logger)
	{
		_httpClientFactory = httpClientFactory;
		_logger = logger;
	}

	public async Task<ChannelPageMetadata?> GetMetadataByYoutubeChannelIdAsync(string youtubeChannelId, CancellationToken ct = default)
	{
		if (!ChannelResolveHelper.LooksLikeYouTubeChannelId(youtubeChannelId))
			return null;

		var url = $"https://www.youtube.com/channel/{youtubeChannelId}";
		return await GetMetadataFromUrlAsync(url, youtubeChannelId, ct);
	}

	public async Task<ChannelPageMetadata?> GetMetadataFromUrlAsync(string url, string? fallbackYoutubeChannelId = null, CancellationToken ct = default)
	{
		var client = _httpClientFactory.CreateClient("YouTubePage");
		using var response = await client.GetAsync(url, ct);
		if (!response.IsSuccessStatusCode)
		{
			_logger.LogDebug("Channel page request failed status={StatusCode} url={Url}", (int)response.StatusCode, url);
			return null;
		}

		var html = await response.Content.ReadAsStringAsync(ct);
		return ParseFromHtml(html, fallbackYoutubeChannelId);
	}

	public static ChannelPageMetadata? ParseFromHtml(string html, string? fallbackYoutubeChannelId = null)
	{
		var youtubeChannelId = ChannelResolveHelper.ExtractChannelIdFromHtml(html);
		if (string.IsNullOrWhiteSpace(youtubeChannelId) && ChannelResolveHelper.LooksLikeYouTubeChannelId(fallbackYoutubeChannelId ?? string.Empty))
			youtubeChannelId = fallbackYoutubeChannelId!.Trim();

		if (!ChannelResolveHelper.LooksLikeYouTubeChannelId(youtubeChannelId ?? string.Empty))
			return null;

		var title = ChannelResolveHelper.ExtractChannelTitleFromHtml(html)?.Trim();
		var description = ChannelResolveHelper.ExtractChannelDescriptionFromHtml(html);
		var thumbnailUrl = ChannelResolveHelper.ExtractChannelLogoFromHtml(html)?.Trim();
		var bannerUrl = ChannelResolveHelper.ExtractChannelBannerFromHtml(html)?.Trim();
		var canonicalUrl = $"https://www.youtube.com/channel/{youtubeChannelId}";

		return new ChannelPageMetadata(
			YoutubeChannelId: youtubeChannelId!,
			Title: string.IsNullOrWhiteSpace(title) ? null : title,
			Description: string.IsNullOrWhiteSpace(description) ? null : description,
			ThumbnailUrl: string.IsNullOrWhiteSpace(thumbnailUrl) ? null : thumbnailUrl,
			BannerUrl: string.IsNullOrWhiteSpace(bannerUrl) ? null : bannerUrl,
			CanonicalUrl: canonicalUrl);
	}
}
