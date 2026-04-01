using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

/// <summary>Shared channel resolve pipeline for <c>GET /channels/resolve</c> and library import scanning.</summary>
public sealed class ChannelResolveService
{
	static readonly Regex BareYoutubeVideoId = new(@"^[a-zA-Z0-9_-]{11}$", RegexOptions.Compiled);

	readonly ChannelPageMetadataService _channelPageMetadata;
	readonly ChannelSearchHtmlResolveService _channelSearchHtml;
	readonly IYtDlpClient _ytDlp;

	public ChannelResolveService(
		ChannelPageMetadataService channelPageMetadata,
		ChannelSearchHtmlResolveService channelSearchHtml,
		IYtDlpClient ytDlp)
	{
		_channelPageMetadata = channelPageMetadata;
		_channelSearchHtml = channelSearchHtml;
		_ytDlp = ytDlp;
	}

	/// <returns>JSON body and HTTP status code.</returns>
	public async Task<(ChannelResolveResultDto Result, int StatusCode)> ResolveAsync(
		string input,
		TubeArrDbContext db,
		ILogger logger,
		CancellationToken ct,
		int resolveTimeoutMs = 300_000)
	{
		var trimmed = (input ?? "").Trim();
		if (string.IsNullOrWhiteSpace(trimmed))
			return (new ChannelResolveResultDto(Success: false, null, null, null, null, "Empty input", null), StatusCodes.Status200OK);

		var classification = ChannelResolveHelper.ClassifyInput(trimmed);
		logger.LogInformation(
			"Resolve start input={Input} kind={Kind} canonicalUrl={CanonicalUrl} channelId={ChannelId} handle={Handle}",
			trimmed, classification.Kind, classification.CanonicalUrl, classification.ChannelId, classification.Handle);

		if (classification.Kind == ChannelResolveHelper.ChannelInputKind.Empty ||
		    classification.Kind == ChannelResolveHelper.ChannelInputKind.Unknown)
		{
			return (new ChannelResolveResultDto(Success: false, null, null, null, null, "Not a resolvable channel identifier; try search instead.", null), StatusCodes.Status200OK);
		}

		if (classification.Kind == ChannelResolveHelper.ChannelInputKind.SearchTerm)
		{
			var fromMedia = await TryResolveSearchTermAsYoutubeMediaAsync(trimmed, db, logger, ct, resolveTimeoutMs);
			if (fromMedia is not null)
				return fromMedia.Value;

			try
			{
				var searchChannelId = await _channelSearchHtml.ResolveFirstChannelIdAsync(trimmed, ct);
				if (ChannelResolveHelper.LooksLikeYouTubeChannelId(searchChannelId ?? string.Empty))
				{
					var title = searchChannelId;
					ChannelPageMetadata? metadata = null;
					try
					{
						metadata = await _channelPageMetadata.GetMetadataByYoutubeChannelIdAsync(searchChannelId!, ct);
					}
					catch (Exception ex)
					{
						logger.LogDebug(ex, "Resolve search metadata fetch failed channelId={ChannelId}", searchChannelId);
					}

					if (!string.IsNullOrWhiteSpace(metadata?.Title))
						title = metadata.Title!;

					var items = new[]
					{
						new ChannelSearchResultDto(
							YoutubeChannelId: searchChannelId!,
							Title: title ?? searchChannelId!,
							TitleSlug: SlugHelper.Slugify(title ?? searchChannelId!),
							Description: metadata?.Description,
							ThumbnailUrl: metadata?.ThumbnailUrl,
							ChannelUrl: $"https://www.youtube.com/channel/{searchChannelId}",
							Handle: null,
							SubscriberCount: null,
							VideoCount: null)
					};

					return (new ChannelResolveResultDto(
						Success: true,
						ChannelId: searchChannelId,
						CanonicalUrl: $"https://www.youtube.com/channel/{searchChannelId}",
						Title: metadata?.Title ?? title,
						ResolutionMethod: "search-html",
						FailureReason: null,
						Items: items), StatusCodes.Status200OK);
				}
			}
			catch (OperationCanceledException)
			{
				if (!ct.IsCancellationRequested)
					return (new ChannelResolveResultDto(Success: false, null, null, null, null, "Timeout", null), StatusCodes.Status504GatewayTimeout);
				return (new ChannelResolveResultDto(Success: false, null, null, null, null, "Request aborted", null), StatusCodes.Status200OK);
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Resolve search-html failed term={Term}", trimmed);
			}

			var ytDlpSearchPath = await _ytDlp.GetExecutablePathAsync(db, ct);
			if (string.IsNullOrWhiteSpace(ytDlpSearchPath))
			{
				return (new ChannelResolveResultDto(Success: false, null, null, null, null, "yt-dlp is not configured and search-html did not resolve a channel.", null), StatusCodes.Status503ServiceUnavailable);
			}

			var ytDlpSearchCookiesPath = await _ytDlp.GetCookiesPathAsync(db, ct);

			try
			{
				var channels = await _ytDlp.SearchChannelsAsync(ytDlpSearchPath, trimmed, maxResults: 1, ct, ytDlpSearchCookiesPath);
				if (channels.Count > 0)
				{
					var m0 = channels[0];
					var items = new[]
					{
						new ChannelSearchResultDto(
							YoutubeChannelId: m0.YoutubeChannelId,
							Title: m0.Title,
							TitleSlug: m0.TitleSlug,
							Description: m0.Description,
							ThumbnailUrl: m0.ThumbnailUrl,
							ChannelUrl: m0.ChannelUrl,
							Handle: m0.Handle,
							SubscriberCount: m0.SubscriberCount,
							VideoCount: m0.VideoCount)
					};

					return (new ChannelResolveResultDto(
						Success: true,
						ChannelId: m0.YoutubeChannelId,
						CanonicalUrl: m0.ChannelUrl ?? $"https://www.youtube.com/channel/{m0.YoutubeChannelId}",
						Title: m0.Title,
						ResolutionMethod: "search-yt-dlp",
						FailureReason: null,
						Items: items), StatusCodes.Status200OK);
				}
			}
			catch (OperationCanceledException)
			{
				if (!ct.IsCancellationRequested)
					return (new ChannelResolveResultDto(Success: false, null, null, null, null, "Timeout", null), StatusCodes.Status504GatewayTimeout);
				return (new ChannelResolveResultDto(Success: false, null, null, null, null, "Request aborted", null), StatusCodes.Status200OK);
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Resolve search-yt-dlp failed term={Term}", trimmed);
			}

			return (new ChannelResolveResultDto(Success: false, null, null, null, "search-html/yt-dlp", "Channel not found", null), StatusCodes.Status200OK);
		}

		ChannelPageMetadata? directMetadata = null;
		try
		{
			if (ChannelResolveHelper.LooksLikeYouTubeChannelId(classification.ChannelId ?? string.Empty))
				directMetadata = await _channelPageMetadata.GetMetadataByYoutubeChannelIdAsync(classification.ChannelId!, ct);
			else if (!string.IsNullOrWhiteSpace(classification.CanonicalUrl))
				directMetadata = await _channelPageMetadata.GetMetadataFromUrlAsync(classification.CanonicalUrl!, null, ct);
		}
		catch (OperationCanceledException)
		{
			if (!ct.IsCancellationRequested)
				return (new ChannelResolveResultDto(Success: false, null, classification.CanonicalUrl, null, null, "Timeout", null), StatusCodes.Status504GatewayTimeout);

			return (new ChannelResolveResultDto(Success: false, null, classification.CanonicalUrl, null, null, "Request aborted", null), StatusCodes.Status200OK);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Resolve direct parse failed input={Input}", trimmed);
		}

		if (directMetadata is not null &&
		    ChannelResolveHelper.LooksLikeYouTubeChannelId(directMetadata.YoutubeChannelId) &&
		    !string.IsNullOrWhiteSpace(directMetadata.Title))
		{
			var directMap = new YtDlpChannelResultMapper.ChannelResultMap(
				directMetadata.YoutubeChannelId,
				directMetadata.Title!,
				directMetadata.TitleSlug,
				directMetadata.Description,
				directMetadata.ThumbnailUrl,
				directMetadata.CanonicalUrl,
				classification.Handle is null ? null : "@" + classification.Handle,
				null,
				null);
			var directResolutionMethod = classification.Kind switch
			{
				ChannelResolveHelper.ChannelInputKind.ChannelId => "direct-channel-id",
				ChannelResolveHelper.ChannelInputKind.Handle => "direct-handle",
				_ => "direct-channel-url"
			};
			var items = new[] { ToChannelSearchResultDto(directMap) };
			return (new ChannelResolveResultDto(
				Success: true,
				ChannelId: directMetadata.YoutubeChannelId,
				CanonicalUrl: directMetadata.CanonicalUrl,
				Title: directMetadata.Title,
				ResolutionMethod: directResolutionMethod,
				FailureReason: null,
				Items: items), StatusCodes.Status200OK);
		}

		var ytDlpPath = await _ytDlp.GetExecutablePathAsync(db, ct);
		if (string.IsNullOrWhiteSpace(ytDlpPath))
		{
			logger.LogWarning("Resolve failed: yt-dlp not configured and direct parse did not succeed");
			return (new ChannelResolveResultDto(Success: false, null, classification.CanonicalUrl, null, null, "Channel could not be resolved from the page and yt-dlp is not configured.", null), StatusCodes.Status503ServiceUnavailable);
		}

		var ytDlpCookiesPath = await _ytDlp.GetCookiesPathAsync(db, ct);

		try
		{
			var (results, resolutionMethod) = await _ytDlp.ResolveExactChannelAsync(ytDlpPath, trimmed, ct, resolveTimeoutMs, logger, ytDlpCookiesPath);
			if (results is { Count: > 0 })
			{
				var m0 = results[0];
				var description = directMetadata?.Description ?? m0.Description;
				var thumbnailUrl = directMetadata?.ThumbnailUrl ?? m0.ThumbnailUrl;
				var merged = new YtDlpChannelResultMapper.ChannelResultMap(
					m0.YoutubeChannelId, m0.Title, m0.TitleSlug, description, thumbnailUrl,
					m0.ChannelUrl, m0.Handle, m0.SubscriberCount, m0.VideoCount);
				var items = new[] { ToChannelSearchResultDto(merged) };
				var channelId = items[0].YoutubeChannelId;
				var canonicalUrl = ChannelResolveHelper.GetCanonicalChannelVideosUrl(channelId).Replace("/videos", "", StringComparison.Ordinal);
				return (new ChannelResolveResultDto(
					Success: true,
					ChannelId: channelId,
					CanonicalUrl: canonicalUrl,
					Title: items[0].Title,
					ResolutionMethod: resolutionMethod ?? "direct-channel-id",
					FailureReason: null,
					Items: items), StatusCodes.Status200OK);
			}
		}
		catch (OperationCanceledException)
		{
			if (!ct.IsCancellationRequested)
			{
				logger.LogWarning("Resolve timed out (yt-dlp/HTTP) input={Input}", trimmed);
				return (new ChannelResolveResultDto(Success: false, null, null, null, null, "Timeout", null), StatusCodes.Status504GatewayTimeout);
			}
			logger.LogWarning("Resolve aborted input={Input}", trimmed);
			return (new ChannelResolveResultDto(Success: false, null, null, null, null, "Request aborted", null), StatusCodes.Status200OK);
		}

		logger.LogWarning("Resolve not found input={Input}", trimmed);
		return (new ChannelResolveResultDto(Success: false, null, null, null, null, "Channel not found", null), StatusCodes.Status200OK);
	}

	async Task<(ChannelResolveResultDto Result, int StatusCode)?> TryResolveSearchTermAsYoutubeMediaAsync(
		string trimmed,
		TubeArrDbContext db,
		ILogger logger,
		CancellationToken ct,
		int resolveTimeoutMs)
	{
		string? mediaUrl = null;
		if (BareYoutubeVideoId.IsMatch(trimmed))
			mediaUrl = "https://www.youtube.com/watch?v=" + Uri.EscapeDataString(trimmed);
		else if (trimmed.Contains("youtube.com/watch", StringComparison.OrdinalIgnoreCase) ||
		         trimmed.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase) ||
		         trimmed.Contains("youtube.com/playlist", StringComparison.OrdinalIgnoreCase))
		{
			mediaUrl = trimmed;
			if (!mediaUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !mediaUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			{
				if (mediaUrl.StartsWith("www.", StringComparison.OrdinalIgnoreCase) || mediaUrl.StartsWith("m.", StringComparison.OrdinalIgnoreCase))
					mediaUrl = "https://" + mediaUrl;
				else if (mediaUrl.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) || mediaUrl.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
					mediaUrl = "https://" + mediaUrl.TrimStart('/');
			}
		}

		if (string.IsNullOrWhiteSpace(mediaUrl))
			return null;

		var ytDlpPath = await _ytDlp.GetExecutablePathAsync(db, ct);
		if (string.IsNullOrWhiteSpace(ytDlpPath))
			return null;

		var cookies = await _ytDlp.GetCookiesPathAsync(db, ct);
		try
		{
			var (results, method) = await _ytDlp.ResolveUploaderFromYoutubeMediaUrlAsync(ytDlpPath, mediaUrl!, ct, resolveTimeoutMs, logger, cookies);
			if (results is not { Count: > 0 })
				return null;

			var m0 = results[0];
			var items = new[] { ToChannelSearchResultDto(m0) };
			var channelId = items[0].YoutubeChannelId;
			var canonicalUrl = ChannelResolveHelper.GetCanonicalChannelVideosUrl(channelId).Replace("/videos", "", StringComparison.Ordinal);
			return (new ChannelResolveResultDto(
				Success: true,
				ChannelId: channelId,
				CanonicalUrl: canonicalUrl,
				Title: items[0].Title,
				ResolutionMethod: method ?? "youtube-media-url",
				FailureReason: null,
				Items: items), StatusCodes.Status200OK);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogDebug(ex, "Resolve youtube media URL failed url={Url}", mediaUrl);
			return null;
		}
	}

	public static ChannelSearchResultDto ToChannelSearchResultDto(YtDlpChannelResultMapper.ChannelResultMap m) =>
		new(m.YoutubeChannelId, m.Title, m.TitleSlug, m.Description, m.ThumbnailUrl, m.ChannelUrl, m.Handle, m.SubscriberCount, m.VideoCount);
}
