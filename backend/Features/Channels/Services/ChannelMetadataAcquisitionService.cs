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
		CancellationToken ct = default)
	{
		var err = await RunUploadsPopulationPhaseAsync(db, channelId, ct);
		if (err is not null)
			return err;
		err = await RunHydrationPhaseAsync(db, channelId, ct);
		if (err is not null)
			return err;
		return await RunShortsParsingPhaseAsync(db, channelId, ct);
	}

	/// <summary>Channel page metadata, discovery, and upsert into the database (uploads population).</summary>
	public async Task<string?> RunUploadsPopulationPhaseAsync(
		TubeArrDbContext db,
		int channelId,
		CancellationToken ct = default,
		Func<string, CancellationToken, Task>? onPhaseDetail = null,
		Func<int, int, CancellationToken, Task>? onPersistProgress = null,
		Func<string, Task>? reportAcquisitionMethod = null)
	{
		var channel = await db.Channels.FirstOrDefaultAsync(c => c.Id == channelId, ct);
		if (channel is null)
			return null;

		if (!ChannelResolveHelper.LooksLikeYouTubeChannelId(channel.YoutubeChannelId))
			return "Channel has no valid YouTube channel ID; cannot fetch metadata.";

		var (directChannelMetadata, usedYouTubeApiForChannelMeta) = await TryGetDirectChannelMetadataAsync(db, channel.YoutubeChannelId, ct);
		if (reportAcquisitionMethod is not null && directChannelMetadata is not null)
		{
			await reportAcquisitionMethod(
				usedYouTubeApiForChannelMeta ? AcquisitionMethodIds.YouTubeDataApi : AcquisitionMethodIds.Internal);
		}

		ChannelPageMetadata? fallbackChannelMetadata = null;
		if (!HasCompleteChannelMetadata(directChannelMetadata))
		{
			_logger.LogWarning("Channel metadata direct parse failed for {YoutubeChannelId}", channel.YoutubeChannelId);
			fallbackChannelMetadata = await TryGetFallbackChannelMetadataAsync(db, channel.YoutubeChannelId, ct);
		}

		var mergedChannelMetadata = MergeChannelMetadata(channel.YoutubeChannelId, directChannelMetadata, fallbackChannelMetadata);
		if (mergedChannelMetadata is null || string.IsNullOrWhiteSpace(mergedChannelMetadata.Title))
			return $"Channel metadata extraction failed for {channel.YoutubeChannelId}.";

		ApplyChannelMetadata(channel, mergedChannelMetadata);
		await db.SaveChangesAsync(ct);

		if (reportAcquisitionMethod is not null && fallbackChannelMetadata is not null)
			await reportAcquisitionMethod(AcquisitionMethodIds.YtDlp);

		var (directDiscoveredVideos, usedYouTubeApiForListing) = await TryDiscoverVideosDirectAsync(db, channel.YoutubeChannelId, ct);
		if (directDiscoveredVideos.Count == 0)
			_logger.LogWarning("Video discovery direct parse failed for channel {YoutubeChannelId}", channel.YoutubeChannelId);

		if (reportAcquisitionMethod is not null && directDiscoveredVideos.Count > 0)
		{
			if (usedYouTubeApiForListing)
				await reportAcquisitionMethod(AcquisitionMethodIds.YouTubeDataApi);
			else
				await reportAcquisitionMethod(AcquisitionMethodIds.Internal);
		}

		var fallbackDiscoveredVideos = await TryDiscoverVideosFallbackAsync(
			db,
			channel.YoutubeChannelId,
			ct,
			required: directDiscoveredVideos.Count == 0);
		var discoveredVideos = MergeDiscoveredVideos(directDiscoveredVideos, fallbackDiscoveredVideos);

		if (reportAcquisitionMethod is not null && fallbackDiscoveredVideos.Count > 0)
			await reportAcquisitionMethod(AcquisitionMethodIds.YtDlp);

		if (discoveredVideos.Count == 0)
			return $"Video discovery failed for channel {channel.YoutubeChannelId}.";

		_logger.LogInformation(
			"Persisting {VideoCount} discovered videos to database for channel {ChannelId}",
			discoveredVideos.Count,
			channel.Id);

		if (onPhaseDetail is not null)
			await onPhaseDetail($"Saving {discoveredVideos.Count} videos to the library…", ct);

		await UpsertDiscoveredVideosAsync(
			db,
			channel,
			discoveredVideos,
			ct,
			onPhaseDetail,
			onPersistProgress);
		return null;
	}

	/// <summary>Fill video rows from channel-level and per-video metadata sources.</summary>
	public async Task<string?> RunHydrationPhaseAsync(
		TubeArrDbContext db,
		int channelId,
		CancellationToken ct = default,
		Func<string, Task>? reportAcquisitionMethod = null)
	{
		var channel = await db.Channels.FirstOrDefaultAsync(c => c.Id == channelId, ct);
		if (channel is null)
			return null;

		_logger.LogInformation(
			"Metadata hydration phase started for channel {ChannelId} ({YoutubeChannelId}).",
			channel.Id,
			channel.YoutubeChannelId);

		await HydrateVideosFromChannelFallbackAsync(db, channel, ct, reportAcquisitionMethod);
		var hydrateCandidates = await db.Videos
			.Where(v => v.ChannelId == channelId)
			.OrderBy(v => v.Id)
			.ToListAsync(ct);

		var hydrateTargets = hydrateCandidates
			.Where(v => NeedsHydrate(v))
			.ToList();

		if (hydrateTargets.Count == 0)
		{
			_logger.LogInformation(
				"Metadata hydration: no videos need detail metadata for channel {ChannelId} ({YoutubeChannelId}); phase complete.",
				channel.Id,
				channel.YoutubeChannelId);
			return null;
		}

		_logger.LogInformation(
			"Metadata hydration: fetching watch/API details for {VideoCount} video(s) on channel {ChannelId}. Reasons: {Reasons}.",
			hydrateTargets.Count,
			channel.Id,
			FormatHydrationReasonSummary(hydrateTargets));

		await HydrateVideosAsync(db, channel, hydrateTargets, null, "videoDetailFetching", ct, reportAcquisitionMethod);
		return null;
	}

	/// <summary>Shorts tab classification and monitoring round-robin.</summary>
	public async Task<string?> RunShortsParsingPhaseAsync(
		TubeArrDbContext db,
		int channelId,
		CancellationToken ct = default,
		Func<string, Task>? reportAcquisitionMethod = null)
	{
		await ApplyShortsTabFlagsAndFilterAsync(db, channelId, ct);
		await RoundRobinMonitoringHelper.ApplyForChannelAsync(db, channelId, ct);
		if (reportAcquisitionMethod is not null)
			await reportAcquisitionMethod(AcquisitionMethodIds.Internal);
		return null;
	}

	public async Task<string?> PopulateVideoMetadataAsync(
		TubeArrDbContext db,
		int channelId,
		MetadataProgressReporter? progressReporter = null,
		CancellationToken ct = default,
		Func<string, Task>? reportAcquisitionMethod = null)
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

			await RunFfProbeForChannelMetadataAsync(db, channelId, progressReporter, ct);
			await ApplyShortsTabFlagsAndFilterAsync(db, channelId, ct);
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

		_logger.LogInformation(
			"Populate video metadata: queued {VideoCount} video(s) for channel {ChannelId} ({YoutubeChannelId}). Reasons: {Reasons}.",
			hydrateTargets.Count,
			channel.Id,
			channel.YoutubeChannelId,
			FormatHydrationReasonSummary(hydrateTargets));

		await HydrateVideosAsync(db, channel, hydrateTargets, progressReporter, "videoDetailFetching", ct, reportAcquisitionMethod);
		await RunFfProbeForChannelMetadataAsync(db, channelId, progressReporter, ct);
		await ApplyShortsTabFlagsAndFilterAsync(db, channelId, ct);
		await RoundRobinMonitoringHelper.ApplyForChannelAsync(db, channelId, ct);
		return $"Updated metadata for {hydrateTargets.Count} video(s).";
	}

	async Task RunFfProbeForChannelMetadataAsync(
		TubeArrDbContext db,
		int channelId,
		MetadataProgressReporter? progressReporter,
		CancellationToken ct)
	{
		if (progressReporter is not null)
		{
			await VideoFileFfProbeEnricher.RunAsync(
				db,
				_logger,
				ct,
				reportProgress: null,
				channelId: channelId,
				reportFileProgress: async (completed, total, fileName) =>
				{
					await progressReporter.SetStageAsync(
						"ffprobe",
						"Media file probing",
						completed,
						total,
						detail: completed == 0
							? $"Queued {total} file(s) for ffprobe."
							: $"{completed}/{total}: {fileName}",
						ct);
				});
		}
		else
			await VideoFileFfProbeEnricher.RunAsync(db, _logger, ct, channelId: channelId);
	}

	async Task<(ChannelPageMetadata? Metadata, bool UsedYouTubeDataApi)> TryGetDirectChannelMetadataAsync(
		TubeArrDbContext db,
		string youtubeChannelId,
		CancellationToken ct)
	{
		if (_youTubeDataApiMetadataService is not null)
		{
			var preference = await _youTubeDataApiMetadataService.GetPreferenceAsync(db, ct);
			if (preference.UseYouTubeApi && preference.IsPrioritized(YouTubeApiMetadataPriorityItems.ChannelMetadata))
			{
				var api = await _youTubeDataApiMetadataService.TryGetChannelMetadataAsync(db, youtubeChannelId, ct);
				if (api is not null)
					return (api, true);
			}
		}

		try
		{
			var html = await _channelPageMetadataService.GetMetadataByYoutubeChannelIdAsync(youtubeChannelId, ct);
			return (html, false);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Channel metadata direct parse failed for {YoutubeChannelId}", youtubeChannelId);
			return (null, false);
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

	async Task HydrateVideosFromChannelFallbackAsync(
		TubeArrDbContext db,
		ChannelEntity channel,
		CancellationToken ct,
		Func<string, Task>? reportAcquisitionMethod = null)
	{
		var pendingVideos = (await db.Videos
			.Where(v => v.ChannelId == channel.Id)
			.OrderBy(v => v.Id)
			.ToListAsync(ct))
			.Where(v => NeedsHydrate(v))
			.ToList();
		if (pendingVideos.Count == 0)
			return;

		var executablePath = await YtDlpMetadataService.GetExecutablePathAsync(db, ct);
		if (string.IsNullOrWhiteSpace(executablePath))
			return;

		var cookiesPath = await YtDlpMetadataService.GetCookiesPathAsync(db, ct);

		_logger.LogInformation(
			"Metadata hydration: fetching channel video listing from yt-dlp for {VideoCount} pending video(s) on channel {ChannelId} ({YoutubeChannelId}). Reasons: {Reasons}.",
			pendingVideos.Count,
			channel.Id,
			channel.YoutubeChannelId,
			FormatHydrationReasonSummary(pendingVideos));

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
			timeoutMs: 600_000,
			flatPlaylist: false,
			cookiesPath: cookiesPath);

		_logger.LogInformation(
			"Metadata hydration: yt-dlp channel listing returned {DocCount} JSON chunk(s) for channel {ChannelId} ({YoutubeChannelId}); applying to pending rows.",
			docs.Count,
			channel.Id,
			channel.YoutubeChannelId);

		var pending = 0;
		var reportedYtDlp = false;
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

					if (reportAcquisitionMethod is not null && !reportedYtDlp)
					{
						await reportAcquisitionMethod(AcquisitionMethodIds.YtDlp);
						reportedYtDlp = true;
					}

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

		_logger.LogInformation(
			"Metadata hydration: channel playlist pass finished for channel {ChannelId} ({YoutubeChannelId}).",
			channel.Id,
			channel.YoutubeChannelId);
	}

	/// <summary>
	/// Second pass: resolve Shorts by channel /shorts listing (paginated like /videos).
	/// Sets <see cref="VideoEntity.IsShort"/> for any video id found there.
	/// When <see cref="ChannelEntity.FilterOutShorts"/> is true (and the channel has a Shorts tab), unmonitors those videos.
	/// </summary>
	async Task ApplyShortsTabFlagsAndFilterAsync(TubeArrDbContext db, int channelId, CancellationToken ct)
	{
		var channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == channelId, ct);
		if (channel is null || !ChannelResolveHelper.LooksLikeYouTubeChannelId(channel.YoutubeChannelId))
			return;
		if (channel.HasShortsTab != true)
			return;

		var shortsListed = await _channelVideoDiscoveryService.DiscoverShortsAsync(channel.YoutubeChannelId, ct);
		if (shortsListed.Count == 0)
		{
			_logger.LogWarning(
				"Shorts tab returned no video ids for channel {ChannelId} ({YoutubeChannelId}). " +
				"May be network/blocking, or /shorts mirroring /videos (no Shorts tab); IsShort may stay false until watch-page hydrate.",
				channelId,
				channel.YoutubeChannelId);
			return;
		}

		var shortIds = new HashSet<string>(
			shortsListed.Select(i => i.YoutubeVideoId),
			StringComparer.OrdinalIgnoreCase);
		var videos = await db.Videos.Where(v => v.ChannelId == channelId).ToListAsync(ct);
		var tagged = 0;
		var unmonitored = 0;
		foreach (var v in videos)
		{
			if (string.IsNullOrWhiteSpace(v.YoutubeVideoId) || !shortIds.Contains(v.YoutubeVideoId))
				continue;
			if (!v.IsShort)
			{
				v.IsShort = true;
				tagged++;
			}
			if (channel.FilterOutShorts && v.Monitored)
			{
				v.Monitored = false;
				unmonitored++;
			}
		}

		if (tagged > 0 || unmonitored > 0)
		{
			if (unmonitored > 0)
			{
				_logger.LogInformation(
					"FilterOutShorts: unmonitored {Count} video(s) on the Shorts tab for channel {ChannelId}",
					unmonitored,
					channelId);
			}
			await db.SaveChangesAsync(ct);
		}
	}

	async Task HydrateVideosAsync(
		TubeArrDbContext db,
		ChannelEntity channel,
		IReadOnlyList<VideoEntity> videos,
		MetadataProgressReporter? progressReporter,
		string progressStageKey,
		CancellationToken ct,
		Func<string, Task>? reportAcquisitionMethod = null)
	{
		var methodsUsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var (batchedDirectMetadataByYoutubeId, usedYouTubeApiBatching) = await TryGetDirectVideoMetadataBatchAsync(db, videos, ct);
		YouTubeApiMetadataPreference? youtubePreference = null;
		if (_youTubeDataApiMetadataService is not null)
			youtubePreference = await _youTubeDataApiMetadataService.GetPreferenceAsync(db, ct);

		var pending = 0;
		var processed = 0;

		void LogDetailProgressIfDue()
		{
			if (processed == 0 || videos.Count == 0)
				return;
			if (processed % 25 != 0 && processed != videos.Count)
				return;
			_logger.LogInformation(
				"Metadata hydration: processed {Processed} of {Total} video detail fetch(es) for channel {ChannelId} ({YoutubeChannelId}).",
				processed,
				videos.Count,
				channel.Id,
				channel.YoutubeChannelId);
		}

		foreach (var video in videos)
		{
			ct.ThrowIfCancellationRequested();
			if (string.IsNullOrWhiteSpace(video.YoutubeVideoId))
			{
				processed++;
				LogDetailProgressIfDue();
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

			var preferApiLivestreamIdentification =
				youtubePreference is { UseYouTubeApi: true } &&
				youtubePreference.IsPrioritized(YouTubeApiMetadataPriorityItems.LivestreamIdentification);

			var mergedMetadata = MergeVideoMetadata(
				video.YoutubeVideoId,
				directMetadata,
				fallbackMetadata,
				preferApiLivestreamIdentification);
			if (!preferApiLivestreamIdentification &&
				mergedMetadata is not null &&
				mergedMetadata.IsLivestream is null &&
				fallbackMetadata is null)
			{
				// Direct/API metadata can be complete while still omitting historical livestream state.
				// In yt-dlp-first mode, do a fallback pass to capture was_live/live_status when possible.
				fallbackMetadata = await TryGetFallbackVideoMetadataAsync(db, video.YoutubeVideoId, ct);
				mergedMetadata = MergeVideoMetadata(
					video.YoutubeVideoId,
					directMetadata,
					fallbackMetadata,
					preferApiLivestreamIdentification);
			}
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
				LogDetailProgressIfDue();
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

			if (reportAcquisitionMethod is not null)
			{
				if (usedYouTubeApiBatching &&
				    batchedDirectMetadataByYoutubeId.TryGetValue(video.YoutubeVideoId, out var batchMeta) &&
				    HasCompleteVideoMetadata(batchMeta))
					methodsUsed.Add(AcquisitionMethodIds.YouTubeDataApi);
				else if (HasCompleteVideoMetadata(directMetadata))
					methodsUsed.Add(AcquisitionMethodIds.Internal);
				if (fallbackMetadata is not null)
					methodsUsed.Add(AcquisitionMethodIds.YtDlp);
			}

			if (((channel.FilterOutShorts && channel.HasShortsTab == true) || channel.FilterOutLivestreams) &&
			    mergedMetadata.IsShort is null &&
			    youtubePreference is { UseYouTubeApi: true } &&
			    youtubePreference.IsPrioritized(YouTubeApiMetadataPriorityItems.VideoDetails) &&
			    HasCompleteVideoMetadata(mergedMetadata))
			{
				var watchIsShort = await TryGetNonApiVideoMetadataAsync(video.YoutubeVideoId, ct);
				if (watchIsShort is not null)
				{
					mergedMetadata = mergedMetadata with
					{
						IsShort = watchIsShort.IsShort ?? mergedMetadata.IsShort,
						IsLivestream = watchIsShort.IsLivestream ?? mergedMetadata.IsLivestream
					};
				}
			}

			ApplyVideoMetadata(video, mergedMetadata);
			if (channel.FilterOutShorts && channel.HasShortsTab == true && mergedMetadata.IsShort == true)
				video.Monitored = false;
			if (channel.FilterOutLivestreams && mergedMetadata.IsLivestream == true)
				video.Monitored = false;
			pending++;
			processed++;
			LogDetailProgressIfDue();
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

		if (reportAcquisitionMethod is not null)
		{
			foreach (var m in methodsUsed)
				await reportAcquisitionMethod(m);
		}

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
		else
		{
			_logger.LogInformation(
				"Metadata hydration: video detail pass finished for channel {ChannelId} ({YoutubeChannelId}): {Processed} of {Total} video(s) processed.{BatchHint}",
				channel.Id,
				channel.YoutubeChannelId,
				processed,
				videos.Count,
				usedYouTubeApiBatching ? " YouTube Data API batching was used for eligible requests." : "");
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
		if (metadata.HasShortsTab == true)
			channel.HasShortsTab = true;
	}

	/// <summary>
	/// Inserts or updates videos from discovery (RSS, channel page, fallbacks).
	/// When <see cref="ChannelEntity.FilterOutShorts"/> is true and <see cref="ChannelEntity.HasShortsTab"/> is true, fetches the channel /shorts listing once
	/// before inserting new rows so Shorts are not monitored (and download queue will not pick them up).
	/// </summary>
	public async Task<(HashSet<string> NewVideoIds, int InsertedCount)> UpsertDiscoveredVideosAsync(
		TubeArrDbContext db,
		ChannelEntity channel,
		IReadOnlyList<ChannelVideoDiscoveryItem> discoveredVideos,
		CancellationToken ct,
		Func<string, CancellationToken, Task>? onPhaseDetail = null,
		Func<int, int, CancellationToken, Task>? onPersistProgress = null)
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

		var shortIdsOnChannelTab = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (channel.FilterOutShorts &&
		    channel.HasShortsTab == true &&
		    ChannelResolveHelper.LooksLikeYouTubeChannelId(channel.YoutubeChannelId) &&
		    discoveredVideos.Any(d =>
			    !string.IsNullOrWhiteSpace(d.YoutubeVideoId) &&
			    !existingByYoutubeId.ContainsKey(d.YoutubeVideoId)))
		{
			if (onPhaseDetail is not null)
				await onPhaseDetail("Loading Shorts tab to filter new uploads…", ct);
			try
			{
				var shortsListed = await _channelVideoDiscoveryService.DiscoverShortsAsync(channel.YoutubeChannelId, ct);
				foreach (var s in shortsListed)
				{
					if (!string.IsNullOrWhiteSpace(s.YoutubeVideoId))
						shortIdsOnChannelTab.Add(s.YoutubeVideoId);
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Shorts tab fetch failed during video upsert for channel {ChannelId}", channel.Id);
			}
		}

		var persistProgressTotal = discoveredVideos.Count(v => !string.IsNullOrWhiteSpace(v.YoutubeVideoId));
		var persistProgressHandled = 0;

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

				persistProgressHandled++;
				if (onPersistProgress is not null &&
				    persistProgressTotal > 0 &&
				    (persistProgressHandled % 400 == 0 || persistProgressHandled == persistProgressTotal))
					await onPersistProgress(persistProgressHandled, persistProgressTotal, ct);

				continue;
			}

			var published = discoveredVideo.PublishedUtc;
			var hasPublished = published.HasValue && published.Value != default;

			var isOnShortsTab = shortIdsOnChannelTab.Count > 0 &&
			                     shortIdsOnChannelTab.Contains(discoveredVideo.YoutubeVideoId);
			var monitorNew = monitoredByDefault && !(channel.FilterOutShorts && channel.HasShortsTab == true && isOnShortsTab);

			var video = new VideoEntity
			{
				ChannelId = channel.Id,
				YoutubeVideoId = discoveredVideo.YoutubeVideoId,
				Title = discoveredVideo.Title?.Trim() ?? string.Empty,
				Description = string.IsNullOrWhiteSpace(discoveredVideo.Description) ? null : discoveredVideo.Description.Trim(),
				ThumbnailUrl = string.IsNullOrWhiteSpace(discoveredVideo.ThumbnailUrl) ? null : discoveredVideo.ThumbnailUrl.Trim(),
				UploadDateUtc = hasPublished ? published!.Value : PlaceholderDateUtc,
				AirDateUtc = hasPublished ? published!.Value : PlaceholderDateUtc,
				AirDate = hasPublished ? published!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty,
				Overview = string.IsNullOrWhiteSpace(discoveredVideo.Description) ? null : discoveredVideo.Description.Trim(),
				Runtime = discoveredVideo.Runtime ?? 0,
				IsShort = isOnShortsTab,
				Monitored = monitorNew,
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

			persistProgressHandled++;
			if (onPersistProgress is not null &&
			    persistProgressTotal > 0 &&
			    (persistProgressHandled % 400 == 0 || persistProgressHandled == persistProgressTotal))
				await onPersistProgress(persistProgressHandled, persistProgressTotal, ct);
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

	/// <summary>
	/// True when upstream metadata is still missing. Empty description is valid (many videos have none on YouTube).
	/// Does not treat derived or duplicate display fields
	/// (<see cref="VideoEntity.AirDate"/>, <see cref="VideoEntity.AirDateUtc"/> from upload time, or <see cref="VideoEntity.Overview"/> from description) as independent requirements.
	/// </summary>
	static bool NeedsHydrate(VideoEntity video)
	{
		return string.IsNullOrWhiteSpace(video.Title) ||
			string.IsNullOrWhiteSpace(video.ThumbnailUrl) ||
			IsPlaceholderDate(video.UploadDateUtc);
	}

	/// <summary>Per-field counts among videos still needing hydration (one video may appear in multiple buckets).</summary>
	static string FormatHydrationReasonSummary(IEnumerable<VideoEntity> videos)
	{
		var missingTitle = 0;
		var missingThumbnail = 0;
		var placeholderUploadDate = 0;
		foreach (var v in videos)
		{
			if (string.IsNullOrWhiteSpace(v.Title)) missingTitle++;
			if (string.IsNullOrWhiteSpace(v.ThumbnailUrl)) missingThumbnail++;
			if (IsPlaceholderDate(v.UploadDateUtc)) placeholderUploadDate++;
		}

		var parts = new List<string>(3);
		if (missingTitle > 0)
			parts.Add($"missing title ({missingTitle})");
		if (missingThumbnail > 0)
			parts.Add($"missing thumbnail ({missingThumbnail})");
		if (placeholderUploadDate > 0)
			parts.Add($"placeholder upload date ({placeholderUploadDate})");
		return parts.Count > 0 ? string.Join(", ", parts) : "none";
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

	/// <summary>
	/// Whether direct/API fetch is sufficient without fallback. Excludes fields that are duplicates or derivable
	/// from others (air date strings, overview vs description, redundant air vs upload timestamps).
	/// Description is optional — many videos have no description on YouTube.
	/// </summary>
	static bool HasCompleteVideoMetadata(VideoWatchPageMetadata? metadata)
	{
		return metadata is not null &&
			!string.IsNullOrWhiteSpace(metadata.Title) &&
			!string.IsNullOrWhiteSpace(metadata.ThumbnailUrl) &&
			metadata.UploadDateUtc.HasValue &&
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
		var hasShortsTab = directMetadata?.HasShortsTab == true || fallbackMetadata?.HasShortsTab == true
			? true
			: (bool?)null;

		return new ChannelPageMetadata(
			YoutubeChannelId: mergedYoutubeChannelId,
			Title: title,
			Description: description,
			ThumbnailUrl: thumbnailUrl,
			BannerUrl: bannerUrl,
			CanonicalUrl: $"https://www.youtube.com/channel/{mergedYoutubeChannelId}",
			HasShortsTab: hasShortsTab);
	}

	static VideoWatchPageMetadata? MergeVideoMetadata(
		string youtubeVideoId,
		VideoWatchPageMetadata? directMetadata,
		VideoWatchPageMetadata? fallbackMetadata,
		bool preferApiLivestreamIdentification)
	{
		var title = directMetadata?.Title ?? fallbackMetadata?.Title;
		var description = directMetadata?.Description ?? fallbackMetadata?.Description;
		var thumbnailUrl = directMetadata?.ThumbnailUrl ?? fallbackMetadata?.ThumbnailUrl;
		var uploadDateUtc = directMetadata?.UploadDateUtc ?? fallbackMetadata?.UploadDateUtc;
		var airDateUtc = directMetadata?.AirDateUtc ?? fallbackMetadata?.AirDateUtc ?? uploadDateUtc;
		var airDate = directMetadata?.AirDate ?? fallbackMetadata?.AirDate ?? airDateUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		var overview = directMetadata?.Overview ?? fallbackMetadata?.Overview ?? description;
		var runtime = directMetadata?.Runtime ?? fallbackMetadata?.Runtime;
		bool? isShort = directMetadata?.IsShort == true || fallbackMetadata?.IsShort == true
			? true
			: directMetadata?.IsShort ?? fallbackMetadata?.IsShort;
		bool? isLivestream;
		if (preferApiLivestreamIdentification)
		{
			// API-first mode: prefer direct (API) livestream signal, then yt-dlp fallback.
			isLivestream =
				directMetadata?.IsLivestream == true ? true :
				fallbackMetadata?.IsLivestream == true ? true :
				null;
		}
		else
		{
			// Default mode: prioritize yt-dlp retroactive signal (was_live/live_status), then direct source.
			// Treat explicit false as known-not-live so we do not run the extra yt-dlp probe for every VOD.
			if (fallbackMetadata?.IsLivestream == true || directMetadata?.IsLivestream == true)
				isLivestream = true;
			else if (fallbackMetadata?.IsLivestream == false || directMetadata?.IsLivestream == false)
				isLivestream = false;
			else
				isLivestream = null;
		}

		if (string.IsNullOrWhiteSpace(title) &&
			string.IsNullOrWhiteSpace(description) &&
			string.IsNullOrWhiteSpace(thumbnailUrl) &&
			!uploadDateUtc.HasValue &&
			!runtime.HasValue &&
			isShort != true &&
			isLivestream != true)
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
			Runtime: runtime,
			IsShort: isShort,
			IsLivestream: isLivestream);
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
		if (metadata.IsShort.HasValue)
			video.IsShort = metadata.IsShort.Value;
		if (metadata.IsLivestream.HasValue)
			video.IsLivestream = metadata.IsLivestream.Value;
		FillDerivedVideoDisplayFields(video);
	}

	/// <summary>Fills duplicate/derived columns from canonical fields so we do not re-fetch upstream for them.</summary>
	static void FillDerivedVideoDisplayFields(VideoEntity video)
	{
		if (!IsPlaceholderDate(video.UploadDateUtc))
		{
			if (IsPlaceholderDate(video.AirDateUtc))
				video.AirDateUtc = video.UploadDateUtc;
			if (string.IsNullOrWhiteSpace(video.AirDate))
				video.AirDate = video.UploadDateUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		}

		if (!string.IsNullOrWhiteSpace(video.Description) && string.IsNullOrWhiteSpace(video.Overview))
			video.Overview = video.Description;
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

		var cookiesPath = await YtDlpMetadataService.GetCookiesPathAsync(db, ct);
		var metadata = await YtDlpChannelLookupService.EnrichChannelForCreateAsync(executablePath, youtubeChannelId, ct, cookiesPath);
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

		var cookiesPath = await YtDlpMetadataService.GetCookiesPathAsync(db, ct);

		var docs = await YtDlpMetadataService.RunYtDlpJsonAsync(
			executablePath,
			ChannelResolveHelper.GetCanonicalChannelVideosUrl(youtubeChannelId),
			ct,
			playlistItems: null,
			timeoutMs: 120_000,
			flatPlaylist: true,
			cookiesPath: cookiesPath);

		try
		{
			return FlattenYtDlpDiscoveryDocuments(docs);
		}
		finally
		{
			foreach (var doc in docs)
				doc.Dispose();
		}
	}

	/// <summary>Merge yt-dlp -j lines into discovery rows (caller disposes <paramref name="docs"/>).</summary>
	public static List<ChannelVideoDiscoveryItem> FlattenYtDlpDiscoveryDocuments(IReadOnlyList<JsonDocument> docs)
	{
		var items = new List<ChannelVideoDiscoveryItem>();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var doc in docs)
			CollectYtDlpDiscoveryItems(doc.RootElement, items, seen);
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

		var cookiesPath = await YtDlpMetadataService.GetCookiesPathAsync(db, ct);

		var docs = await YtDlpMetadataService.RunYtDlpJsonAsync(
			executablePath,
			$"https://www.youtube.com/watch?v={youtubeVideoId}",
			ct,
			playlistItems: null,
			timeoutMs: 60_000,
			flatPlaylist: false,
			cookiesPath: cookiesPath);

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
			Runtime: runtime,
			IsShort: null,
			IsLivestream: ParseYtDlpIsLivestream(element));
	}

	static bool? ParseYtDlpIsLivestream(JsonElement element)
	{
		var liveStatus = GetYtDlpString(element, "live_status");
		if (!string.IsNullOrWhiteSpace(liveStatus))
		{
			switch (liveStatus.Trim().ToLowerInvariant())
			{
				case "is_live":
				case "was_live":
				case "is_upcoming":
				case "post_live":
					return true;
			}
		}

		var wasLive = GetYtDlpBool(element, "was_live");
		if (wasLive == true)
			return true;
		var isLive = GetYtDlpBool(element, "is_live");
		if (isLive == true)
			return true;
		var isUpcoming = GetYtDlpBool(element, "is_upcoming");
		if (isUpcoming == true)
			return true;

		return null;
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

	static bool? GetYtDlpBool(JsonElement element, string propertyName)
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
