using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class ChannelResolveEndpoints
{
	internal static void Map(RouteGroupBuilder api)
	{
		api.MapGet("/channels/resolve", async (string? input, HttpContext httpContext, TubeArrDbContext db, ChannelPageMetadataService channelPageMetadataService, ChannelSearchHtmlResolveService channelSearchHtmlResolveService, IYtDlpClient ytDlpClient) =>
		{
			var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("ChannelResolve");
			var trimmed = (input ?? "").Trim();
			if (string.IsNullOrWhiteSpace(trimmed))
			{
				return Results.Json(new ChannelResolveResultDto(Success: false, null, null, null, null, "Empty input", null));
			}

			var ct = httpContext.RequestAborted;
			const int ResolveTimeoutMs = 300_000;

			var classification = ChannelResolveHelper.ClassifyInput(trimmed);
			logger.LogInformation("Resolve start input={Input} kind={Kind} canonicalUrl={CanonicalUrl} channelId={ChannelId} handle={Handle}", trimmed, classification.Kind, classification.CanonicalUrl, classification.ChannelId, classification.Handle);
			if (classification.Kind == ChannelResolveHelper.ChannelInputKind.Empty ||
			    classification.Kind == ChannelResolveHelper.ChannelInputKind.Unknown)
			{
				return Results.Json(new ChannelResolveResultDto(Success: false, null, null, null, null, "Not a resolvable channel identifier; try search instead.", null));
			}

			if (classification.Kind == ChannelResolveHelper.ChannelInputKind.SearchTerm)
			{
				try
				{
					var searchChannelId = await channelSearchHtmlResolveService.ResolveFirstChannelIdAsync(trimmed, ct);
					if (ChannelResolveHelper.LooksLikeYouTubeChannelId(searchChannelId ?? string.Empty))
					{
						var title = searchChannelId;
						ChannelPageMetadata? metadata = null;
						try
						{
							metadata = await channelPageMetadataService.GetMetadataByYoutubeChannelIdAsync(searchChannelId!, ct);
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

						return Results.Json(new ChannelResolveResultDto(
							Success: true,
							ChannelId: searchChannelId,
							CanonicalUrl: $"https://www.youtube.com/channel/{searchChannelId}",
							Title: metadata?.Title ?? title,
							ResolutionMethod: "search-html",
							FailureReason: null,
							Items: items));
					}
				}
				catch (OperationCanceledException)
				{
					if (!httpContext.RequestAborted.IsCancellationRequested)
						return Results.Json(new ChannelResolveResultDto(Success: false, null, null, null, null, "Timeout", null), statusCode: 504);
					return Results.Json(new ChannelResolveResultDto(Success: false, null, null, null, null, "Request aborted", null));
				}
				catch (Exception ex)
				{
					logger.LogWarning(ex, "Resolve search-html failed term={Term}", trimmed);
				}

				var ytDlpSearchPath = await ytDlpClient.GetExecutablePathAsync(db, ct);
				if (string.IsNullOrWhiteSpace(ytDlpSearchPath))
				{
					return Results.Json(new ChannelResolveResultDto(Success: false, null, null, null, null, "yt-dlp is not configured and search-html did not resolve a channel.", null), statusCode: 503);
				}

				try
				{
					var channels = await ytDlpClient.SearchChannelsAsync(ytDlpSearchPath, trimmed, maxResults: 1, ct);
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

						return Results.Json(new ChannelResolveResultDto(
							Success: true,
							ChannelId: m0.YoutubeChannelId,
							CanonicalUrl: m0.ChannelUrl ?? $"https://www.youtube.com/channel/{m0.YoutubeChannelId}",
							Title: m0.Title,
							ResolutionMethod: "search-yt-dlp",
							FailureReason: null,
							Items: items));
					}
				}
				catch (OperationCanceledException)
				{
					if (!httpContext.RequestAborted.IsCancellationRequested)
						return Results.Json(new ChannelResolveResultDto(Success: false, null, null, null, null, "Timeout", null), statusCode: 504);
					return Results.Json(new ChannelResolveResultDto(Success: false, null, null, null, null, "Request aborted", null));
				}
				catch (Exception ex)
				{
					logger.LogWarning(ex, "Resolve search-yt-dlp failed term={Term}", trimmed);
				}

				return Results.Json(new ChannelResolveResultDto(Success: false, null, null, null, "search-html/yt-dlp", "Channel not found", null));
			}

			ChannelPageMetadata? directMetadata = null;
			try
			{
				if (ChannelResolveHelper.LooksLikeYouTubeChannelId(classification.ChannelId ?? string.Empty))
					directMetadata = await channelPageMetadataService.GetMetadataByYoutubeChannelIdAsync(classification.ChannelId!, ct);
				else if (!string.IsNullOrWhiteSpace(classification.CanonicalUrl))
					directMetadata = await channelPageMetadataService.GetMetadataFromUrlAsync(classification.CanonicalUrl!, null, ct);
			}
			catch (OperationCanceledException)
			{
				if (!httpContext.RequestAborted.IsCancellationRequested)
					return Results.Json(new ChannelResolveResultDto(Success: false, null, classification.CanonicalUrl, null, null, "Timeout", null), statusCode: 504);

				return Results.Json(new ChannelResolveResultDto(Success: false, null, classification.CanonicalUrl, null, null, "Request aborted", null));
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
				return Results.Json(new ChannelResolveResultDto(
					Success: true,
					ChannelId: directMetadata.YoutubeChannelId,
					CanonicalUrl: directMetadata.CanonicalUrl,
					Title: directMetadata.Title,
					ResolutionMethod: directResolutionMethod,
					FailureReason: null,
					Items: items));
			}

			var ytDlpPath = await ytDlpClient.GetExecutablePathAsync(db, ct);
			if (string.IsNullOrWhiteSpace(ytDlpPath))
			{
				logger.LogWarning("Resolve failed: yt-dlp not configured and direct parse did not succeed");
				return Results.Json(new ChannelResolveResultDto(Success: false, null, classification.CanonicalUrl, null, null, "Channel could not be resolved from the page and yt-dlp is not configured.", null), statusCode: 503);
			}

			try
			{
				var (results, resolutionMethod) = await ytDlpClient.ResolveExactChannelAsync(ytDlpPath, trimmed, ct, ResolveTimeoutMs, logger);
				if (results != null && results.Count > 0)
				{
					var m0 = results[0];
					var description = directMetadata?.Description ?? m0.Description;
					var thumbnailUrl = directMetadata?.ThumbnailUrl ?? m0.ThumbnailUrl;
					var merged = new YtDlpChannelResultMapper.ChannelResultMap(
						m0.YoutubeChannelId, m0.Title, m0.TitleSlug, description, thumbnailUrl,
						m0.ChannelUrl, m0.Handle, m0.SubscriberCount, m0.VideoCount);
					var items = new[] { ToChannelSearchResultDto(merged) };
					var channelId = items[0].YoutubeChannelId;
					var canonicalUrl = ChannelResolveHelper.GetCanonicalChannelVideosUrl(channelId).Replace("/videos", "");
					return Results.Json(new ChannelResolveResultDto(
						Success: true,
						ChannelId: channelId,
						CanonicalUrl: canonicalUrl,
						Title: items[0].Title,
						ResolutionMethod: resolutionMethod ?? "direct-channel-id",
						FailureReason: null,
						Items: items
					));
				}
			}
			catch (OperationCanceledException)
			{
				if (!httpContext.RequestAborted.IsCancellationRequested)
				{
					logger.LogWarning("Resolve timed out (yt-dlp/HTTP) input={Input}", trimmed);
					return Results.Json(new ChannelResolveResultDto(Success: false, null, null, null, null, "Timeout", null), statusCode: 504);
				}
				logger.LogWarning("Resolve aborted input={Input}", trimmed);
				return Results.Json(new ChannelResolveResultDto(Success: false, null, null, null, null, "Request aborted", null));
			}
			logger.LogWarning("Resolve not found input={Input}", trimmed);
			return Results.Json(new ChannelResolveResultDto(Success: false, null, null, null, null, "Channel not found", null));
		});

		api.MapGet("/channels/search", async (string term, HttpContext httpContext, TubeArrDbContext db, ChannelSearchHtmlResolveService channelSearchHtmlResolveService, ChannelPageMetadataService channelPageMetadataService, IYtDlpClient ytDlpClient) =>
		{
			var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("ChannelSearch");
			var trimmed = term?.Trim() ?? "";
			if (string.IsNullOrWhiteSpace(trimmed))
			{
				return Results.Json(Array.Empty<ChannelSearchResultDto>());
			}

			try
			{
				var htmlCandidates = await channelSearchHtmlResolveService.SearchChannelsAsync(trimmed, maxResults: 20, httpContext.RequestAborted);
				if (htmlCandidates.Count > 0)
				{
					var metadataByChannelId = new Dictionary<string, ChannelPageMetadata>(StringComparer.OrdinalIgnoreCase);
					var needsEnrichment = htmlCandidates
						.Where(c =>
							string.IsNullOrWhiteSpace(c.ThumbnailUrl) ||
							string.IsNullOrWhiteSpace(c.Description) ||
							string.IsNullOrWhiteSpace(c.Title) ||
							string.Equals(c.Title, c.YoutubeChannelId, StringComparison.OrdinalIgnoreCase))
						.Take(5)
						.ToArray();

					foreach (var candidate in needsEnrichment)
					{
						try
						{
							var metadata = await channelPageMetadataService.GetMetadataByYoutubeChannelIdAsync(candidate.YoutubeChannelId, httpContext.RequestAborted);
							if (metadata is not null)
							{
								metadataByChannelId[candidate.YoutubeChannelId] = metadata;
							}
						}
						catch (OperationCanceledException)
						{
							throw;
						}
						catch (Exception ex)
						{
							logger.LogDebug(ex, "Search metadata enrichment failed for channelId={ChannelId}", candidate.YoutubeChannelId);
						}
					}

					var results = htmlCandidates.Select(c => new ChannelSearchResultDto(
						YoutubeChannelId: c.YoutubeChannelId,
						Title: string.IsNullOrWhiteSpace(c.Title) || string.Equals(c.Title, c.YoutubeChannelId, StringComparison.OrdinalIgnoreCase)
							? metadataByChannelId.GetValueOrDefault(c.YoutubeChannelId)?.Title ?? c.YoutubeChannelId
							: c.Title,
						TitleSlug: SlugHelper.Slugify(
							(string.IsNullOrWhiteSpace(c.Title) || string.Equals(c.Title, c.YoutubeChannelId, StringComparison.OrdinalIgnoreCase)
								? metadataByChannelId.GetValueOrDefault(c.YoutubeChannelId)?.Title ?? c.YoutubeChannelId
								: c.Title)),
						Description: string.IsNullOrWhiteSpace(c.Description)
							? metadataByChannelId.GetValueOrDefault(c.YoutubeChannelId)?.Description
							: c.Description,
						ThumbnailUrl: string.IsNullOrWhiteSpace(c.ThumbnailUrl)
							? metadataByChannelId.GetValueOrDefault(c.YoutubeChannelId)?.ThumbnailUrl
							: c.ThumbnailUrl,
						ChannelUrl: $"https://www.youtube.com/channel/{c.YoutubeChannelId}",
						Handle: null,
						SubscriberCount: null,
						VideoCount: null)).ToArray();
					return Results.Json(results);
				}

				var ytDlpPath = await ytDlpClient.GetExecutablePathAsync(db, httpContext.RequestAborted);
				if (string.IsNullOrWhiteSpace(ytDlpPath))
				{
					return Results.Json(new { message = "yt-dlp is not configured (and search-html returned no results)." }, statusCode: 503);
				}

				var channels = await ytDlpClient.SearchChannelsAsync(ytDlpPath, trimmed, maxResults: 20, httpContext.RequestAborted);
				var ytResults = channels.Select(m => ToChannelSearchResultDto(m)).ToArray();
				return Results.Json(ytResults);
			}
			catch (OperationCanceledException)
			{
				return Results.Json(Array.Empty<ChannelSearchResultDto>());
			}
		});
	}

	private static ChannelSearchResultDto ToChannelSearchResultDto(YtDlpChannelResultMapper.ChannelResultMap m) =>
		new ChannelSearchResultDto(m.YoutubeChannelId, m.Title, m.TitleSlug, m.Description, m.ThumbnailUrl, m.ChannelUrl, m.Handle, m.SubscriberCount, m.VideoCount);
}
