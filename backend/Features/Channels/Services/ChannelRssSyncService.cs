using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

/// <summary>
/// Fetches YouTube channel upload RSS (Atom) and upserts videos for monitored channels.
/// Feed URL: https://www.youtube.com/feeds/videos.xml?channel_id=UC...
/// <para><b>Acquisition order (this service):</b> (1) channel RSS/Atom feed, (2) yt-dlp on uploads playlist and channel /videos with
/// <see cref="RssFailureFallbackPlaylistEnd"/>, (3) YouTube Data API when earlier steps fail or return nothing usable.</para>
/// </summary>
public sealed class ChannelRssSyncService
{
	public const int RssFailureFallbackPlaylistEnd = 30;
	/// <summary>How many channels RSS/HTML/API/yt-dlp work can overlap during a monitored sync.</summary>
	public const int RssSyncMaxParallelChannels = 4;

	readonly IHttpClientFactory _httpClientFactory;
	readonly YouTubeDataApiMetadataService _youTubeDataApi;
	readonly ChannelMetadataAcquisitionService _metadataAcquisition;
	readonly IServiceScopeFactory _scopeFactory;
	readonly ILogger<ChannelRssSyncService> _logger;

	public ChannelRssSyncService(
		IHttpClientFactory httpClientFactory,
		YouTubeDataApiMetadataService youTubeDataApi,
		ChannelMetadataAcquisitionService metadataAcquisition,
		IServiceScopeFactory scopeFactory,
		ILogger<ChannelRssSyncService> logger)
	{
		_httpClientFactory = httpClientFactory;
		_youTubeDataApi = youTubeDataApi;
		_metadataAcquisition = metadataAcquisition;
		_scopeFactory = scopeFactory;
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
	/// <param name="reportAcquisitionMethodAsync">When set, reports each distinct acquisition source used for this command (internal RSS/HTML, yt-dlp, YouTube Data API).</param>
	public async Task<string> SyncMonitoredChannelsAsync(
		TubeArrDbContext db,
		int? onlyChannelId,
		MetadataProgressReporter? progressReporter,
		CancellationToken ct,
		Func<string, Task>? reportAcquisitionMethodAsync = null)
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
		var channelsWithRssEntries = 0;
		var channelsWithFallbackDiscovery = 0;
		var client = _httpClientFactory.CreateClient("YouTubePage");
		var aggregateLock = new object();

		await Parallel.ForEachAsync(
			channels,
			new ParallelOptions { MaxDegreeOfParallelism = RssSyncMaxParallelChannels, CancellationToken = ct },
			async (channel, ct) =>
			{
				ct.ThrowIfCancellationRequested();

				if (!ChannelResolveHelper.LooksLikeYouTubeChannelId(channel.YoutubeChannelId))
				{
					var err = $"{channel.Title}: invalid YouTube channel id for RSS.";
					lock (aggregateLock)
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

					return;
				}

				await using var scope = _scopeFactory.CreateAsyncScope();
				var scopedDb = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
				var ch = await scopedDb.Channels.FirstAsync(c => c.Id == channel.Id, ct);
				var syncOutcome = await TrySyncOneChannelRssAsync(
					ch,
					scopedDb,
					client,
					progressReporter,
					channels.Count,
					reportAcquisitionMethodAsync,
					ct);
				lock (aggregateLock)
				{
					insertedTotal += syncOutcome.Inserted;
					if (syncOutcome.CountRssChannel)
						channelsWithRssEntries++;
					if (syncOutcome.CountFallbackChannel)
						channelsWithFallbackDiscovery++;
					if (syncOutcome.ErrorToAggregate is not null)
						errors.Add(syncOutcome.ErrorToAggregate);
				}
			});

		var msg =
			$"RSS sync finished: {insertedTotal} new video(s) across {channels.Count} channel(s). " +
			$"RSS feed returned videos for {channelsWithRssEntries} channel(s); " +
			$"yt-dlp/API fallback used for {channelsWithFallbackDiscovery} channel(s).";
		if (errors.Count > 0)
			msg += " " + string.Join(" ", errors);

		return msg;
	}

	readonly record struct RssChannelSyncPartial(int Inserted, bool CountRssChannel, bool CountFallbackChannel, string? ErrorToAggregate);

	async Task<RssChannelSyncPartial> TrySyncOneChannelRssAsync(
		ChannelEntity channel,
		TubeArrDbContext db,
		HttpClient client,
		MetadataProgressReporter? progressReporter,
		int channelCount,
		Func<string, Task>? reportAcquisitionMethodAsync,
		CancellationToken ct)
	{
		try
		{
			List<ChannelVideoDiscoveryItem>? rssItems = null;
			string? rssHttpError = null;

			var url = GetChannelRssUrl(channel.YoutubeChannelId);
			using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
			{
				if (response.IsSuccessStatusCode)
				{
					await using var stream = await response.Content.ReadAsStreamAsync(ct);
					var doc = await XDocument.LoadAsync(stream, LoadOptions.None, ct);
					rssItems = ParseFeedEntries(doc);
				}
				else
				{
					var statusCode = (int)response.StatusCode;
					rssHttpError = $"{channel.Title}: RSS HTTP {statusCode}.";
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
								rssItems = ParseFeedEntries(retryDoc);
								rssHttpError = null;
							}
						}
						else
						{
							_logger.LogDebug(
								"RSS feed fallback extraction returned empty/same URL for channelId={ChannelId}; baseUrl={BaseUrl} httpStatus={StatusCode}",
								channel.YoutubeChannelId, url, statusCode);
						}
					}
				}
			}

			if (rssItems is { Count: > 0 })
			{
				var (_, inserted) = await _metadataAcquisition.UpsertDiscoveredVideosAsync(db, channel, rssItems, ct);
				await RoundRobinMonitoringHelper.ApplyForChannelAsync(db, channel.Id, ct);
				if (progressReporter is not null)
				{
					await progressReporter.IncrementStageAsync(
						"rssFeedSync",
						"RSS feed sync",
						channelCount,
						detail: $"{channel.Title}: {rssItems.Count} feed entries, {inserted} new video(s).",
						ct);
				}

				if (reportAcquisitionMethodAsync is not null)
					await reportAcquisitionMethodAsync(AcquisitionMethodIds.Internal);

				return new RssChannelSyncPartial(inserted, true, false, null);
			}

			_logger.LogInformation(
				"RSS sync: no usable RSS entries for channel {ChannelId} ({Title}); trying yt-dlp (playlist-end {End}) then YouTube API.",
				channel.YoutubeChannelId,
				channel.Title,
				RssFailureFallbackPlaylistEnd);

			var fallbackItems = await TryDiscoverAfterRssFailureAsync(db, channel.YoutubeChannelId, reportAcquisitionMethodAsync, ct);
			if (fallbackItems.Count > 0)
			{
				var (_, fbInserted) = await _metadataAcquisition.UpsertDiscoveredVideosAsync(db, channel, fallbackItems, ct);
				await RoundRobinMonitoringHelper.ApplyForChannelAsync(db, channel.Id, ct);
				if (progressReporter is not null)
				{
					await progressReporter.IncrementStageAsync(
						"rssFeedSync",
						"RSS feed sync",
						channelCount,
						detail: $"{channel.Title}: RSS failed; {fallbackItems.Count} video(s) via yt-dlp or API, {fbInserted} new.",
						ct);
				}

				return new RssChannelSyncPartial(fbInserted, false, true, null);
			}

			if (!string.IsNullOrWhiteSpace(rssHttpError))
			{
				if (progressReporter is not null)
				{
					await progressReporter.AddStageErrorAsync("rssFeedSync", "RSS feed sync", rssHttpError, ct);
					await progressReporter.IncrementStageAsync(
						"rssFeedSync",
						"RSS feed sync",
						channelCount,
						detail: $"{channel.Title}: RSS HTTP error; fallback found no videos.",
						ct);
				}

				return new RssChannelSyncPartial(0, false, false, rssHttpError);
			}

			_logger.LogWarning("RSS sync: no entries and no fallback videos for channel {ChannelId} {Title}", channel.Id, channel.Title);
			if (progressReporter is not null)
			{
				await progressReporter.IncrementStageAsync(
					"rssFeedSync",
					"RSS feed sync",
					channelCount,
					detail: $"{channel.Title}: feed returned no entries; fallback found no videos.",
					ct);
			}

			return new RssChannelSyncPartial(0, false, false, null);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "RSS sync failed for channel {ChannelId}", channel.Id);
			try
			{
				var recovered = await TryDiscoverAfterRssFailureAsync(db, channel.YoutubeChannelId, reportAcquisitionMethodAsync, ct);
				if (recovered.Count > 0)
				{
					var (_, ins) = await _metadataAcquisition.UpsertDiscoveredVideosAsync(db, channel, recovered, ct);
					await RoundRobinMonitoringHelper.ApplyForChannelAsync(db, channel.Id, ct);
					if (progressReporter is not null)
					{
						await progressReporter.IncrementStageAsync(
							"rssFeedSync",
							"RSS feed sync",
							channelCount,
							detail: $"{channel.Title}: RSS error; recovered {recovered.Count} video(s) via fallback, {ins} new.",
							ct);
					}

					return new RssChannelSyncPartial(ins, false, true, null);
				}
			}
			catch (Exception ex2)
			{
				_logger.LogDebug(ex2, "RSS failure fallback also failed for channel {ChannelId}", channel.Id);
			}

			var err = $"{channel.Title}: {ex.Message}";
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
					channelCount,
					detail: $"Failed: {channel.Title}.",
					ct);
			}

			return new RssChannelSyncPartial(0, false, false, err);
		}
	}

	async Task<IReadOnlyList<ChannelVideoDiscoveryItem>> TryDiscoverAfterRssFailureAsync(
		TubeArrDbContext db,
		string youtubeChannelId,
		Func<string, Task>? reportAcquisitionMethodAsync,
		CancellationToken ct)
	{
		var ytDlpItems = await TryDiscoverViaYtDlpRssFallbackAsync(db, youtubeChannelId, ct);
		if (ytDlpItems.Count > 0)
		{
			if (reportAcquisitionMethodAsync is not null)
				await reportAcquisitionMethodAsync(AcquisitionMethodIds.YtDlp);

			return ytDlpItems;
		}

		var apiResult = await _youTubeDataApi.TryDiscoverChannelVideosAsync(db, youtubeChannelId, ct);
		if (apiResult.Items.Count == 0)
			return Array.Empty<ChannelVideoDiscoveryItem>();

		if (reportAcquisitionMethodAsync is not null)
			await reportAcquisitionMethodAsync(AcquisitionMethodIds.YouTubeDataApi);

		return apiResult.Items.Take(RssFailureFallbackPlaylistEnd).ToList();
	}

	async Task<IReadOnlyList<ChannelVideoDiscoveryItem>> TryDiscoverViaYtDlpRssFallbackAsync(
		TubeArrDbContext db,
		string youtubeChannelId,
		CancellationToken ct)
	{
		var executablePath = await YtDlpMetadataService.GetExecutablePathAsync(db, ct);
		if (string.IsNullOrWhiteSpace(executablePath))
			return Array.Empty<ChannelVideoDiscoveryItem>();

		var cookiesPath = await YtDlpMetadataService.GetCookiesPathAsync(db, ct);

		var uploadsUrl = ChannelResolveHelper.GetChannelUploadsPlaylistUrl(youtubeChannelId);
		if (!string.IsNullOrWhiteSpace(uploadsUrl))
		{
			var docs = await YtDlpMetadataService.RunYtDlpJsonAsync(
				executablePath,
				uploadsUrl,
				ct,
				playlistItems: null,
				timeoutMs: 120_000,
				flatPlaylist: true,
				playlistEnd: RssFailureFallbackPlaylistEnd,
				cookiesPath: cookiesPath);
			try
			{
				var items = ChannelMetadataAcquisitionService.FlattenYtDlpDiscoveryDocuments(docs);
				if (items.Count > 0)
					return items;
			}
			finally
			{
				foreach (var d in docs)
					d.Dispose();
			}
		}

		var channelVideosUrl = ChannelResolveHelper.GetCanonicalChannelVideosUrl(youtubeChannelId);
		var docsChannel = await YtDlpMetadataService.RunYtDlpJsonAsync(
			executablePath,
			channelVideosUrl,
			ct,
			playlistItems: null,
			timeoutMs: 120_000,
			flatPlaylist: true,
			playlistEnd: RssFailureFallbackPlaylistEnd,
			cookiesPath: cookiesPath);
		try
		{
			return ChannelMetadataAcquisitionService.FlattenYtDlpDiscoveryDocuments(docsChannel);
		}
		finally
		{
			foreach (var d in docsChannel)
				d.Dispose();
		}
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
