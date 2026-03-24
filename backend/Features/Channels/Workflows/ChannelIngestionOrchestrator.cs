using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;
using TubeArr.Backend.Realtime;

namespace TubeArr.Backend;

public sealed class ChannelIngestionOrchestrator
{
	readonly ChannelPageMetadataService _channelPageMetadataService;
	readonly IYtDlpClient _ytDlpClient;
	readonly IServiceScopeFactory _scopeFactory;
	readonly CommandDispatcher _commandDispatcher;
	readonly IRealtimeEventBroadcaster _realtime;
	readonly ILogger<ChannelIngestionOrchestrator> _logger;

	public ChannelIngestionOrchestrator(
		ChannelPageMetadataService channelPageMetadataService,
		IYtDlpClient ytDlpClient,
		IServiceScopeFactory scopeFactory,
		CommandDispatcher commandDispatcher,
		IRealtimeEventBroadcaster realtime,
		ILogger<ChannelIngestionOrchestrator> logger)
	{
		_channelPageMetadataService = channelPageMetadataService;
		_ytDlpClient = ytDlpClient;
		_scopeFactory = scopeFactory;
		_commandDispatcher = commandDispatcher;
		_realtime = realtime;
		_logger = logger;
	}

	public async Task<(ChannelEntity? Channel, bool WasNew, string? ErrorMessage)> CreateOrUpdateAsync(
		CreateChannelRequest request,
		TubeArrDbContext db,
		CancellationToken ct = default)
	{
		var youtubeChannelId = (request.YoutubeChannelId ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(youtubeChannelId))
			return (null, false, "youtubeChannelId is required");

		var title = (request.Title ?? string.Empty).Trim();
		var description = request.Description;
		var thumbnailUrl = (string?)null;
		var bannerUrl = (string?)null;
		var monitored = request.Monitored;
		var qualityProfileId = request.QualityProfileId.HasValue && request.QualityProfileId.Value > 0
			? request.QualityProfileId
			: null;
		var rootFolderPath = request.RootFolderPath;
		var channelType = request.ChannelType;
		var playlistFolder = request.PlaylistFolder;
		var monitorNewItems = request.MonitorNewItems ?? (monitored ? 1 : 0);
		var roundRobinCap = request.RoundRobinLatestVideoCount is int rr && rr > 0 ? rr : (int?)null;
		var tags = NormalizeTags(request.Tags);
		var path = request.Path;
		var filterOutShorts = request.FilterOutShorts;
		var filterOutLivestreams = request.FilterOutLivestreams;

		if (ChannelResolveHelper.LooksLikeYouTubeChannelId(youtubeChannelId))
		{
			ChannelPageMetadata? directChannelMetadata = null;
			try
			{
				directChannelMetadata = await _channelPageMetadataService.GetMetadataByYoutubeChannelIdAsync(youtubeChannelId, ct);
			}
			catch (Exception ex)
			{
				_logger.LogDebug(ex, "Channel metadata direct parse threw for {YoutubeChannelId}", youtubeChannelId);
			}

			ChannelPageMetadata? fallbackChannelMetadata = null;
			if (!HasCompleteChannelMetadata(directChannelMetadata))
			{
				var ytDlpPath = await _ytDlpClient.GetExecutablePathAsync(db, ct);
				if (!string.IsNullOrWhiteSpace(ytDlpPath))
				{
					try
					{
						var enriched = await _ytDlpClient.EnrichChannelForCreateAsync(ytDlpPath, youtubeChannelId, ct);
						if (enriched.HasValue)
						{
							var (ytTitle, ytDesc, ytThumb, _, _) = enriched.Value;
							fallbackChannelMetadata = new ChannelPageMetadata(
								YoutubeChannelId: youtubeChannelId,
								Title: ytTitle,
								Description: ytDesc,
								ThumbnailUrl: ytThumb,
								BannerUrl: null,
								CanonicalUrl: $"https://www.youtube.com/channel/{youtubeChannelId}");
						}
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Channel metadata yt-dlp fallback failed for {YoutubeChannelId}", youtubeChannelId);
					}
				}
				else
				{
					_logger.LogDebug("Channel metadata yt-dlp fallback unavailable (yt-dlp not configured) for {YoutubeChannelId}", youtubeChannelId);
				}
			}

			var mergedTitle = directChannelMetadata?.Title ?? fallbackChannelMetadata?.Title;
			var mergedDescription = directChannelMetadata?.Description ?? fallbackChannelMetadata?.Description;
			var mergedThumbnailUrl = directChannelMetadata?.ThumbnailUrl ?? fallbackChannelMetadata?.ThumbnailUrl;
			var mergedBannerUrl = directChannelMetadata?.BannerUrl ?? fallbackChannelMetadata?.BannerUrl;
			if (string.IsNullOrWhiteSpace(mergedTitle) ||
				string.IsNullOrWhiteSpace(mergedDescription) ||
				string.IsNullOrWhiteSpace(mergedThumbnailUrl) ||
				string.IsNullOrWhiteSpace(mergedBannerUrl))
			{
				_logger.LogWarning("Channel metadata remained incomplete after direct parse and fallback for {YoutubeChannelId}", youtubeChannelId);
			}

			if (string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(mergedTitle))
				title = mergedTitle.Trim();
			if (string.IsNullOrWhiteSpace(description) && !string.IsNullOrWhiteSpace(mergedDescription))
				description = mergedDescription;
			if (string.IsNullOrWhiteSpace(thumbnailUrl) && !string.IsNullOrWhiteSpace(mergedThumbnailUrl))
				thumbnailUrl = mergedThumbnailUrl.Trim();
			if (string.IsNullOrWhiteSpace(bannerUrl) && !string.IsNullOrWhiteSpace(mergedBannerUrl))
				bannerUrl = mergedBannerUrl.Trim();
		}

		if (string.IsNullOrWhiteSpace(title))
			return (null, false, "title is required after metadata extraction.");

		var titleSlug = SlugHelper.Slugify(title);
		var storage = await ResolveChannelStorageAsync(
			db,
			youtubeChannelId,
			title,
			titleSlug,
			rootFolderPath,
			path,
			ct);
		rootFolderPath = storage.RootFolderPath;
		path = storage.ChannelPath;

		var wasNew = false;
		var existing = await db.Channels.FirstOrDefaultAsync(x => x.YoutubeChannelId == youtubeChannelId, ct);
		if (existing is null)
		{
			wasNew = true;
			existing = new ChannelEntity
			{
				YoutubeChannelId = youtubeChannelId,
				Title = title,
				Description = description,
				ThumbnailUrl = string.IsNullOrWhiteSpace(thumbnailUrl) ? null : thumbnailUrl,
				BannerUrl = string.IsNullOrWhiteSpace(bannerUrl) ? null : bannerUrl,
				TitleSlug = titleSlug,
				Monitored = monitored,
				Added = DateTimeOffset.UtcNow,
				QualityProfileId = qualityProfileId,
				Path = path,
				RootFolderPath = rootFolderPath,
				Tags = tags,
				MonitorNewItems = monitorNewItems,
				ChannelType = channelType,
				PlaylistFolder = playlistFolder,
				RoundRobinLatestVideoCount = roundRobinCap,
				FilterOutShorts = filterOutShorts,
				FilterOutLivestreams = filterOutLivestreams
			};

			db.Channels.Add(existing);
		}
		else
		{
			existing.Title = title;
			existing.TitleSlug = titleSlug;
			existing.Description = description;
			existing.ThumbnailUrl = string.IsNullOrWhiteSpace(thumbnailUrl) ? existing.ThumbnailUrl : thumbnailUrl;
			existing.BannerUrl = string.IsNullOrWhiteSpace(bannerUrl) ? existing.BannerUrl : bannerUrl;
			existing.Monitored = monitored;
			existing.QualityProfileId = qualityProfileId;
			if (!string.IsNullOrWhiteSpace(path))
				existing.Path = path;
			if (!string.IsNullOrWhiteSpace(rootFolderPath))
				existing.RootFolderPath = rootFolderPath;
			if (request.Tags is not null)
				existing.Tags = tags;
			if (request.MonitorNewItems.HasValue || !existing.MonitorNewItems.HasValue)
				existing.MonitorNewItems = monitorNewItems;
			if (channelType is not null)
				existing.ChannelType = channelType;
			if (playlistFolder.HasValue)
				existing.PlaylistFolder = playlistFolder;
			if (request.RoundRobinLatestVideoCount.HasValue)
				existing.RoundRobinLatestVideoCount = request.RoundRobinLatestVideoCount.Value <= 0 ? null : request.RoundRobinLatestVideoCount.Value;
			existing.FilterOutShorts = filterOutShorts;
			existing.FilterOutLivestreams = filterOutLivestreams;
		}

		await db.SaveChangesAsync(ct);

		if (!wasNew)
			await RoundRobinMonitoringHelper.ApplyForChannelAsync(db, existing.Id, ct);
		else
		{
			try
			{
				await _commandDispatcher.QueueRefreshChannelAsync(existing.Id, "auto", _realtime);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Queueing channel metadata acquisition failed for {ChannelId}", existing.Id);
			}
		}

		return (existing, wasNew, null);
	}

	private static bool HasCompleteChannelMetadata(ChannelPageMetadata? metadata)
	{
		return metadata is not null &&
			!string.IsNullOrWhiteSpace(metadata.Title) &&
			!string.IsNullOrWhiteSpace(metadata.Description) &&
			!string.IsNullOrWhiteSpace(metadata.ThumbnailUrl) &&
			!string.IsNullOrWhiteSpace(metadata.BannerUrl);
	}

	private static string? NormalizeTags(int[]? tags)
	{
		if (tags is not { Length: > 0 })
			return null;

		return string.Join(",", tags.Distinct().OrderBy(id => id));
	}

	private static async Task<(string? RootFolderPath, string? ChannelPath)> ResolveChannelStorageAsync(
		TubeArrDbContext db,
		string youtubeChannelId,
		string title,
		string titleSlug,
		string? requestedRootFolderPath,
		string? requestedPath,
		CancellationToken ct = default)
	{
		var normalizedRequestedPath = string.IsNullOrWhiteSpace(requestedPath) ? null : requestedPath.Trim();
		var normalizedRootFolderPath = string.IsNullOrWhiteSpace(requestedRootFolderPath)
			? await db.RootFolders.AsNoTracking().OrderBy(x => x.Path).Select(x => x.Path).FirstOrDefaultAsync(ct)
			: requestedRootFolderPath.Trim();

		if (!string.IsNullOrWhiteSpace(normalizedRequestedPath))
			return (normalizedRootFolderPath, normalizedRequestedPath);

		var naming = await db.NamingConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct) ?? new NamingConfigEntity { Id = 1 };
		var previewChannel = new ChannelEntity
		{
			YoutubeChannelId = youtubeChannelId,
			Title = title,
			TitleSlug = titleSlug
		};
		var dummyVideo = new VideoEntity
		{
			Title = title,
			YoutubeVideoId = youtubeChannelId,
			UploadDateUtc = DateTimeOffset.UtcNow
		};
		var context = new VideoFileNaming.NamingContext(
			Channel: previewChannel,
			Video: dummyVideo,
			Playlist: null,
			PlaylistIndex: null,
			QualityFull: null,
			Resolution: null,
			Extension: null);
		var folderName = VideoFileNaming.BuildFolderName(naming.ChannelFolderFormat, context, naming);
		if (string.IsNullOrWhiteSpace(folderName))
			folderName = titleSlug;

		if (string.IsNullOrWhiteSpace(folderName))
			return (normalizedRootFolderPath, null);

		if (string.IsNullOrWhiteSpace(normalizedRootFolderPath))
			return (null, folderName);

		return (normalizedRootFolderPath, Path.Combine(normalizedRootFolderPath, folderName));
	}

}
