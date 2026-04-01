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
		api.MapGet("/channels/resolve", async (string? input, HttpContext httpContext, TubeArrDbContext db, ChannelResolveService channelResolveService) =>
		{
			var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("ChannelResolve");
			const int resolveTimeoutMs = 300_000;
			var (result, status) = await channelResolveService.ResolveAsync(input ?? "", db, logger, httpContext.RequestAborted, resolveTimeoutMs);
			return Results.Json(result, statusCode: status);
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

				var ytDlpSearchCookiesPath = await ytDlpClient.GetCookiesPathAsync(db, httpContext.RequestAborted);
				var channels = await ytDlpClient.SearchChannelsAsync(ytDlpPath, trimmed, maxResults: 20, httpContext.RequestAborted, ytDlpSearchCookiesPath);
				var ytResults = channels.Select(m => ChannelResolveService.ToChannelSearchResultDto(m)).ToArray();
				return Results.Json(ytResults);
			}
			catch (OperationCanceledException)
			{
				return Results.Json(Array.Empty<ChannelSearchResultDto>());
			}
		});
	}
}
