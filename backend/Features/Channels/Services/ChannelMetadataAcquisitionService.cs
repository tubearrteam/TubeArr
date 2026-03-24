using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

public sealed class ChannelMetadataAcquisitionService
{
	static readonly DateTimeOffset PlaceholderDateUtc = DateTimeOffset.UnixEpoch;

	readonly ChannelPageMetadataService _channelPageMetadataService;
	readonly ChannelVideoDiscoveryService _channelVideoDiscoveryService;
	readonly VideoWatchPageMetadataService _videoWatchPageMetadataService;
	readonly YouTubeDataApiMetadataService? _youTubeDataApiMetadataService;
	readonly ILogger<ChannelMetadataAcquisitionService> _logger;
	readonly Func<TubeArrDbContext, string, CancellationToken, Task<ChannelPageMetadata?>> _channelMetadataFallbackAsync;
	readonly Func<TubeArrDbContext, string, CancellationToken, Task<IReadOnlyList<ChannelVideoDiscoveryItem>>> _videoDiscoveryFallbackAsync;
	readonly Func<TubeArrDbContext, string, CancellationToken, Task<VideoWatchPageMetadata?>> _videoMetadataFallbackAsync;

	public ChannelMetadataAcquisitionService(
		ChannelPageMetadataService channelPageMetadataService,
		ChannelVideoDiscoveryService channelVideoDiscoveryService,
		VideoWatchPageMetadataService videoWatchPageMetadataService,
		ILogger<ChannelMetadataAcquisitionService> logger)
		: this(
			channelPageMetadataService,
			channelVideoDiscoveryService,
			videoWatchPageMetadataService,
			logger,
			DefaultChannelMetadataFallbackAsync,
			DefaultVideoDiscoveryFallbackAsync,
			DefaultVideoMetadataFallbackAsync,
			null)
	{
	}

	public ChannelMetadataAcquisitionService(
		ChannelPageMetadataService channelPageMetadataService,
		ChannelVideoDiscoveryService channelVideoDiscoveryService,
		VideoWatchPageMetadataService videoWatchPageMetadataService,
		YouTubeDataApiMetadataService youTubeDataApiMetadataService,
		ILogger<ChannelMetadataAcquisitionService> logger)
		: this(
			channelPageMetadataService,
			channelVideoDiscoveryService,
			videoWatchPageMetadataService,
			logger,
			DefaultChannelMetadataFallbackAsync,
			DefaultVideoDiscoveryFallbackAsync,
			DefaultVideoMetadataFallbackAsync,
			youTubeDataApiMetadataService)
	{
	}

	public ChannelMetadataAcquisitionService(
		ChannelPageMetadataService channelPageMetadataService,
		ChannelVideoDiscoveryService channelVideoDiscoveryService,
		VideoWatchPageMetadataService videoWatchPageMetadataService,
		ILogger<ChannelMetadataAcquisitionService> logger,
		Func<TubeArrDbContext, string, CancellationToken, Task<ChannelPageMetadata?>> channelMetadataFallbackAsync,
		Func<TubeArrDbContext, string, CancellationToken, Task<IReadOnlyList<ChannelVideoDiscoveryItem>>> videoDiscoveryFallbackAsync,
		Func<TubeArrDbContext, string, CancellationToken, Task<VideoWatchPageMetadata?>> videoMetadataFallbackAsync,
		YouTubeDataApiMetadataService? youTubeDataApiMetadataService = null)
	{
		_channelPageMetadataService = channelPageMetadataService;
		_channelVideoDiscoveryService = channelVideoDiscoveryService;
		_videoWatchPageMetadataService = videoWatchPageMetadataService;
		_youTubeDataApiMetadataService = youTubeDataApiMetadataService;
		_logger = logger;
		_channelMetadataFallbackAsync = channelMetadataFallbackAsync;
		_videoDiscoveryFallbackAsync = videoDiscoveryFallbackAsync;
		_videoMetadataFallbackAsync = videoMetadataFallbackAsync;
	}

	public async Task<string?> PopulateChannelDetailsAsync(
		TubeArrDbContext db,
		int channelId,
		MetadataProgressReporter? progressReporter = null,
		CancellationToken ct = default)
	{
		var channel = await db.Channels.FirstOrDefaultAsync(c => c.Id == channelId, ct);
		if (channel is null)
			return null;

		if (!ChannelResolveHelper.LooksLikeYouTubeChannelId(channel.YoutubeChannelId))
		{
			if (progressReporter is not null)
			{
				var invalidChannelError = "Channel has no valid YouTube channel ID; cannot fetch metadata.";
				await progressReporter.AddStageErrorAsync(
					"channelVideoListFetching",
					"Channel video list fetching",
					invalidChannelError,
					ct);
				await progressReporter.IncrementStageAsync(
					"channelVideoListFetching",
					"Channel video list fetching",
					total: 1,
					detail: $"Unable to fetch video list for channel {channel.Id}.",
					ct);
			}
			return "Channel has no valid YouTube channel ID; cannot fetch metadata.";
		}

		var directChannelMetadata = await TryGetDirectChannelMetadataAsync(db, channel.YoutubeChannelId, ct);
		ChannelPageMetadata? fallbackChannelMetadata = null;
		if (!HasCompleteChannelMetadata(directChannelMetadata))
		{
			_logger.LogWarning("Channel metadata direct parse failed for {YoutubeChannelId}", channel.YoutubeChannelId);
			fallbackChannelMetadata = await TryGetFallbackChannelMetadataAsync(db, channel.YoutubeChannelId, ct);
		}

		var mergedChannelMetadata = MergeChannelMetadata(channel.YoutubeChannelId, directChannelMetadata, fallbackChannelMetadata);
		if (mergedChannelMetadata is null || string.IsNullOrWhiteSpace(mergedChannelMetadata.Title))
		{
			var channelMetadataError = $"Channel metadata extraction failed for {channel.YoutubeChannelId}.";
			if (progressReporter is not null)
			{
				await progressReporter.AddStageErrorAsync(
					"channelVideoListFetching",
					"Channel video list fetching",
					channelMetadataError,
					ct);
				await progressReporter.IncrementStageAsync(
					"channelVideoListFetching",
					"Channel video list fetching",
					total: 1,
					detail: $"Unable to fetch video list for channel {channel.YoutubeChannelId}.",
					ct);
			}
			return $"Channel metadata extraction failed for {channel.YoutubeChannelId}.";
		}

		ApplyChannelMetadata(channel, mergedChannelMetadata);
		await db.SaveChangesAsync(ct);

		var (directDiscoveredVideos, usedYouTubeApiListing) = await TryDiscoverVideosDirectAsync(db, channel.YoutubeChannelId, ct);
		if (directDiscoveredVideos.Count == 0)
			_logger.LogWarning("Video discovery direct parse failed for channel {YoutubeChannelId}", channel.YoutubeChannelId);

		var fallbackDiscoveredVideos = await TryDiscoverVideosFallbackAsync(
			db,
			channel.YoutubeChannelId,
			ct,
			required: directDiscoveredVideos.Count == 0);
		var discoveredVideos = MergeDiscoveredVideos(directDiscoveredVideos, fallbackDiscoveredVideos);
		if (progressReporter is not null)
		{
			await progressReporter.IncrementStageAsync(
				"channelVideoListFetching",
				"Channel video list fetching",
				total: 1,
				detail: $"Fetched video list for {channel.Title}. Discovered {discoveredVideos.Count} video(s).",
				ct);
		}

		if (discoveredVideos.Count == 0)
		{
			var discoveryError = $"Video discovery failed for channel {channel.YoutubeChannelId}.";
			if (progressReporter is not null)
			{
				await progressReporter.AddStageErrorAsync(
					"channelVideoListFetching",
					"Channel video list fetching",
					discoveryError,
					ct);
			}
			return $"Video discovery failed for channel {channel.YoutubeChannelId}.";
		}

		var upsertResult = await UpsertDiscoveredVideosAsync(db, channel, discoveredVideos, ct);
		await HydrateVideosFromChannelFallbackAsync(db, channel, requireRuntime: false, ct);
		var hydrateCandidates = await db.Videos
			.Where(v => v.ChannelId == channelId)
			.OrderBy(v => v.Id)
			.ToListAsync(ct);

		var hydrateTargets = hydrateCandidates
			.Where(v => NeedsHydrate(v, requireRuntime: false))
			.ToList();

		if (usedYouTubeApiListing && hydrateTargets.Count == 0)
		{
			_logger.LogInformation(
				"Skipping additional video detail enrichment for channel {YoutubeChannelId} because playlistItems fields were sufficient.",
				channel.YoutubeChannelId);
		}

		if (progressReporter is not null)
		{
			await progressReporter.AddToStageTotalAsync(
				"videoDetailFetching",
				"Video detail fetching",
				hydrateTargets.Count,
				detail: hydrateTargets.Count > 0
					? $"Queued {hydrateTargets.Count} video detail fetch(es) for {channel.Title}."
					: $"No video detail refresh needed for {channel.Title}.",
				ct);
		}
		await HydrateVideosAsync(db, hydrateTargets, progressReporter, "videoDetailFetching", ct);
		await RoundRobinMonitoringHelper.ApplyForChannelAsync(db, channelId, ct);
		return null;
	}

	public async Task<string?> PopulateVideoMetadataAsync(
		TubeArrDbContext db,
		int channelId,
		MetadataProgressReporter? progressReporter = null,
		CancellationToken ct = default)
	{
		var channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == channelId, ct);
		if (channel is null)
			return "Channel not found.";

		var hydrateTargets = (await db.Videos
			.Where(v => v.ChannelId == channelId)
			.OrderBy(v => v.Id)
			.ToListAsync(ct))
			.Where(v => NeedsHydrate(v))
			.ToList();

		if (hydrateTargets.Count == 0)
		{
			if (progressReporter is not null)
			{
				await progressReporter.SetStageAsync(
					"videoDetailFetching",
					"Video detail fetching",
					0,
					0,
					detail: "All video metadata is already current.",
					ct);
			}
			await RoundRobinMonitoringHelper.ApplyForChannelAsync(db, channelId, ct);
			return "All video metadata is already current.";
		}

		if (progressReporter is not null)
		{
			await progressReporter.AddToStageTotalAsync(
				"videoDetailFetching",
				"Video detail fetching",
				hydrateTargets.Count,
				detail: $"Queued {hydrateTargets.Count} video detail fetch(es) for {channel.Title}.",
				ct);
		}
		await HydrateVideosAsync(db, hydrateTargets, progressReporter, "videoDetailFetching", ct);
		await RoundRobinMonitoringHelper.ApplyForChannelAsync(db, channelId, ct);
		return $"Updated metadata for {hydrateTargets.Count} video(s).";
	}

	async Task<ChannelPageMetadata?> TryGetDirectChannelMetadataAsync(TubeArrDbContext db, string youtubeChannelId, CancellationToken ct)
	{
		if (_youTubeDataApiMetadataService is not null)
		{
			var preference = await _youTubeDataApiMetadataService.GetPreferenceAsync(db, ct);
			if (preference.UseYouTubeApi && preference.IsPrioritized(YouTubeApiMetadataPriorityItems.ChannelMetadata))
			{
				var api = await _youTubeDataApiMetadataService.TryGetChannelMetadataAsync(db, youtubeChannelId, ct);
				if (api is not null)
					return api;
			}
		}

		try
		{
			return await _channelPageMetadataService.GetMetadataByYoutubeChannelIdAsync(youtubeChannelId, ct);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Channel metadata direct parse failed for {YoutubeChannelId}", youtubeChannelId);
			return null;
		}
	}

	async Task<ChannelPageMetadata?> TryGetFallbackChannelMetadataAsync(TubeArrDbContext db, string youtubeChannelId, CancellationToken ct)
	{
		try
		{
			var fallback = await _channelMetadataFallbackAsync(db, youtubeChannelId, ct);
			if (fallback is null)
				_logger.LogError("Channel metadata yt-dlp fallback failed for {YoutubeChannelId}", youtubeChannelId);
			return fallback;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Channel metadata yt-dlp fallback failed for {YoutubeChannelId}", youtubeChannelId);
			return null;
		}
	}

	async Task<(IReadOnlyList<ChannelVideoDiscoveryItem> Items, bool UsedYouTubeApi)> TryDiscoverVideosDirectAsync(TubeArrDbContext db, string youtubeChannelId, CancellationToken ct)
	{
		if (_youTubeDataApiMetadataService is not null)
		{
			var preference = await _youTubeDataApiMetadataService.GetPreferenceAsync(db, ct);
			if (preference.UseYouTubeApi && preference.IsPrioritized(YouTubeApiMetadataPriorityItems.VideoListing))
			{
				var api = await _youTubeDataApiMetadataService.TryDiscoverChannelVideosAsync(db, youtubeChannelId, ct);
				if (api.Items.Count > 0)
					return (api.Items, true);
			}
		}

		try
		{
			return (await _channelVideoDiscoveryService.DiscoverVideosAsync(youtubeChannelId, ct), false);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Video discovery direct parse failed for channel {YoutubeChannelId}", youtubeChannelId);
			return (Array.Empty<ChannelVideoDiscoveryItem>(), false);
		}
	}

	async Task<IReadOnlyList<ChannelVideoDiscoveryItem>> TryDiscoverVideosFallbackAsync(
		TubeArrDbContext db,
		string youtubeChannelId,
		CancellationToken ct,
		bool required)
	{
		try
		{
			var fallback = await _videoDiscoveryFallbackAsync(db, youtubeChannelId, ct);
			if (required && fallback.Count == 0)
				_logger.LogError("Video discovery yt-dlp fallback failed for channel {YoutubeChannelId}", youtubeChannelId);
			return fallback;
		}
		catch (Exception ex)
		{
			if (required)
				_logger.LogError(ex, "Video discovery yt-dlp fallback failed for channel {YoutubeChannelId}", youtubeChannelId);
			else
				_logger.LogWarning(ex, "Video discovery yt-dlp supplement failed for channel {YoutubeChannelId}", youtubeChannelId);
			return Array.Empty<ChannelVideoDiscoveryItem>();
		}
	}

	async Task HydrateVideosFromChannelFallbackAsync(TubeArrDbContext db, ChannelEntity channel, bool requireRuntime, CancellationToken ct)
	{
		var pendingVideos = (await db.Videos
			.Where(v => v.ChannelId == channel.Id)
			.OrderBy(v => v.Id)
			.ToListAsync(ct))
			.Where(v => NeedsHydrate(v, requireRuntime))
			.ToList();
		if (pendingVideos.Count == 0)
			return;

		var executablePath = await YtDlpMetadataService.GetExecutablePathAsync(db, ct);
		if (string.IsNullOrWhiteSpace(executablePath))
			return;

		var pendingByYoutubeId = pendingVideos
			.Where(v => !string.IsNullOrWhiteSpace(v.YoutubeVideoId))
			.ToDictionary(v => v.YoutubeVideoId, StringComparer.OrdinalIgnoreCase);
		if (pendingByYoutubeId.Count == 0)
			return;

		var docs = await YtDlpMetadataService.RunYtDlpJsonAsync(
			executablePath,
			ChannelResolveHelper.GetCanonicalChannelVideosUrl(channel.YoutubeChannelId),
			ct,
			playlistItems: null,
			timeoutMs: 120_000,
			flatPlaylist: false);

		var pending = 0;
		try
		{
			foreach (var doc in docs)
			{
				var metadataByYoutubeId = new Dictionary<string, VideoWatchPageMetadata>(StringComparer.OrdinalIgnoreCase);
				CollectYtDlpVideoMetadata(doc.RootElement, metadataByYoutubeId);
				foreach (var pair in metadataByYoutubeId)
				{
					if (!pendingByYoutubeId.TryGetValue(pair.Key, out var video))
						continue;

					ApplyVideoMetadata(video, pair.Value);
					pendingByYoutubeId.Remove(pair.Key);
					pending++;

					if (pending >= 25)
					{
						await db.SaveChangesAsync(ct);
						pending = 0;
					}
				}

				if (pendingByYoutubeId.Count == 0)
					break;
			}
		}
		finally
		{
			foreach (var doc in docs)
				doc.Dispose();
		}

		if (pending > 0)
			await db.SaveChangesAsync(ct);
	}

	async Task HydrateVideosAsync(
		TubeArrDbContext db,
		IReadOnlyList<VideoEntity> videos,
		MetadataProgressReporter? progressReporter,
		string progressStageKey,
		CancellationToken ct)
	{
		var (batchedDirectMetadataByYoutubeId, usedYouTubeApiBatching) = await TryGetDirectVideoMetadataBatchAsync(db, videos, ct);
		var pending = 0;
		var processed = 0;

		foreach (var video in videos)
		{
			ct.ThrowIfCancellationRequested();
			if (string.IsNullOrWhiteSpace(video.YoutubeVideoId))
			{
				processed++;
				var missingIdError = $"Video {video.Id}: Missing YouTube video ID.";
				if (progressReporter is not null)
				{
					await progressReporter.AddStageErrorAsync(
						progressStageKey,
						"Video detail fetching",
						missingIdError,
						ct);
					await progressReporter.SetStageAsync(
						progressStageKey,
						"Video detail fetching",
						processed,
						videos.Count,
						detail: $"Processed {processed}/{videos.Count} video detail fetch(es).",
						ct);
				}
				continue;
			}

			VideoWatchPageMetadata? directMetadata;
			if (usedYouTubeApiBatching)
			{
				batchedDirectMetadataByYoutubeId.TryGetValue(video.YoutubeVideoId, out directMetadata);
				if (!HasCompleteVideoMetadata(directMetadata))
					directMetadata = await TryGetNonApiVideoMetadataAsync(video.YoutubeVideoId, ct);
			}
			else
			{
				directMetadata = await TryGetDirectVideoMetadataAsync(db, video.YoutubeVideoId, ct);
			}

			VideoWatchPageMetadata? fallbackMetadata = null;
			if (!HasCompleteVideoMetadata(directMetadata))
			{
				_logger.LogWarning("Video hydrate direct parse failed for video {YoutubeVideoId}", video.YoutubeVideoId);
				fallbackMetadata = await TryGetFallbackVideoMetadataAsync(db, video.YoutubeVideoId, ct);
			}

			var mergedMetadata = MergeVideoMetadata(video.YoutubeVideoId, directMetadata, fallbackMetadata);
			if (mergedMetadata is null)
			{
				var unavailableError = $"Video {video.YoutubeVideoId}: Metadata unavailable.";
				if (progressReporter is not null)
				{
					await progressReporter.AddStageErrorAsync(
						progressStageKey,
						"Video detail fetching",
						unavailableError,
						ct);
				}
				processed++;
				if (progressReporter is not null)
				{
					await progressReporter.SetStageAsync(
						progressStageKey,
						"Video detail fetching",
						processed,
						videos.Count,
						detail: $"Processed {processed}/{videos.Count} video detail fetch(es).",
						ct);
				}
				continue;
			}

			ApplyVideoMetadata(video, mergedMetadata);
			pending++;
			processed++;
			if (progressReporter is not null)
			{
				await progressReporter.SetStageAsync(
					progressStageKey,
					"Video detail fetching",
					processed,
					videos.Count,
					detail: $"Processed {processed}/{videos.Count} video detail fetch(es).",
					ct);
			}

			if (pending >= 25)
			{
				await db.SaveChangesAsync(ct);
				pending = 0;
			}
		}

		if (pending > 0)
			await db.SaveChangesAsync(ct);

		if (videos.Count == 0)
		{
			if (progressReporter is not null)
			{
				await progressReporter.SetStageAsync(
					progressStageKey,
					"Video detail fetching",
					0,
					0,
					detail: "No video detail fetches were required.",
					ct);
			}
		}
	}

	async Task<(IReadOnlyDictionary<string, VideoWatchPageMetadata> MetadataByYoutubeId, bool UsedYouTubeApi)> TryGetDirectVideoMetadataBatchAsync(
		TubeArrDbContext db,
		IReadOnlyList<VideoEntity> videos,
		CancellationToken ct)
	{
		if (_youTubeDataApiMetadataService is null)
			return (new Dictionary<string, VideoWatchPageMetadata>(StringComparer.OrdinalIgnoreCase), false);

		var preference = await _youTubeDataApiMetadataService.GetPreferenceAsync(db, ct);
		if (!preference.UseYouTubeApi || !preference.IsPrioritized(YouTubeApiMetadataPriorityItems.VideoDetails))
			return (new Dictionary<string, VideoWatchPageMetadata>(StringComparer.OrdinalIgnoreCase), false);

		var result = await _youTubeDataApiMetadataService.TryGetVideoMetadataBatchAsync(
			db,
			videos.Where(v => !string.IsNullOrWhiteSpace(v.YoutubeVideoId)).Select(v => v.YoutubeVideoId),
			ct);

		return (result.MetadataByYoutubeId, true);
	}

	async Task<VideoWatchPageMetadata?> TryGetDirectVideoMetadataAsync(TubeArrDbContext db, string youtubeVideoId, CancellationToken ct)
	{
		if (_youTubeDataApiMetadataService is not null)
		{
			var preference = await _youTubeDataApiMetadataService.GetPreferenceAsync(db, ct);
			if (preference.UseYouTubeApi && preference.IsPrioritized(YouTubeApiMetadataPriorityItems.VideoDetails))
			{
				var api = await _youTubeDataApiMetadataService.TryGetVideoMetadataAsync(db, youtubeVideoId, ct);
				if (api is not null)
					return api;
			}
		}

		return await TryGetNonApiVideoMetadataAsync(youtubeVideoId, ct);
	}

	async Task<VideoWatchPageMetadata?> TryGetNonApiVideoMetadataAsync(string youtubeVideoId, CancellationToken ct)
	{

		try
		{
			return await _videoWatchPageMetadataService.GetMetadataAsync(youtubeVideoId, ct);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Video hydrate direct parse failed for video {YoutubeVideoId}", youtubeVideoId);
			return null;
		}
	}

	async Task<VideoWatchPageMetadata?> TryGetFallbackVideoMetadataAsync(TubeArrDbContext db, string youtubeVideoId, CancellationToken ct)
	{
		try
		{
			var fallback = await _videoMetadataFallbackAsync(db, youtubeVideoId, ct);
			if (fallback is null)
				_logger.LogError("Video hydrate yt-dlp fallback failed for video {YoutubeVideoId}", youtubeVideoId);
			return fallback;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Video hydrate yt-dlp fallback failed for video {YoutubeVideoId}", youtubeVideoId);
			return null;
		}
	}

	static void ApplyChannelMetadata(ChannelEntity channel, ChannelPageMetadata metadata)
	{
		channel.YoutubeChannelId = metadata.YoutubeChannelId;
		channel.Title = metadata.Title?.Trim() ?? channel.Title;
		channel.TitleSlug = SlugHelper.Slugify(channel.Title);
		channel.Description = metadata.Description;
		channel.ThumbnailUrl = metadata.ThumbnailUrl;
		channel.BannerUrl = metadata.BannerUrl;
	}

	public static async Task<(HashSet<string> NewVideoIds, int InsertedCount)> UpsertDiscoveredVideosAsync(
		TubeArrDbContext db,
		ChannelEntity channel,
		IReadOnlyList<ChannelVideoDiscoveryItem> discoveredVideos,
		CancellationToken ct)
	{
		var discoveredVideoIds = discoveredVideos
			.Where(v => !string.IsNullOrWhiteSpace(v.YoutubeVideoId))
			.Select(v => v.YoutubeVideoId)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		var existingVideos = await db.Videos
			.Where(v => v.ChannelId == channel.Id)
			.ToListAsync(ct);
		var existingByYoutubeId = existingVideos
			.Where(v => !string.IsNullOrWhiteSpace(v.YoutubeVideoId))
			.ToDictionary(v => v.YoutubeVideoId, StringComparer.OrdinalIgnoreCase);

		var conflictingVideos = discoveredVideoIds.Count == 0
			? new List<VideoEntity>()
			: await db.Videos
				.Where(v => v.ChannelId != channel.Id && discoveredVideoIds.Contains(v.YoutubeVideoId))
				.ToListAsync(ct);

		if (conflictingVideos.Count > 0)
		{
			var conflictingChannelIds = conflictingVideos
				.Select(v => v.ChannelId)
				.Distinct()
				.ToList();
			var liveChannelIds = conflictingChannelIds.Count == 0
				? new HashSet<int>()
				: (await db.Channels
					.AsNoTracking()
					.Where(c => conflictingChannelIds.Contains(c.Id))
					.Select(c => c.Id)
					.ToListAsync(ct))
				.ToHashSet();
			var orphanedVideos = conflictingVideos
				.Where(v => !liveChannelIds.Contains(v.ChannelId))
				.ToList();

			if (orphanedVideos.Count > 0)
			{
				var orphanedChannelIds = orphanedVideos
					.Select(v => v.ChannelId)
					.Distinct()
					.ToList();
				var orphanedVideoIds = orphanedVideos
					.Select(v => v.Id)
					.ToList();

				foreach (var orphanedVideo in orphanedVideos)
				{
					orphanedVideo.ChannelId = channel.Id;
					orphanedVideo.PlaylistId = null;
					existingByYoutubeId[orphanedVideo.YoutubeVideoId] = orphanedVideo;
				}

				if (orphanedVideoIds.Count > 0)
				{
					var orphanedVideoFiles = await db.VideoFiles
						.Where(vf => orphanedVideoIds.Contains(vf.VideoId))
						.ToListAsync(ct);
					foreach (var orphanedVideoFile in orphanedVideoFiles)
					{
						orphanedVideoFile.ChannelId = channel.Id;
						orphanedVideoFile.PlaylistId = null;
					}

					var orphanedQueueItems = await db.DownloadQueue
						.Where(q => orphanedVideoIds.Contains(q.VideoId))
						.ToListAsync(ct);
					foreach (var orphanedQueueItem in orphanedQueueItems)
					{
						orphanedQueueItem.ChannelId = channel.Id;
					}

					var orphanedHistoryItems = await db.DownloadHistory
						.Where(h => orphanedVideoIds.Contains(h.VideoId))
						.ToListAsync(ct);
					foreach (var orphanedHistoryItem in orphanedHistoryItems)
					{
						orphanedHistoryItem.ChannelId = channel.Id;
						orphanedHistoryItem.PlaylistId = null;
					}
				}

				if (orphanedChannelIds.Count > 0)
				{
					var orphanedPlaylists = await db.Playlists
						.Where(p => orphanedChannelIds.Contains(p.ChannelId))
						.ToListAsync(ct);
					if (orphanedPlaylists.Count > 0)
						db.Playlists.RemoveRange(orphanedPlaylists);
				}
			}
		}

		var newVideoIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var insertedCount = 0;
		var pending = 0;
		var hasUnsavedChanges = conflictingVideos.Count > 0;
		var monitoredByDefault = ShouldMonitorNewVideo(channel);

		foreach (var discoveredVideo in discoveredVideos)
		{
			if (string.IsNullOrWhiteSpace(discoveredVideo.YoutubeVideoId))
				continue;

			if (existingByYoutubeId.TryGetValue(discoveredVideo.YoutubeVideoId, out var existingVideo))
			{
				var updatedExistingVideo = false;
				if (string.IsNullOrWhiteSpace(existingVideo.Title) && !string.IsNullOrWhiteSpace(discoveredVideo.Title))
				{
					existingVideo.Title = discoveredVideo.Title!.Trim();
					updatedExistingVideo = true;
				}
				if (string.IsNullOrWhiteSpace(existingVideo.ThumbnailUrl) && !string.IsNullOrWhiteSpace(discoveredVideo.ThumbnailUrl))
				{
					existingVideo.ThumbnailUrl = discoveredVideo.ThumbnailUrl!.Trim();
					updatedExistingVideo = true;
				}
				if (string.IsNullOrWhiteSpace(existingVideo.Description) && !string.IsNullOrWhiteSpace(discoveredVideo.Description))
				{
					existingVideo.Description = discoveredVideo.Description!.Trim();
					updatedExistingVideo = true;
				}
				if (string.IsNullOrWhiteSpace(existingVideo.Overview) && !string.IsNullOrWhiteSpace(discoveredVideo.Description))
				{
					existingVideo.Overview = discoveredVideo.Description!.Trim();
					updatedExistingVideo = true;
				}
				if (IsPlaceholderDate(existingVideo.UploadDateUtc) && discoveredVideo.PublishedUtc.HasValue)
				{
					existingVideo.UploadDateUtc = discoveredVideo.PublishedUtc.Value;
					updatedExistingVideo = true;
				}
				if (IsPlaceholderDate(existingVideo.AirDateUtc) && discoveredVideo.PublishedUtc.HasValue)
				{
					existingVideo.AirDateUtc = discoveredVideo.PublishedUtc.Value;
					updatedExistingVideo = true;
				}
				if (string.IsNullOrWhiteSpace(existingVideo.AirDate) && discoveredVideo.PublishedUtc.HasValue)
				{
					existingVideo.AirDate = discoveredVideo.PublishedUtc.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
					updatedExistingVideo = true;
				}
				if (existingVideo.Runtime <= 0 && discoveredVideo.Runtime.HasValue)
				{
					existingVideo.Runtime = discoveredVideo.Runtime.Value;
					updatedExistingVideo = true;
				}

				if (updatedExistingVideo)
				{
					hasUnsavedChanges = true;
					pending++;
				}

				if (pending >= 50)
				{
					await db.SaveChangesAsync(ct);
					pending = 0;
					hasUnsavedChanges = false;
				}

				continue;
			}

			var published = discoveredVideo.PublishedUtc;
			var hasPublished = published.HasValue && published.Value != default;

			var video = new VideoEntity
			{
				ChannelId = channel.Id,
				PlaylistId = null,
				YoutubeVideoId = discoveredVideo.YoutubeVideoId,
				Title = discoveredVideo.Title?.Trim() ?? string.Empty,
				Description = string.IsNullOrWhiteSpace(discoveredVideo.Description) ? null : discoveredVideo.Description.Trim(),
				ThumbnailUrl = string.IsNullOrWhiteSpace(discoveredVideo.ThumbnailUrl) ? null : discoveredVideo.ThumbnailUrl.Trim(),
				UploadDateUtc = hasPublished ? published!.Value : PlaceholderDateUtc,
				AirDateUtc = hasPublished ? published!.Value : PlaceholderDateUtc,
				AirDate = hasPublished ? published!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty,
				Overview = string.IsNullOrWhiteSpace(discoveredVideo.Description) ? null : discoveredVideo.Description.Trim(),
				Runtime = discoveredVideo.Runtime ?? 0,
				Monitored = monitoredByDefault,
				Added = DateTimeOffset.UtcNow
			};

			db.Videos.Add(video);
			existingByYoutubeId[video.YoutubeVideoId] = video;
			newVideoIds.Add(video.YoutubeVideoId);
			insertedCount++;
			pending++;
			hasUnsavedChanges = true;

			if (pending >= 50)
			{
				await db.SaveChangesAsync(ct);
				pending = 0;
				hasUnsavedChanges = false;
			}
		}

		if (pending > 0 || hasUnsavedChanges)
			await db.SaveChangesAsync(ct);

		return (newVideoIds, insertedCount);
	}

	static bool ShouldMonitorNewVideo(ChannelEntity channel)
	{
		if (!channel.Monitored)
			return false;

		if (channel.MonitorNewItems.HasValue && channel.MonitorNewItems.Value == 0)
			return false;

		return true;
	}

	static bool NeedsHydrate(VideoEntity video, bool requireRuntime = true)
	{
		return string.IsNullOrWhiteSpace(video.Title) ||
			string.IsNullOrWhiteSpace(video.Description) ||
			string.IsNullOrWhiteSpace(video.ThumbnailUrl) ||
			IsPlaceholderDate(video.UploadDateUtc) ||
			IsPlaceholderDate(video.AirDateUtc) ||
			string.IsNullOrWhiteSpace(video.AirDate) ||
			string.IsNullOrWhiteSpace(video.Overview) ||
			(requireRuntime && video.Runtime <= 0);
	}

	static IReadOnlyList<ChannelVideoDiscoveryItem> MergeDiscoveredVideos(
		IReadOnlyList<ChannelVideoDiscoveryItem> directDiscoveredVideos,
		IReadOnlyList<ChannelVideoDiscoveryItem> fallbackDiscoveredVideos)
	{
		var merged = new Dictionary<string, ChannelVideoDiscoveryItem>(StringComparer.OrdinalIgnoreCase);

		foreach (var video in directDiscoveredVideos)
		{
			if (!string.IsNullOrWhiteSpace(video.YoutubeVideoId))
				merged[video.YoutubeVideoId] = video;
		}

		foreach (var video in fallbackDiscoveredVideos)
		{
			if (string.IsNullOrWhiteSpace(video.YoutubeVideoId))
				continue;

			if (merged.TryGetValue(video.YoutubeVideoId, out var existing))
			{
				merged[video.YoutubeVideoId] = new ChannelVideoDiscoveryItem(
					YoutubeVideoId: existing.YoutubeVideoId,
					Title: existing.Title ?? video.Title,
					ThumbnailUrl: existing.ThumbnailUrl ?? video.ThumbnailUrl,
					Runtime: existing.Runtime ?? video.Runtime,
					PublishedUtc: existing.PublishedUtc ?? video.PublishedUtc,
					Description: existing.Description ?? video.Description);
				continue;
			}

			merged[video.YoutubeVideoId] = video;
		}

		return merged.Values.ToList();
	}

	static bool HasCompleteChannelMetadata(ChannelPageMetadata? metadata)
	{
		return metadata is not null &&
			ChannelResolveHelper.LooksLikeYouTubeChannelId(metadata.YoutubeChannelId) &&
			!string.IsNullOrWhiteSpace(metadata.Title) &&
			!string.IsNullOrWhiteSpace(metadata.Description) &&
			!string.IsNullOrWhiteSpace(metadata.ThumbnailUrl) &&
			!string.IsNullOrWhiteSpace(metadata.BannerUrl);
	}

	static bool HasCompleteVideoMetadata(VideoWatchPageMetadata? metadata)
	{
		return metadata is not null &&
			!string.IsNullOrWhiteSpace(metadata.Title) &&
			!string.IsNullOrWhiteSpace(metadata.Description) &&
			!string.IsNullOrWhiteSpace(metadata.ThumbnailUrl) &&
			metadata.UploadDateUtc.HasValue &&
			metadata.AirDateUtc.HasValue &&
			!string.IsNullOrWhiteSpace(metadata.AirDate) &&
			!string.IsNullOrWhiteSpace(metadata.Overview) &&
			metadata.Runtime.HasValue;
	}

	static ChannelPageMetadata? MergeChannelMetadata(
		string youtubeChannelId,
		ChannelPageMetadata? directMetadata,
		ChannelPageMetadata? fallbackMetadata)
	{
		var mergedYoutubeChannelId =
			directMetadata?.YoutubeChannelId
			?? fallbackMetadata?.YoutubeChannelId
			?? youtubeChannelId;

		if (!ChannelResolveHelper.LooksLikeYouTubeChannelId(mergedYoutubeChannelId))
			return null;

		var title = directMetadata?.Title ?? fallbackMetadata?.Title;
		var description = directMetadata?.Description ?? fallbackMetadata?.Description;
		var thumbnailUrl = directMetadata?.ThumbnailUrl ?? fallbackMetadata?.ThumbnailUrl;
		var bannerUrl = directMetadata?.BannerUrl ?? fallbackMetadata?.BannerUrl;

		return new ChannelPageMetadata(
			YoutubeChannelId: mergedYoutubeChannelId,
			Title: title,
			Description: description,
			ThumbnailUrl: thumbnailUrl,
			BannerUrl: bannerUrl,
			CanonicalUrl: $"https://www.youtube.com/channel/{mergedYoutubeChannelId}");
	}

	static VideoWatchPageMetadata? MergeVideoMetadata(
		string youtubeVideoId,
		VideoWatchPageMetadata? directMetadata,
		VideoWatchPageMetadata? fallbackMetadata)
	{
		var title = directMetadata?.Title ?? fallbackMetadata?.Title;
		var description = directMetadata?.Description ?? fallbackMetadata?.Description;
		var thumbnailUrl = directMetadata?.ThumbnailUrl ?? fallbackMetadata?.ThumbnailUrl;
		var uploadDateUtc = directMetadata?.UploadDateUtc ?? fallbackMetadata?.UploadDateUtc;
		var airDateUtc = directMetadata?.AirDateUtc ?? fallbackMetadata?.AirDateUtc ?? uploadDateUtc;
		var airDate = directMetadata?.AirDate ?? fallbackMetadata?.AirDate ?? airDateUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		var overview = directMetadata?.Overview ?? fallbackMetadata?.Overview ?? description;
		var runtime = directMetadata?.Runtime ?? fallbackMetadata?.Runtime;

		if (string.IsNullOrWhiteSpace(title) &&
			string.IsNullOrWhiteSpace(description) &&
			string.IsNullOrWhiteSpace(thumbnailUrl) &&
			!uploadDateUtc.HasValue &&
			!runtime.HasValue)
		{
			return null;
		}

		return new VideoWatchPageMetadata(
			YoutubeVideoId: youtubeVideoId,
			Title: title,
			Description: description,
			ThumbnailUrl: thumbnailUrl,
			UploadDateUtc: uploadDateUtc,
			AirDateUtc: airDateUtc,
			AirDate: airDate,
			Overview: overview,
			Runtime: runtime);
	}

	static void ApplyVideoMetadata(VideoEntity video, VideoWatchPageMetadata metadata)
	{
		if (!string.IsNullOrWhiteSpace(metadata.Title))
			video.Title = metadata.Title.Trim();
		if (metadata.Description is not null)
			video.Description = metadata.Description;
		if (!string.IsNullOrWhiteSpace(metadata.ThumbnailUrl))
			video.ThumbnailUrl = metadata.ThumbnailUrl.Trim();
		if (metadata.UploadDateUtc.HasValue)
			video.UploadDateUtc = metadata.UploadDateUtc.Value;
		if (metadata.AirDateUtc.HasValue)
			video.AirDateUtc = metadata.AirDateUtc.Value;
		if (metadata.AirDate is not null)
			video.AirDate = metadata.AirDate;
		if (metadata.Overview is not null)
			video.Overview = metadata.Overview;
		if (metadata.Runtime.HasValue)
			video.Runtime = metadata.Runtime.Value;
	}

	static bool IsPlaceholderDate(DateTimeOffset value)
	{
		return value == default || value == PlaceholderDateUtc;
	}

	static async Task<ChannelPageMetadata?> DefaultChannelMetadataFallbackAsync(
		TubeArrDbContext db,
		string youtubeChannelId,
		CancellationToken ct)
	{
		var executablePath = await YtDlpMetadataService.GetExecutablePathAsync(db, ct);
		if (string.IsNullOrWhiteSpace(executablePath))
			return null;

		var metadata = await YtDlpChannelLookupService.EnrichChannelForCreateAsync(executablePath, youtubeChannelId, ct);
		if (!metadata.HasValue)
			return null;

		var (title, description, thumbnailUrl, _, _) = metadata.Value;
		return new ChannelPageMetadata(
			YoutubeChannelId: youtubeChannelId,
			Title: title,
			Description: description,
			ThumbnailUrl: thumbnailUrl,
			BannerUrl: null,
			CanonicalUrl: $"https://www.youtube.com/channel/{youtubeChannelId}");
	}

	static async Task<IReadOnlyList<ChannelVideoDiscoveryItem>> DefaultVideoDiscoveryFallbackAsync(
		TubeArrDbContext db,
		string youtubeChannelId,
		CancellationToken ct)
	{
		var executablePath = await YtDlpMetadataService.GetExecutablePathAsync(db, ct);
		if (string.IsNullOrWhiteSpace(executablePath))
			return Array.Empty<ChannelVideoDiscoveryItem>();

		var docs = await YtDlpMetadataService.RunYtDlpJsonAsync(
			executablePath,
			ChannelResolveHelper.GetCanonicalChannelVideosUrl(youtubeChannelId),
			ct,
			playlistItems: null,
			timeoutMs: 120_000,
			flatPlaylist: true);

		var items = new List<ChannelVideoDiscoveryItem>();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			foreach (var doc in docs)
			{
				CollectYtDlpDiscoveryItems(doc.RootElement, items, seen);
			}
		}
		finally
		{
			foreach (var doc in docs)
				doc.Dispose();
		}

		return items;
	}

	static async Task<VideoWatchPageMetadata?> DefaultVideoMetadataFallbackAsync(
		TubeArrDbContext db,
		string youtubeVideoId,
		CancellationToken ct)
	{
		var executablePath = await YtDlpMetadataService.GetExecutablePathAsync(db, ct);
		if (string.IsNullOrWhiteSpace(executablePath))
			return null;

		var docs = await YtDlpMetadataService.RunYtDlpJsonAsync(
			executablePath,
			$"https://www.youtube.com/watch?v={youtubeVideoId}",
			ct,
			playlistItems: null,
			timeoutMs: 60_000,
			flatPlaylist: false);

		try
		{
			if (docs.Count == 0)
				return null;

			return ParseYtDlpVideoMetadata(docs[0].RootElement, youtubeVideoId);
		}
		finally
		{
			foreach (var doc in docs)
				doc.Dispose();
		}
	}

	static void CollectYtDlpDiscoveryItems(JsonElement element, List<ChannelVideoDiscoveryItem> items, HashSet<string> seen)
	{
		if (TryCreateYtDlpDiscoveryItem(element, out var item) && seen.Add(item.YoutubeVideoId))
			items.Add(item);

		if (element.ValueKind == JsonValueKind.Object)
		{
			if (element.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
			{
				foreach (var entry in entries.EnumerateArray())
					CollectYtDlpDiscoveryItems(entry, items, seen);
			}
		}
	}

	static bool TryCreateYtDlpDiscoveryItem(JsonElement element, out ChannelVideoDiscoveryItem item)
	{
		item = default!;
		var youtubeVideoId = GetYtDlpVideoId(element);
		if (string.IsNullOrWhiteSpace(youtubeVideoId))
			return false;

		var title = GetYtDlpString(element, "title") ?? GetYtDlpString(element, "fulltitle");
		var thumbnailUrl = GetYtDlpString(element, "thumbnail") ?? GetYtDlpThumbnail(element);
		var runtime = GetYtDlpInt(element, "duration");

		item = new ChannelVideoDiscoveryItem(
			YoutubeVideoId: youtubeVideoId,
			Title: title,
			ThumbnailUrl: thumbnailUrl,
			Runtime: runtime);
		return true;
	}

	static void CollectYtDlpVideoMetadata(JsonElement element, Dictionary<string, VideoWatchPageMetadata> items)
	{
		if (TryCreateYtDlpVideoMetadata(element, out var item))
			items[item.YoutubeVideoId] = item;

		if (element.ValueKind == JsonValueKind.Object)
		{
			if (element.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
			{
				foreach (var entry in entries.EnumerateArray())
					CollectYtDlpVideoMetadata(entry, items);
			}
		}
	}

	static bool TryCreateYtDlpVideoMetadata(JsonElement element, out VideoWatchPageMetadata item)
	{
		item = default!;
		var youtubeVideoId = GetYtDlpVideoId(element);
		if (string.IsNullOrWhiteSpace(youtubeVideoId))
			return false;

		var metadata = ParseYtDlpVideoMetadata(element, youtubeVideoId);
		if (string.IsNullOrWhiteSpace(metadata.Title) &&
			string.IsNullOrWhiteSpace(metadata.Description) &&
			string.IsNullOrWhiteSpace(metadata.ThumbnailUrl) &&
			!metadata.UploadDateUtc.HasValue &&
			!metadata.Runtime.HasValue)
		{
			return false;
		}

		item = metadata;
		return true;
	}

	static VideoWatchPageMetadata ParseYtDlpVideoMetadata(JsonElement element, string youtubeVideoId)
	{
		var title = GetYtDlpString(element, "title") ?? GetYtDlpString(element, "fulltitle");
		var description = GetYtDlpString(element, "description");
		var thumbnailUrl = GetYtDlpString(element, "thumbnail") ?? GetYtDlpThumbnail(element);
		var uploadDateUtc = ParseYtDlpUploadDate(element);
		var airDateUtc = uploadDateUtc;
		var airDate = airDateUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		var runtime = GetYtDlpInt(element, "duration");

		return new VideoWatchPageMetadata(
			YoutubeVideoId: youtubeVideoId,
			Title: title,
			Description: description,
			ThumbnailUrl: thumbnailUrl,
			UploadDateUtc: uploadDateUtc,
			AirDateUtc: airDateUtc,
			AirDate: airDate,
			Overview: description,
			Runtime: runtime);
	}

	static string? GetYtDlpVideoId(JsonElement element)
	{
		return GetYtDlpString(element, "id")
			?? GetYtDlpString(element, "video_id")
			?? GetYtDlpString(element, "display_id");
	}

	static string? GetYtDlpString(JsonElement element, string propertyName)
	{
		if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
			return null;

		var value = property.GetString()?.Trim();
		return string.IsNullOrWhiteSpace(value) ? null : value;
	}

	static int? GetYtDlpInt(JsonElement element, string propertyName)
	{
		if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number)
			return null;

		return property.TryGetInt32(out var parsed) ? parsed : null;
	}

	static string? GetYtDlpThumbnail(JsonElement element)
	{
		if (!element.TryGetProperty("thumbnails", out var thumbnails) || thumbnails.ValueKind != JsonValueKind.Array)
			return null;

		string? value = null;
		foreach (var thumbnail in thumbnails.EnumerateArray())
		{
			var candidate = GetYtDlpString(thumbnail, "url");
			if (!string.IsNullOrWhiteSpace(candidate))
				value = candidate;
		}

		return value;
	}

	static DateTimeOffset? ParseYtDlpUploadDate(JsonElement element)
	{
		var uploadDate = GetYtDlpString(element, "upload_date") ?? GetYtDlpString(element, "release_date");
		if (!string.IsNullOrWhiteSpace(uploadDate) &&
			DateTimeOffset.TryParseExact(
				uploadDate,
				"yyyyMMdd",
				CultureInfo.InvariantCulture,
				DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
				out var parsed))
		{
			return parsed;
		}

		if (element.TryGetProperty("timestamp", out var timestampElement) &&
			timestampElement.ValueKind == JsonValueKind.Number &&
			timestampElement.TryGetInt64(out var timestamp))
		{
			return DateTimeOffset.FromUnixTimeSeconds(timestamp);
		}

		return null;
	}
}
