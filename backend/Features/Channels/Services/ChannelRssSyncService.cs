using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

/// <summary>
/// Fetches YouTube channel upload RSS (Atom) and upserts videos for monitored channels.
/// Feed URL: https://www.youtube.com/feeds/videos.xml?channel_id=UC...
/// </summary>
public sealed class ChannelRssSyncService
{
	readonly IHttpClientFactory _httpClientFactory;
	readonly ILogger<ChannelRssSyncService> _logger;

	public ChannelRssSyncService(IHttpClientFactory httpClientFactory, ILogger<ChannelRssSyncService> logger)
	{
		_httpClientFactory = httpClientFactory;
		_logger = logger;
	}

	public static string GetChannelRssUrl(string youtubeChannelId) =>
		$"https://www.youtube.com/feeds/videos.xml?channel_id={Uri.EscapeDataString(youtubeChannelId)}";

	static async Task<string?> TryExtractFallbackRssUrlAsync(string youtubeChannelId, HttpClient client, CancellationToken ct)
	{
		// When feeds/videos.xml?channel_id=... returns 404/5xx, YouTube sometimes exposes the working RSS URL
		// via the channel page's `channelMetadataRenderer.rssUrl` (usually includes a playlist_id).
		var channelUrl = $"https://www.youtube.com/channel/{Uri.EscapeDataString(youtubeChannelId)}";
		var candidates = new[]
		{
			channelUrl,
			channelUrl + "/videos"
		};

		foreach (var candidateUrl in candidates)
		{
			using var response = await client.GetAsync(candidateUrl, HttpCompletionOption.ResponseHeadersRead, ct);
			if (!response.IsSuccessStatusCode)
				continue;

			var html = await response.Content.ReadAsStringAsync(ct);
			var rssUrl = ChannelResolveHelper.ExtractChannelRssUrlFromHtml(html);
			if (!string.IsNullOrWhiteSpace(rssUrl))
				return rssUrl;
		}

		return null;
	}

	/// <param name="onlyChannelId">When set, syncs that channel only (any monitoring state).</param>
	/// <param name="progressReporter">When set, reports per-channel RSS progress for metadata monitoring UI.</param>
	public async Task<string> SyncMonitoredChannelsAsync(
		TubeArrDbContext db,
		int? onlyChannelId,
		MetadataProgressReporter? progressReporter,
		CancellationToken ct)
	{
		IQueryable<ChannelEntity> query = onlyChannelId is { } cid && cid > 0
			? db.Channels.Where(c => c.Id == cid)
			: db.Channels.Where(c => c.Monitored);

		var channels = await query.OrderBy(c => c.Id).ToListAsync(ct);
		if (channels.Count == 0)
		{
			if (progressReporter is not null)
			{
				var emptyDetail = onlyChannelId.HasValue
					? "Channel not found."
					: "No monitored channels to sync.";
				await progressReporter.SetStageAsync(
					"rssFeedSync",
					"RSS feed sync",
					0,
					0,
					detail: emptyDetail,
					ct);
			}

			return onlyChannelId.HasValue
				? "Channel not found."
				: "No monitored channels to sync.";
		}

		if (progressReporter is not null)
		{
			await progressReporter.SetStageAsync(
				"rssFeedSync",
				"RSS feed sync",
				0,
				channels.Count,
				detail: $"Syncing {channels.Count} channel(s) via RSS.",
				ct);
		}

		var insertedTotal = 0;
		var errors = new List<string>();
		var client = _httpClientFactory.CreateClient("YouTubePage");

		for (var i = 0; i < channels.Count; i++)
		{
			var channel = channels[i];
			ct.ThrowIfCancellationRequested();

			if (progressReporter is not null)
			{
				await progressReporter.SetStageAsync(
					"rssFeedSync",
					"RSS feed sync",
					i,
					channels.Count,
					detail: $"Fetching RSS feed for {channel.Title}â€¦",
					ct);
			}

			if (!ChannelResolveHelper.LooksLikeYouTubeChannelId(channel.YoutubeChannelId))
			{
				var err = $"{channel.Title}: invalid YouTube channel id for RSS.";
				errors.Add(err);
				if (progressReporter is not null)
				{
					await progressReporter.AddStageErrorAsync("rssFeedSync", "RSS feed sync", err, ct);
					await progressReporter.IncrementStageAsync(
						"rssFeedSync",
						"RSS feed sync",
						channels.Count,
						detail: $"Skipped {channel.Title} (invalid YouTube id).",
						ct);
				}

				continue;
			}

			try
			{
				var url = GetChannelRssUrl(channel.YoutubeChannelId);
				using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

				// The `feeds/videos.xml?channel_id=...` endpoint sometimes returns 404/500 for otherwise-valid channels.
				// Fallback: fetch channel HTML and extract `channelMetadataRenderer.rssUrl`, then retry with that URL.
				if (!response.IsSuccessStatusCode)
				{
					var statusCode = (int)response.StatusCode;
					if (statusCode == 404 || statusCode >= 500)
					{
						var fallbackUrl = await TryExtractFallbackRssUrlAsync(channel.YoutubeChannelId, client, ct);
						if (!string.IsNullOrWhiteSpace(fallbackUrl) &&
						    !string.Equals(fallbackUrl, url, StringComparison.OrdinalIgnoreCase))
						{
							_logger.LogInformation("RSS feed 404/5xx fallback for channel {ChannelId}: {FallbackUrl}", channel.YoutubeChannelId, fallbackUrl);

							using var retryResponse = await client.GetAsync(fallbackUrl, HttpCompletionOption.ResponseHeadersRead, ct);
							if (retryResponse.IsSuccessStatusCode)
							{
								await using var retryStream = await retryResponse.Content.ReadAsStreamAsync(ct);
								var retryDoc = await XDocument.LoadAsync(retryStream, LoadOptions.None, ct);
								var retryItems = ParseFeedEntries(retryDoc);
								if (retryItems.Count == 0)
									return $"{channel.Title}: feed returned no entries.";

								var (_, retryInserted) = await ChannelMetadataAcquisitionService.UpsertDiscoveredVideosAsync(db, channel, retryItems, ct);
								insertedTotal += retryInserted;
								await RoundRobinMonitoringHelper.ApplyForChannelAsync(db, channel.Id, ct);
								if (progressReporter is not null)
								{
									await progressReporter.IncrementStageAsync(
										"rssFeedSync",
										"RSS feed sync",
										channels.Count,
										detail: $"{channel.Title}: {retryItems.Count} feed entries, {retryInserted} new video(s).",
										ct);
								}

								continue;
							}
						}
						else
						{
							_logger.LogDebug(
								"RSS feed fallback extraction returned empty/same URL for channelId={ChannelId}; baseUrl={BaseUrl} httpStatus={StatusCode}",
								channel.YoutubeChannelId, url, statusCode);
						}
					}

					var httpErr = $"{channel.Title}: RSS HTTP {(int)response.StatusCode}.";
					errors.Add(httpErr);
					if (progressReporter is not null)
					{
						await progressReporter.AddStageErrorAsync("rssFeedSync", "RSS feed sync", httpErr, ct);
						await progressReporter.IncrementStageAsync(
							"rssFeedSync",
							"RSS feed sync",
							channels.Count,
							detail: $"{channel.Title}: HTTP {(int)response.StatusCode}.",
							ct);
					}

					continue;
				}

				await using var stream = await response.Content.ReadAsStreamAsync(ct);
				var doc = await XDocument.LoadAsync(stream, LoadOptions.None, ct);
				var items = ParseFeedEntries(doc);
				if (items.Count == 0)
				{
					_logger.LogWarning("RSS sync: no entries for channel {ChannelId} {Title}", channel.Id, channel.Title);
					if (progressReporter is not null)
					{
						await progressReporter.IncrementStageAsync(
							"rssFeedSync",
							"RSS feed sync",
							channels.Count,
							detail: $"{channel.Title}: feed returned no entries.",
							ct);
					}

					continue;
				}

				var (_, inserted) = await ChannelMetadataAcquisitionService.UpsertDiscoveredVideosAsync(db, channel, items, ct);
				insertedTotal += inserted;
				await RoundRobinMonitoringHelper.ApplyForChannelAsync(db, channel.Id, ct);
				if (progressReporter is not null)
				{
					await progressReporter.IncrementStageAsync(
						"rssFeedSync",
						"RSS feed sync",
						channels.Count,
						detail: $"{channel.Title}: {items.Count} feed entries, {inserted} new video(s).",
						ct);
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "RSS sync failed for channel {ChannelId}", channel.Id);
				errors.Add($"{channel.Title}: {ex.Message}");
				if (progressReporter is not null)
				{
					await progressReporter.AddStageErrorAsync(
						"rssFeedSync",
						"RSS feed sync",
						$"{channel.Title}: {ex.Message}",
						ct);
					await progressReporter.IncrementStageAsync(
						"rssFeedSync",
						"RSS feed sync",
						channels.Count,
						detail: $"Failed: {channel.Title}.",
						ct);
				}
			}
		}

		var msg = $"RSS sync finished: {insertedTotal} new video(s) across {channels.Count} channel(s).";
		if (errors.Count > 0)
			msg += " " + string.Join(" ", errors);

		return msg;
	}

	static List<ChannelVideoDiscoveryItem> ParseFeedEntries(XDocument doc)
	{
		var list = new List<ChannelVideoDiscoveryItem>();
		var root = doc.Root;
		if (root is null)
			return list;

		foreach (var entry in root.Descendants().Where(e => e.Name.LocalName == "entry"))
		{
			var videoId = TryGetVideoId(entry);
			if (string.IsNullOrWhiteSpace(videoId))
				continue;

			var title = FirstLocalChildText(entry, "title");
			var publishedText = FirstLocalChildText(entry, "published");
			DateTimeOffset? published = null;
			if (!string.IsNullOrWhiteSpace(publishedText) &&
			    DateTimeOffset.TryParse(publishedText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
				published = dto;

			var thumbnailUrl = entry.Descendants()
				.FirstOrDefault(e => e.Name.LocalName == "thumbnail")
				?.Attribute("url")?.Value;

			list.Add(new ChannelVideoDiscoveryItem(
				YoutubeVideoId: videoId.Trim(),
				Title: string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
				ThumbnailUrl: string.IsNullOrWhiteSpace(thumbnailUrl) ? null : thumbnailUrl.Trim(),
				Runtime: null,
				PublishedUtc: published));
		}

		return list;
	}

	static string? TryGetVideoId(XElement entry)
	{
		var idText = FirstLocalChildText(entry, "id");
		if (!string.IsNullOrWhiteSpace(idText) &&
		    idText.StartsWith("yt:video:", StringComparison.OrdinalIgnoreCase))
			return idText["yt:video:".Length..].Trim();

		var vid = entry.Elements().FirstOrDefault(e => e.Name.LocalName == "videoId")?.Value;
		if (!string.IsNullOrWhiteSpace(vid))
			return vid.Trim();

		var link = entry.Elements()
			.FirstOrDefault(e =>
				e.Name.LocalName == "link" &&
				string.Equals((string?)e.Attribute("rel"), "alternate", StringComparison.OrdinalIgnoreCase));
		var href = link?.Attribute("href")?.Value;
		if (string.IsNullOrWhiteSpace(href))
			return null;

		var q = href.IndexOf("v=", StringComparison.OrdinalIgnoreCase);
		if (q < 0)
			return null;
		var start = q + 2;
		var amp = href.IndexOf('&', start);
		var raw = amp < 0 ? href[start..] : href[start..amp];
		return string.IsNullOrWhiteSpace(raw) ? null : Uri.UnescapeDataString(raw.Trim());
	}

	static string? FirstLocalChildText(XElement parent, string localName) =>
		parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;
}
