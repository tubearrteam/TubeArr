using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TubeArr.Backend.Data;
using TubeArr.Backend.DownloadBackends;
using TubeArr.Backend.Media.Nfo;
using TubeArr.Backend.QualityProfile;
using TubeArr.Shared.Infrastructure;

namespace TubeArr.Backend;

/// <summary>
/// Processes DownloadQueue items: runs yt-dlp for each queued video to the channel's root folder with the channel's quality profile.
/// </summary>
public static partial class DownloadQueueProcessor
{
	/// <summary>When true, failed downloads that look like cookie/auth errors pause the queue and export browser cookies once, then retry. Keep false to avoid touching the browser during downloads.</summary>
	const bool AutomaticBrowserCookieRefreshOnAuthFailure = false;

	/// <summary>Must match <c>Logging:LogLevel</c> in appsettings so download/cookie diagnostics are not filtered by category.</summary>
	const string DownloadQueueLogCategory = "TubeArr.Backend.DownloadQueueProcessor";

	const int DownloadTimeoutMs = 600_000; // 10 min stall (no yt-dlp progress lines) per video; timer resets while download reports progress.

	/// <summary>Message stored on aborted queue/history rows when the host cancels the download token.</summary>
	internal const string DownloadQueueCancelledUserMessage = "Download cancelled.";

	/// <summary>Allowed range for <c>YtDlpConfig.DownloadQueueParallelWorkers</c> (settings UI and DB).</summary>
	public const int MinDownloadQueueParallelWorkers = 1;
	public const int MaxDownloadQueueParallelWorkers = 10;

	static volatile bool _isProcessing;

	/// <summary>True if the download loop is currently running.</summary>
	public static bool IsProcessing => _isProcessing;

	/// <summary>Signals the active yt-dlp download for this queue row to stop. Returns false if that row is not downloading.</summary>
	public static bool TryCancelActiveDownload(int queueId) =>
		DownloadQueueWorkerSync.TryCancelActiveDownload(queueId);

	/// <summary>Add monitored videos for a channel (optionally one playlist) to the queue and start processing in the background.</summary>
	public static async Task<int> EnqueueMonitoredVideosAsync(TubeArrDbContext db, int channelId, int? playlistNumber = null, CancellationToken ct = default, ILogger? logger = null)
	{
		int? targetPlaylistId = null;
		if (playlistNumber.HasValue && playlistNumber.Value > 1)
		{
			var orderedPlaylists = await ChannelDtoMapper.LoadPlaylistsOrderedByLatestUploadAsync(db, channelId, ct);
			var index = playlistNumber.Value - 2; // playlistNumber 1 is the synthetic "Videos" row
			if (index < 0 || index >= orderedPlaylists.Count)
			{
				logger?.LogInformation("Enqueue skipped: playlistNumber not found channelId={ChannelId} playlistNumber={PlaylistNumber}", channelId, playlistNumber.Value);
				return 0;
			}

			targetPlaylistId = orderedPlaylists[index].Id;
		}

		var filterChannelFlags = await db.Channels.AsNoTracking()
			.Where(c => c.Id == channelId)
			.Select(c => new { c.FilterOutShorts, c.FilterOutLivestreams, c.HasShortsTab })
			.FirstOrDefaultAsync(ct);

		var videosQuery = db.Videos.Where(v => v.ChannelId == channelId && v.Monitored);
		if (filterChannelFlags is { FilterOutShorts: true, HasShortsTab: true })
			videosQuery = videosQuery.Where(v => !v.IsShort);
		if (filterChannelFlags is { FilterOutLivestreams: true })
			videosQuery = videosQuery.Where(v => !v.IsLivestream);
		if (targetPlaylistId.HasValue)
			videosQuery = videosQuery.Where(v =>
				db.PlaylistVideos.Any(pv => pv.VideoId == v.Id && pv.PlaylistId == targetPlaylistId.Value));

		var videoIds = await videosQuery
			.Select(v => v.Id)
			.ToListAsync(ct);
		var existingList = await db.DownloadQueue
			.Where(q => q.ChannelId == channelId && q.Status != QueueJobStatuses.Failed && q.Status != QueueJobStatuses.Completed && q.Status != QueueJobStatuses.Aborted)
			.Select(q => q.VideoId)
			.ToListAsync(ct);
		var existing = new HashSet<int>(existingList);
		var toAdd = videoIds.Where(id => !existing.Contains(id)).ToList();
		foreach (var videoId in toAdd)
		{
			db.DownloadQueue.Add(new DownloadQueueEntity
			{
				VideoId = videoId,
				ChannelId = channelId,
				Status = QueueJobStatuses.Queued,
				QueuedAtUtc = DateTimeOffset.UtcNow,
				AcquisitionMethodsJson = AcquisitionMethodsJsonHelper.DefaultDownloadJson
			});
		}
		await db.SaveChangesAsync(ct);
		logger?.LogInformation("Enqueued {Count} monitored video(s) for channelId={ChannelId} playlistNumber={PlaylistNumber} (alreadyQueued={AlreadyQueued})",
			toAdd.Count, channelId, playlistNumber, existing.Count);
		return toAdd.Count;
	}

	/// <summary>
	/// Enqueue specific videos for a channel by internal id (ignores monitored flag).
	/// Skips videos not in the channel or already active in the queue.
	/// </summary>
	public static async Task<int> EnqueueVideosAsync(TubeArrDbContext db, int channelId, IReadOnlyList<int> videoIds, CancellationToken ct = default, ILogger? logger = null)
	{
		if (videoIds.Count == 0)
			return 0;

		var idSet = videoIds.Distinct().ToHashSet();
		var validIds = await db.Videos
			.Where(v => v.ChannelId == channelId && idSet.Contains(v.Id))
			.Select(v => v.Id)
			.ToListAsync(ct);

		if (validIds.Count == 0)
		{
			logger?.LogInformation("EnqueueVideosAsync: no matching videos channelId={ChannelId}", channelId);
			return 0;
		}

		var existingList = await db.DownloadQueue
			.Where(q => q.ChannelId == channelId && q.Status != QueueJobStatuses.Failed && q.Status != QueueJobStatuses.Completed && q.Status != QueueJobStatuses.Aborted)
			.Select(q => q.VideoId)
			.ToListAsync(ct);
		var existing = new HashSet<int>(existingList);
		var toAdd = validIds.Where(id => !existing.Contains(id)).ToList();

		foreach (var videoId in toAdd)
		{
			db.DownloadQueue.Add(new DownloadQueueEntity
			{
				VideoId = videoId,
				ChannelId = channelId,
				Status = QueueJobStatuses.Queued,
				QueuedAtUtc = DateTimeOffset.UtcNow,
				AcquisitionMethodsJson = AcquisitionMethodsJsonHelper.DefaultDownloadJson
			});
		}

		await db.SaveChangesAsync(ct);
		logger?.LogInformation("Enqueued {Count} video(s) for download by id channelId={ChannelId} (requested={Requested} alreadyQueued={AlreadyQueued})",
			toAdd.Count, channelId, validIds.Count, existing.Count);
		return toAdd.Count;
	}

	/// <summary>
	/// Enqueues monitored videos that have no media file on disk (tracked <see cref="VideoFileEntity.Path"/> must exist).
	/// Scope: <see cref="VideoEntity.Monitored"/> and <see cref="ChannelEntity.Monitored"/>.
	/// Skips videos already queued or downloading. Downloads use each channel's quality profile when processed.
	/// </summary>
	public static async Task<int> EnqueueMonitoredMissingOnDiskAsync(
		TubeArrDbContext db,
		IReadOnlySet<int>? excludedChannelIds = null,
		CancellationToken ct = default,
		ILogger? logger = null)
	{
		var videoFiles = await db.VideoFiles.AsNoTracking()
			.Select(vf => new { vf.VideoId, vf.Path })
			.ToListAsync(ct);

		var onDisk = new HashSet<int>();
		foreach (var vf in videoFiles)
		{
			if (!string.IsNullOrWhiteSpace(vf.Path) && File.Exists(vf.Path))
				onDisk.Add(vf.VideoId);
		}

		var candidates = await (
			from v in db.Videos.AsNoTracking()
			join c in db.Channels.AsNoTracking() on v.ChannelId equals c.Id
			where v.Monitored &&
			      c.Monitored &&
			      !(c.FilterOutShorts && c.HasShortsTab == true && v.IsShort) &&
			      !(c.FilterOutLivestreams && v.IsLivestream)
			select new { v.Id, v.ChannelId }
		).ToListAsync(ct);

		var activeQueue = await db.DownloadQueue
			.Where(q => q.Status == QueueJobStatuses.Queued || q.Status == QueueJobStatuses.Running)
			.Select(q => q.VideoId)
			.ToListAsync(ct);
		var inActiveQueue = new HashSet<int>(activeQueue);

		var toAdd = new List<(int VideoId, int ChannelId)>();
		foreach (var row in candidates)
		{
			if (excludedChannelIds is not null && excludedChannelIds.Contains(row.ChannelId))
				continue;
			if (onDisk.Contains(row.Id))
				continue;
			if (inActiveQueue.Contains(row.Id))
				continue;
			toAdd.Add((row.Id, row.ChannelId));
		}

		foreach (var (videoId, channelId) in toAdd)
		{
			db.DownloadQueue.Add(new DownloadQueueEntity
			{
				VideoId = videoId,
				ChannelId = channelId,
				Status = QueueJobStatuses.Queued,
				QueuedAtUtc = DateTimeOffset.UtcNow,
				AcquisitionMethodsJson = AcquisitionMethodsJsonHelper.DefaultDownloadJson
			});
		}

		await db.SaveChangesAsync(ct);
		logger?.LogInformation(
			"Enqueue missing on disk: added {Added} monitored video(s) (monitoredChannelVideos={Eligible})",
			toAdd.Count, candidates.Count);

		return toAdd.Count;
	}

	/// <summary>Process one queue item with yt-dlp.</summary>
	public static async Task<bool> ProcessOneAsync(
		TubeArrDbContext db,
		string executablePath,
		string contentRoot,
		DownloadBackendRouter backendRouter,
		IHttpClientFactory httpClientFactory,
		CancellationToken ct = default,
		ILogger? logger = null,
		IBrowserCookieService? browserCookieService = null,
		TubeArrDbPersistQueue? persistQueue = null,
		Integrations.Slskd.SlskdHttpClient? slskdHttpClient = null)
	{
		await RecoverOrphanedDownloadingItemsAsync(db, executablePath, ct, logger);

		var nextRow = await db.DownloadQueue
			.Where(q => q.Status == QueueJobStatuses.Queued
				|| (q.Status == QueueJobStatuses.Running && q.ExternalWorkPending == 1))
			.OrderBy(q => q.Id)
			.Select(q => new { q.Id, q.Status })
			.FirstOrDefaultAsync(ct);
		if (nextRow is null)
			return false;

		var claimTime = DateTimeOffset.UtcNow;
		if (nextRow.Status == QueueJobStatuses.Queued)
		{
			var claimedRows = await db.DownloadQueue
				.Where(q => q.Id == nextRow.Id && q.Status == QueueJobStatuses.Queued)
				.ExecuteUpdateAsync(setters => setters
					.SetProperty(q => q.Status, QueueJobStatuses.Running)
					.SetProperty(q => q.StartedAtUtc, claimTime)
					.SetProperty(q => q.Progress, 0.0)
					.SetProperty(q => q.EstimatedSecondsRemaining, (int?)null)
					.SetProperty(q => q.FormatSummary, (string?)null)
					.SetProperty(q => q.ExternalWorkPending, 0),
				ct);
			if (claimedRows == 0)
				return true;
		}
		else
		{
			var cleared = await db.DownloadQueue
				.Where(q => q.Id == nextRow.Id && q.ExternalWorkPending == 1)
				.ExecuteUpdateAsync(setters => setters.SetProperty(q => q.ExternalWorkPending, 0), ct);
			if (cleared == 0)
				return true;
		}

		try
		{
			var item = await db.DownloadQueue.FirstAsync(q => q.Id == nextRow.Id, ct);

			var video = await db.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == item.VideoId, ct);
			var channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == item.ChannelId, ct);
			if (video is null || channel is null)
			{
				logger?.LogWarning("Queue item invalid: queueId={QueueId} videoId={VideoId} channelId={ChannelId} (videoFound={VideoFound} channelFound={ChannelFound})",
					item.Id, item.VideoId, item.ChannelId, video is not null, channel is not null);
				item.Status = QueueJobStatuses.Failed;
				item.EstimatedSecondsRemaining = null;
				item.LastError = "Video or channel not found.";
				item.EndedAtUtc = DateTimeOffset.UtcNow;
				await FinalizeTerminalDownloadQueueOutcomeAsync(db, item, video, null, ct, logger);
				return true;
			}

			logger?.LogInformation("Starting download queueId={QueueId} channelId={ChannelId} videoId={VideoId} youtubeVideoId={YoutubeVideoId} title={Title}",
				item.Id, item.ChannelId, item.VideoId, video.YoutubeVideoId, video.Title);

			var primaryPlaylistId = await ChannelDtoMapper.GetPrimaryPlaylistIdForVideoAsync(db, channel.Id, video.Id, ct);

			try
			{
				var profile = channel.QualityProfileId.HasValue
				? await db.QualityProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == channel.QualityProfileId.Value, ct)
				: null;
			// When channel has no profile assigned, use first available profile so downloads can run
			if (profile is null)
				profile = await db.QualityProfiles.AsNoTracking().OrderBy(p => p.Id).FirstOrDefaultAsync(ct);
			if (profile is null)
			{
				item.Status = QueueJobStatuses.Failed;
				item.EstimatedSecondsRemaining = null;
				item.LastError = "No quality profile exists. Create one in Settings → Quality Profiles.";
				item.EndedAtUtc = DateTimeOffset.UtcNow;
				await FinalizeTerminalDownloadQueueOutcomeAsync(db, item, video, primaryPlaylistId, ct, logger);
				logger?.LogWarning("Download failed: no quality profile available queueId={QueueId} channelId={ChannelId}", item.Id, item.ChannelId);
				return true;
			}

			var rootFolders = await db.RootFolders.AsNoTracking().ToListAsync(ct);
			var naming = await db.NamingConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct) ?? new NamingConfigEntity { Id = 1 };
			var mediaManagement = await db.MediaManagementConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
			var useCustomNfos = mediaManagement?.UseCustomNfos != false;
			var plexProvider = await db.PlexProviderConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
			var exportLibraryThumbnails = LibraryThumbnailExportPolicy.ShouldExport(
				mediaManagement?.DownloadLibraryThumbnails == true,
				plexProvider?.Enabled == true);
			PlaylistEntity? playlist = null;
			if (channel.PlaylistFolder == true && primaryPlaylistId.HasValue)
			{
				playlist = await db.Playlists.AsNoTracking().FirstOrDefaultAsync(p => p.Id == primaryPlaylistId.Value, ct);
			}

			int? seasonForPlaylistFolder = null;
			if (channel.PlaylistFolder == true && useCustomNfos)
			{
				var (season, _) = await NfoLibraryExporter.ResolveSeasonNumberForPlaylistFolderAsync(db, channel.Id, video, primaryPlaylistId, ct);
				seasonForPlaylistFolder = season;
			}

			var outputDir = GetOutputDirectory(channel, video, playlist, naming, rootFolders, useCustomNfos, seasonForPlaylistFolder);
			if (string.IsNullOrWhiteSpace(outputDir))
			{
				item.Status = QueueJobStatuses.Failed;
				item.EstimatedSecondsRemaining = null;
				item.LastError = "No root folder configured or channel folder could not be resolved.";
				item.EndedAtUtc = DateTimeOffset.UtcNow;
				await FinalizeTerminalDownloadQueueOutcomeAsync(db, item, video, primaryPlaylistId, ct, logger);
				logger?.LogWarning("Download failed: output directory unresolved queueId={QueueId} channelId={ChannelId} channelPath={ChannelPath} rootFolders={RootFolderCount}",
					item.Id, item.ChannelId, channel.Path, rootFolders.Count);
				return true;
			}

			Directory.CreateDirectory(outputDir);

			var ffmpegConfig = await db.FFmpegConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
			var slskdCfgRow = await db.SlskdConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
			if (slskdHttpClient is not null && slskdCfgRow is not null)
			{
				var slskdHandled = await TrySlskdAcquisitionFlowAsync(
					db,
					item,
					video,
					channel,
					profile,
					outputDir,
					contentRoot,
					SanitizeYoutubeVideoIdForWatchUrl(video.YoutubeVideoId),
					executablePath,
					slskdHttpClient,
					slskdCfgRow,
					naming,
					playlist,
					primaryPlaylistId,
					seasonForPlaylistFolder,
					rootFolders,
					useCustomNfos,
					exportLibraryThumbnails,
					httpClientFactory,
					ffmpegConfig,
					ct,
					logger);
				if (slskdHandled)
				{
					if (item.Status == QueueJobStatuses.Running)
					{
						var ext = Integrations.Slskd.ExternalAcquisitionJsonSerializer.TryDeserialize(item.ExternalAcquisitionJson);
						if (ext?.Phase == Integrations.Slskd.ExternalAcquisitionPhases.Transferring || ext?.ResumeProcessor == true)
							item.ExternalWorkPending = 1;
					}

					await db.SaveChangesAsync(ct);
					if (item.Status != QueueJobStatuses.Running)
						await FinalizeTerminalDownloadQueueOutcomeAsync(db, item, video, primaryPlaylistId, ct, logger);
					return true;
				}
			}

			var orderKind = slskdCfgRow is null
				? Integrations.Slskd.AcquisitionOrderKind.YtDlpFirst
				: Integrations.Slskd.AcquisitionOrderKindExtensions.ParseOrDefault(slskdCfgRow.AcquisitionOrder);
			if (orderKind == Integrations.Slskd.AcquisitionOrderKind.SlskdOnly)
			{
				item.Status = QueueJobStatuses.Failed;
				item.LastError = item.LastError ?? "slskd-only mode: acquisition did not complete.";
				item.EndedAtUtc = DateTimeOffset.UtcNow;
				await FinalizeTerminalDownloadQueueOutcomeAsync(db, item, video, primaryPlaylistId, ct, logger);
				return true;
			}

			var ytProfileBuild = new YtDlpQualityProfileBuilder().Build(profile);
			var ffmpegConfigured = ffmpegConfig is not null && ffmpegConfig.Enabled && !string.IsNullOrWhiteSpace(ffmpegConfig.ExecutablePath);
			await QualityProfileConfigFileOperations.EnsureConfigFileExistsAsync(contentRoot, profile, ffmpegConfigured, logger, ct);
			var configPath = QualityProfileConfigPaths.GetConfigFilePath(contentRoot, profile.Id);
			var configTextForHints = await QualityProfileConfigFileOperations.ReadConfigTextOrEmptyAsync(contentRoot, profile.Id, ct);
			var preferredOutputContainer = QualityProfileYtDlpConfigContent.TryGetMergeOutputFormatFromConfigText(configTextForHints)
				?? QualityProfileYtDlpConfigContent.GetPreferredOutputContainer(profile);
			var youtubeVideoIdForYtDlp = SanitizeYoutubeVideoIdForWatchUrl(video.YoutubeVideoId);
			if (string.IsNullOrWhiteSpace(youtubeVideoIdForYtDlp))
			{
				item.Status = QueueJobStatuses.Failed;
				item.EstimatedSecondsRemaining = null;
				item.LastError = "Invalid or empty YouTube video id.";
				item.EndedAtUtc = DateTimeOffset.UtcNow;
				await FinalizeTerminalDownloadQueueOutcomeAsync(db, item, video, primaryPlaylistId, ct, logger);
				logger?.LogWarning("Download failed: empty video id queueId={QueueId} raw={Raw}", item.Id, video.YoutubeVideoId);
				return true;
			}

			var url = "https://www.youtube.com/watch?v=" + youtubeVideoIdForYtDlp;
			// yt-dlp must write [video id] into the filename so the download backend can pick the merged output; user-defined naming is applied afterward when Rename Videos is enabled.
			var outputTemplate = Path.Combine(outputDir, "%(upload_date)s - %(title)s [%(id)s].%(ext)s");

			const string backendLabel = DownloadBackendKindParser.YtDlpString;
			logger?.LogInformation("Starting yt-dlp download queueId={QueueId}", item.Id);

			var cookiesPath = await YtDlpMetadataService.GetCookiesPathAsync(db, ct, contentRoot);
			string? resolvedCookiesPath = null;
			var cookiesFileReadable = false;
			if (!string.IsNullOrWhiteSpace(cookiesPath))
			{
				try
				{
					resolvedCookiesPath = Path.GetFullPath(cookiesPath.Trim());
					cookiesFileReadable = File.Exists(resolvedCookiesPath);
				}
				catch
				{
					resolvedCookiesPath = cookiesPath;
					cookiesFileReadable = File.Exists(cookiesPath);
				}

				if (!cookiesFileReadable)
				{
					logger?.LogWarning(
						"Cookies path is set but file is missing or not readable. queueId={QueueId} cookiesPath={CookiesPath}",
						item.Id, cookiesPath);
				}
			}
			else
			{
				logger?.LogWarning(
					"No Netscape cookies file resolved; YouTube may require cookies (Settings → Tools → yt-dlp). queueId={QueueId}",
					item.Id);
			}

			if (string.IsNullOrWhiteSpace(executablePath))
			{
				item.Status = QueueJobStatuses.Failed;
				item.EstimatedSecondsRemaining = null;
				item.LastError = "yt-dlp is not configured. Set the executable in Settings → Tools → yt-dlp.";
				item.EndedAtUtc = DateTimeOffset.UtcNow;
				await FinalizeTerminalDownloadQueueOutcomeAsync(db, item, video, primaryPlaylistId, ct, logger);
				return true;
			}

			if (!File.Exists(configPath))
			{
				item.Status = QueueJobStatuses.Failed;
				item.EstimatedSecondsRemaining = null;
				item.LastError = "Quality profile config file not found: " + configPath;
				item.EndedAtUtc = DateTimeOffset.UtcNow;
				await FinalizeTerminalDownloadQueueOutcomeAsync(db, item, video, primaryPlaylistId, ct, logger);
				logger?.LogWarning("Download failed: missing quality profile config queueId={QueueId} path={ConfigPath}", item.Id, configPath);
				return true;
			}

			logger?.LogInformation("yt-dlp quality profile config queueId={QueueId} path={ConfigPath}", item.Id, configPath);

			var ytdlpRetryCfg = await db.YtDlpConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
			var maxTransientRetries = Math.Clamp(ytdlpRetryCfg?.DownloadTransientMaxRetries ?? 3, 0, 10);
			var retryDelaysSeconds = DownloadRetryPolicy.ParseRetryDelaysSecondsJson(ytdlpRetryCfg?.DownloadRetryDelaysSecondsJson);

			using var perDownloadCts = new CancellationTokenSource();
			DownloadQueueWorkerSync.ActiveDownloadCancellations[item.Id] = perDownloadCts;
			using var linkedDownload = CancellationTokenSource.CreateLinkedTokenSource(ct, perDownloadCts.Token);
			var downloadCt = linkedDownload.Token;

			DownloadAttemptResult attemptResult;
			try
			{
				var rawProfileConfig = File.Exists(configPath)
					? await File.ReadAllTextAsync(configPath, downloadCt)
					: "";

				var lastProgressSaveAt = 0L;
				const int ProgressSaveIntervalMs = 500;
				async ValueTask OnDownloadProgress(DownloadProgressInfo p)
				{
					if (p.Progress.HasValue)
						item.Progress = p.Progress.Value;
					if (p.EstimatedSecondsRemaining.HasValue)
						item.EstimatedSecondsRemaining = p.EstimatedSecondsRemaining;
					if (!string.IsNullOrWhiteSpace(p.FormatSummary))
						item.FormatSummary = p.FormatSummary.Trim();
					var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
					if (now - lastProgressSaveAt < ProgressSaveIntervalMs)
						return;
					lastProgressSaveAt = now;
					await db.SaveChangesAsync(downloadCt);
				}

				var ytReq = new DownloadRequest
				{
					QueueId = item.Id,
					VideoId = video.Id,
					ChannelId = item.ChannelId,
					YoutubeVideoId = youtubeVideoIdForYtDlp,
					WatchUrl = url,
					OutputDirectory = outputDir,
					BackendKind = DownloadBackendKind.YtDlp,
					ContentRoot = contentRoot,
					Db = db,
					YtDlpExecutablePath = executablePath,
					CookiesPath = cookiesPath,
					CookiesFileReadable = cookiesFileReadable,
					ResolvedCookiesPath = resolvedCookiesPath,
					QualityProfileConfigPath = configPath,
					RawQualityProfileConfigText = rawProfileConfig,
					OutputTemplate = outputTemplate,
					YtDlpProfileHints = ytProfileBuild,
					PreferredOutputContainer = preferredOutputContainer,
					FfmpegConfigured = ffmpegConfigured,
					FfmpegExecutablePath = ffmpegConfig?.ExecutablePath,
					OnProgress = OnDownloadProgress,
					BrowserCookieService = browserCookieService
				};

				// Retry loop: only transient/unknown failures (see DownloadRetryPolicy); max attempts and delays from YtDlpConfig.
				attemptResult = null!;
				for (var attempt = 0; attempt <= maxTransientRetries; attempt++)
				{
					if (attempt > 0)
					{
						var delaySeconds = retryDelaysSeconds[Math.Min(attempt - 1, retryDelaysSeconds.Length - 1)];
						logger?.LogInformation(
							"Retrying download queueId={QueueId} attempt={Attempt}/{MaxRetries} after {Delay}s delay",
							item.Id, attempt, maxTransientRetries, delaySeconds);
						item.Progress = 0.0;
						item.EstimatedSecondsRemaining = null;
						item.LastError = $"Retrying ({attempt}/{maxTransientRetries}) after transient failure...";
						await db.SaveChangesAsync(downloadCt);
						await Task.Delay(TimeSpan.FromSeconds(delaySeconds), downloadCt);
						lastProgressSaveAt = 0L;
					}

					attemptResult = await backendRouter.Get(DownloadBackendKind.YtDlp).DownloadAsync(ytReq, downloadCt);

					if (attemptResult.Success)
						break;

					if (IsPermanentDownloadError(attemptResult.UserMessage, attemptResult.DiagnosticDetails))
					{
						logger?.LogInformation(
							"Download error is permanent, skipping retries queueId={QueueId} detail={Detail}",
							item.Id, Truncate(attemptResult.UserMessage, 200));
						break;
					}

					var retryClass = DownloadRetryPolicy.Classify(
						attemptResult.FailureStage,
						attemptResult.StructuredErrorCode,
						attemptResult.DiagnosticDetails ?? attemptResult.UserMessage);
					if (!DownloadRetryPolicy.ShouldRetryAfterFailure(retryClass))
					{
						logger?.LogInformation(
							"Download failure not auto-retried queueId={QueueId} class={Class} stage={Stage} code={Code}",
							item.Id, retryClass, attemptResult.FailureStage, attemptResult.StructuredErrorCode);
						break;
					}

					if (attempt >= maxTransientRetries)
						break;

					logger?.LogWarning(
						"Retry-eligible download failure queueId={QueueId} attempt={Attempt}/{MaxRetries} stage={Stage} code={Code} detail={Detail}",
						item.Id, attempt + 1, maxTransientRetries,
						attemptResult.FailureStage, attemptResult.StructuredErrorCode,
						Truncate(attemptResult.DiagnosticDetails ?? attemptResult.UserMessage, 500));
				}
			}
			finally
			{
				DownloadQueueWorkerSync.ActiveDownloadCancellations.TryRemove(item.Id, out var reg);
				reg?.Dispose();
			}

			if (!attemptResult.Success)
			{
				var extFb = Integrations.Slskd.ExternalAcquisitionJsonSerializer.TryDeserialize(item.ExternalAcquisitionJson)
					?? new Integrations.Slskd.ExternalAcquisitionState();
				var orderFb = slskdCfgRow is null
					? Integrations.Slskd.AcquisitionOrderKind.YtDlpFirst
					: Integrations.Slskd.AcquisitionOrderKindExtensions.ParseOrDefault(slskdCfgRow.AcquisitionOrder);
				var slskdReadyFb = slskdCfgRow is not null
					&& slskdCfgRow.Enabled
					&& !string.IsNullOrWhiteSpace(slskdCfgRow.BaseUrl)
					&& !string.IsNullOrWhiteSpace(slskdCfgRow.ApiKey);
				if (slskdCfgRow is not null
					&& TryPrepareSlskdFallbackAfterYtDlpFailure(
						item,
						extFb,
						slskdCfgRow,
						orderFb,
						slskdReadyFb,
						attemptResult.UserMessage))
				{
					await db.SaveChangesAsync(ct);
					return true;
				}

				TryCleanupPartialYtDlpArtifacts(outputDir, youtubeVideoIdForYtDlp, logger);
				var failureClass = DownloadRetryPolicy.Classify(
					attemptResult.FailureStage,
					attemptResult.StructuredErrorCode,
					attemptResult.DiagnosticDetails ?? attemptResult.UserMessage);
				item.Status = QueueJobStatuses.Failed;
				item.Progress = null;
				item.EstimatedSecondsRemaining = null;
				item.FormatSummary = null;
				item.LastError = attemptResult.UserMessage ?? "Download failed.";
				if (failureClass == DownloadRetryPolicy.FailureClass.AuthOrCookies)
					item.LastError += DownloadRetryPolicy.FormatCookieActionHint(resolvedCookiesPath, cookiesFileReadable);
				logger?.LogWarning(
					"Download failed (all retries exhausted) queueId={QueueId} backend={Backend} stage={Stage} code={Code} class={FailureClass} detail={Detail}",
					item.Id,
					backendLabel,
					attemptResult.FailureStage,
					attemptResult.StructuredErrorCode,
					failureClass,
					Truncate(attemptResult.DiagnosticDetails ?? attemptResult.UserMessage, 2000));
			}
			else
			{
				var resolvedOutputPath = attemptResult.PrimaryOutputPath;
				if (string.IsNullOrWhiteSpace(resolvedOutputPath) || !File.Exists(resolvedOutputPath))
				{
					item.Status = QueueJobStatuses.Failed;
					item.Progress = null;
					item.EstimatedSecondsRemaining = null;
					item.FormatSummary = null;
					item.LastError = "Download reported success but output file was missing.";
				}
				else
				{
					var expectedToken = $"[{youtubeVideoIdForYtDlp}]";
					if (IsIntermediateYtDlpPartFile(resolvedOutputPath))
					{
						item.Status = QueueJobStatuses.Failed;
						item.Progress = null;
						item.EstimatedSecondsRemaining = null;
						item.FormatSummary = null;
						item.LastError = attemptResult.UserMessage ?? "Download produced an intermediate stream file only.";
						item.OutputPath = null;
						item.EndedAtUtc = DateTimeOffset.UtcNow;
						await FinalizeTerminalDownloadQueueOutcomeAsync(db, item, video, primaryPlaylistId, ct, logger);
						return true;
					}

					var workingOutputPath = await ApplyUserVideoNamingToDownloadedFileAsync(
						db,
						resolvedOutputPath,
						outputDir,
						channel,
						video,
						primaryPlaylistId,
						playlist,
						naming,
						useCustomNfos,
						item.Id,
						logger,
						ct);

					try
					{
						var keepFullPath = Path.GetFullPath(workingOutputPath);
						var mediaExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
						{
							".mp4", ".webm", ".mkv", ".avi", ".mov", ".m4v", ".flv", ".wmv", ".mpg", ".mpeg",
							".m4a", ".mp3", ".aac", ".opus", ".ogg", ".wav", ".flac"
						};

						foreach (var filePath in Directory.EnumerateFiles(outputDir, "*", SearchOption.TopDirectoryOnly))
						{
							var name = Path.GetFileName(filePath);
							if (string.IsNullOrWhiteSpace(name) || !name.Contains(expectedToken, StringComparison.OrdinalIgnoreCase))
								continue;
							if (!mediaExts.Contains(Path.GetExtension(filePath)))
								continue;
							var full = Path.GetFullPath(filePath);
							if (string.Equals(full, keepFullPath, StringComparison.OrdinalIgnoreCase))
								continue;
							try { File.Delete(full); } catch { /* best-effort */ }
						}

						logger?.LogInformation("Cleanup complete queueId={QueueId} kept={OutputPath}", item.Id, workingOutputPath);
					}
					catch
					{
						// best-effort cleanup
					}

					item.Status = QueueJobStatuses.Completed;
					item.Progress = 1.0;
					item.EstimatedSecondsRemaining = 0;
					item.OutputPath = workingOutputPath;
					item.LastError = null;
					if (persistQueue is not null)
					{
						var (seasonForArt, _) = await NfoLibraryExporter.ResolveSeasonNumberForPlaylistFolderAsync(
							db, channel.Id, video, primaryPlaylistId, ct);
						NfoLibraryExporter.ExpectedNfoSet? builtNfo = null;
						if (useCustomNfos)
						{
							try
							{
								builtNfo = await NfoLibraryExporter.TryBuildExpectedNfoSetAsync(
									db,
									channel,
									video,
									playlist,
									primaryPlaylistId,
									workingOutputPath,
									naming,
									rootFolders,
									ct);
							}
							catch (Exception ex)
							{
								logger?.LogWarning(ex, "NFO build failed queueId={QueueId} videoId={VideoId}", item.Id, video.Id);
							}
						}

						var vfVideoId = video.Id;
						var vfChannelId = video.ChannelId;
						var vfPrimary = primaryPlaylistId;
						var vfPath = workingOutputPath;
						var qId = item.Id;

						Task PersistVideoFileAsync() => persistQueue.EnqueueAsync(async (persistDb, persistCt) =>
						{
							try
							{
								var fileInfo = new FileInfo(vfPath);
								var rootPrefix = rootFolders
									.Select(r => (r.Path ?? "").Trim())
									.Where(p => !string.IsNullOrWhiteSpace(p))
									.OrderByDescending(p => p.Length)
									.FirstOrDefault(p =>
										vfPath.StartsWith(
											Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
											StringComparison.OrdinalIgnoreCase));
								var relativePath = !string.IsNullOrWhiteSpace(rootPrefix)
									? Path.GetRelativePath(rootPrefix!, vfPath)
									: Path.GetFileName(vfPath);
								var existingVideoFile = await persistDb.VideoFiles.FirstOrDefaultAsync(vf => vf.VideoId == vfVideoId, persistCt);
								if (existingVideoFile is null)
								{
									persistDb.VideoFiles.Add(new VideoFileEntity
									{
										VideoId = vfVideoId,
										ChannelId = vfChannelId,
										PlaylistId = vfPrimary,
										Path = vfPath,
										RelativePath = relativePath,
										Size = fileInfo.Exists ? fileInfo.Length : 0,
										DateAdded = DateTimeOffset.UtcNow
									});
								}
								else
								{
									existingVideoFile.ChannelId = vfChannelId;
									existingVideoFile.PlaylistId = vfPrimary;
									existingVideoFile.Path = vfPath;
									existingVideoFile.RelativePath = relativePath;
									existingVideoFile.Size = fileInfo.Exists ? fileInfo.Length : 0;
									existingVideoFile.DateAdded = DateTimeOffset.UtcNow;
								}

								await persistDb.SaveChangesAsync(persistCt);
							}
							catch (Exception ex)
							{
								logger?.LogWarning(ex, "Failed to upsert video file tracking queueId={QueueId} videoId={VideoId}", qId, vfVideoId);
							}
						}, ct);

						async Task WriteNfoFilesAsync()
						{
							if (!useCustomNfos || builtNfo is null)
								return;
							try
							{
								await NfoLibraryExporter.WriteExpectedNfoSetAsync(builtNfo.Value, rootFolders, ct);
							}
							catch (Exception ex)
							{
								logger?.LogWarning(ex, "NFO export failed queueId={QueueId} videoId={VideoId}", qId, video.Id);
							}
						}

						async Task WriteArtworkFilesAsync()
						{
							if (!exportLibraryThumbnails)
								return;
							try
							{
								await PlexLibraryArtworkExporter.WriteForCompletedDownloadWithSeasonAsync(
									channel,
									video,
									playlist,
									seasonForArt,
									workingOutputPath,
									naming,
									rootFolders,
									httpClientFactory,
									logger,
									ct);
							}
							catch (Exception ex)
							{
								logger?.LogWarning(ex, "Library thumbnail export failed queueId={QueueId} videoId={VideoId}", qId, video.Id);
							}
						}

						await Task.WhenAll(PersistVideoFileAsync(), WriteNfoFilesAsync(), WriteArtworkFilesAsync());

						try
						{
							var schemaJson = SystemMiscEndpoints.GetNotificationSchemaJson();
							await persistQueue.EnqueueAsync(async (persistDb, persistCt) =>
								await PlexNotificationRefresher.TryAfterVideoFileImportedAsync(
									persistDb,
									httpClientFactory,
									schemaJson,
									logger,
									persistCt), ct);
						}
						catch (Exception ex)
						{
							logger?.LogDebug(ex, "Plex notification refresh skipped queueId={QueueId}", qId);
						}
					}
					else
					{
						try
						{
							var fileInfo = new FileInfo(workingOutputPath);
							var rootPrefix = rootFolders
								.Select(r => (r.Path ?? "").Trim())
								.Where(p => !string.IsNullOrWhiteSpace(p))
								.OrderByDescending(p => p.Length)
								.FirstOrDefault(p =>
									workingOutputPath.StartsWith(
										Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
										StringComparison.OrdinalIgnoreCase));
							var relativePath = !string.IsNullOrWhiteSpace(rootPrefix)
								? Path.GetRelativePath(rootPrefix!, workingOutputPath)
								: Path.GetFileName(workingOutputPath);
							var existingVideoFile = await db.VideoFiles.FirstOrDefaultAsync(vf => vf.VideoId == video.Id, ct);
							if (existingVideoFile is null)
							{
								db.VideoFiles.Add(new VideoFileEntity
								{
									VideoId = video.Id,
									ChannelId = video.ChannelId,
									PlaylistId = primaryPlaylistId,
									Path = workingOutputPath,
									RelativePath = relativePath,
									Size = fileInfo.Exists ? fileInfo.Length : 0,
									DateAdded = DateTimeOffset.UtcNow
								});
							}
							else
							{
								existingVideoFile.ChannelId = video.ChannelId;
								existingVideoFile.PlaylistId = primaryPlaylistId;
								existingVideoFile.Path = workingOutputPath;
								existingVideoFile.RelativePath = relativePath;
								existingVideoFile.Size = fileInfo.Exists ? fileInfo.Length : 0;
								existingVideoFile.DateAdded = DateTimeOffset.UtcNow;
							}
						}
						catch (Exception ex)
						{
							logger?.LogWarning(ex, "Failed to upsert video file tracking queueId={QueueId} videoId={VideoId}", item.Id, video.Id);
						}

						if (useCustomNfos)
						{
							try
							{
								await NfoLibraryExporter.WriteForCompletedDownloadAsync(
									db,
									channel,
									video,
									playlist,
									primaryPlaylistId,
									workingOutputPath,
									naming,
									rootFolders,
									ct);
							}
							catch (Exception ex)
							{
								logger?.LogWarning(ex, "NFO export failed queueId={QueueId} videoId={VideoId}", item.Id, video.Id);
							}
						}

						if (exportLibraryThumbnails)
						{
							try
							{
								await PlexLibraryArtworkExporter.WriteForCompletedDownloadAsync(
									db,
									channel,
									video,
									playlist,
									primaryPlaylistId,
									workingOutputPath,
									naming,
									rootFolders,
									httpClientFactory,
									logger,
									ct);
							}
							catch (Exception ex)
							{
								logger?.LogWarning(ex, "Library thumbnail export failed queueId={QueueId} videoId={VideoId}", item.Id, video.Id);
							}
						}

						try
						{
							await PlexNotificationRefresher.TryAfterVideoFileImportedAsync(
								db,
								httpClientFactory,
								SystemMiscEndpoints.GetNotificationSchemaJson(),
								logger,
								ct);
						}
						catch (Exception ex)
						{
							logger?.LogDebug(ex, "Plex notification refresh skipped queueId={QueueId}", item.Id);
						}
					}

					logger?.LogInformation("Download completed queueId={QueueId} outputPath={OutputPath} backend={Backend}", item.Id, workingOutputPath, backendLabel);
				}
			}
		}
		catch (Exception ex) when (ex is not DbUpdateConcurrencyException)
		{
			item.EstimatedSecondsRemaining = null;
			item.Progress = null;
			item.FormatSummary = null;
			if (ex is OperationCanceledException)
			{
				item.Status = QueueJobStatuses.Aborted;
				item.LastError = DownloadQueueCancelledUserMessage;
				logger?.LogWarning(
					"Download cancelled queueId={QueueId} channelId={ChannelId} videoId={VideoId}",
					item.Id, item.ChannelId, item.VideoId);
			}
			else
			{
				item.Status = QueueJobStatuses.Failed;
				item.LastError = ex.Message ?? "Download failed.";
				logger?.LogError(ex, "Download exception queueId={QueueId} channelId={ChannelId} videoId={VideoId}", item.Id, item.ChannelId, item.VideoId);
			}
		}

		await FinalizeTerminalDownloadQueueOutcomeAsync(db, item, video, primaryPlaylistId, ct, logger);
		return true;
		}
		catch (DbUpdateConcurrencyException ex)
		{
			logger?.LogWarning(ex, "Download queue row was removed or updated concurrently queueId={QueueId}.", nextRow.Id);
			db.ChangeTracker.Clear();
			return true;
		}
	}

	/// <summary>Replaces the yt-dlp placeholder filename with <see cref="VideoFileNaming"/> output for the channel type (Settings → Media Management → Naming).</summary>
	static async Task<string> ApplyUserVideoNamingToDownloadedFileAsync(
		TubeArrDbContext db,
		string resolvedOutputPath,
		string outputDir,
		ChannelEntity channel,
		VideoEntity video,
		int? primaryPlaylistId,
		PlaylistEntity? playlist,
		NamingConfigEntity naming,
		bool useCustomNfos,
		int queueId,
		ILogger? logger,
		CancellationToken ct)
	{
		if (!naming.RenameVideos)
			return resolvedOutputPath;

		var ext = Path.GetExtension(resolvedOutputPath);
		if (string.IsNullOrWhiteSpace(ext))
			return resolvedOutputPath;

		var nameBase = await BuildDownloadVideoFileNameBaseAsync(
			db, channel, video, primaryPlaylistId, playlist, naming, useCustomNfos, ext, ct);
		if (string.IsNullOrWhiteSpace(nameBase))
			return resolvedOutputPath;

		var destPath = Path.Combine(outputDir, nameBase + ext);
		var fullSrc = Path.GetFullPath(resolvedOutputPath);
		var fullDest = Path.GetFullPath(destPath);
		var same = OperatingSystem.IsWindows()
			? string.Equals(fullSrc, fullDest, StringComparison.OrdinalIgnoreCase)
			: string.Equals(fullSrc, fullDest, StringComparison.Ordinal);
		if (same)
			return resolvedOutputPath;

		if (File.Exists(fullDest))
			throw new InvalidOperationException("A file already exists at the target path: " + fullDest);

		try
		{
			var dir = Path.GetDirectoryName(fullDest);
			if (!string.IsNullOrEmpty(dir))
				Directory.CreateDirectory(dir);
			File.Move(fullSrc, fullDest);
			var srcBase = Path.Combine(Path.GetDirectoryName(fullSrc) ?? "", Path.GetFileNameWithoutExtension(fullSrc));
			var dstBase = Path.Combine(Path.GetDirectoryName(fullDest) ?? "", Path.GetFileNameWithoutExtension(fullDest));
			TryMoveDownloadSidecar(srcBase + ".nfo", dstBase + ".nfo");
			TryMoveDownloadSidecar(srcBase + "-thumb.jpg", dstBase + "-thumb.jpg");
			logger?.LogInformation("Applied naming pattern to download queueId={QueueId} path={Path}", queueId, fullDest);
			return fullDest;
		}
		catch (Exception ex) when (ex is not InvalidOperationException)
		{
			logger?.LogWarning(ex, "Could not rename download to naming pattern queueId={QueueId}", queueId);
			return resolvedOutputPath;
		}
	}

	static void TryMoveDownloadSidecar(string sourcePath, string destinationPath)
	{
		try
		{
			if (!File.Exists(sourcePath))
				return;
			var dir = Path.GetDirectoryName(destinationPath);
			if (!string.IsNullOrEmpty(dir))
				Directory.CreateDirectory(dir);
			if (File.Exists(destinationPath))
				return;
			File.Move(sourcePath, destinationPath);
		}
		catch
		{
			// best-effort
		}
	}

	static async Task<string?> BuildDownloadVideoFileNameBaseAsync(
		TubeArrDbContext db,
		ChannelEntity channel,
		VideoEntity video,
		int? primaryPlaylistId,
		PlaylistEntity? playlist,
		NamingConfigEntity naming,
		bool useCustomNfos,
		string fileExtensionWithDot,
		CancellationToken ct)
	{
		string ResolveVideoPattern()
		{
			var ctRaw = (channel.ChannelType ?? "").Trim().ToLowerInvariant();
			return ctRaw switch
			{
				"daily" => naming.DailyVideoFormat,
				"episodic" => naming.EpisodicVideoFormat,
				"streaming" => naming.StreamingVideoFormat,
				_ => naming.StandardVideoFormat
			};
		}

		var videoPattern = ResolveVideoPattern();
		var patternForTokens = videoPattern ?? string.Empty;
		var needsPlaylistNumber = patternForTokens.Contains("{Playlist Number", StringComparison.OrdinalIgnoreCase);
		var needsPlaylistIndex = patternForTokens.Contains("{Playlist Index", StringComparison.OrdinalIgnoreCase);

		int? playlistNumberToken = null;
		if (needsPlaylistNumber || (channel.PlaylistFolder == true && useCustomNfos))
		{
			var (sn, _) = await NfoLibraryExporter.ResolveSeasonNumberForPlaylistFolderAsync(db, channel.Id, video, primaryPlaylistId, ct);
			if (needsPlaylistNumber)
				playlistNumberToken = sn;
		}

		int? playlistIndexToken = null;
		if (needsPlaylistIndex || channel.PlaylistFolder == true)
			playlistIndexToken = await NfoPlaylistEpisodeResolver.ResolveEpisodeNumberAsync(db, primaryPlaylistId, video.Id, ct);

		var ctx = new VideoFileNaming.NamingContext(
			Channel: channel,
			Playlist: playlist,
			Video: video,
			PlaylistIndex: playlistIndexToken,
			QualityFull: null,
			Resolution: null,
			Extension: fileExtensionWithDot,
			PlaylistNumber: playlistNumberToken);
		var built = VideoFileNaming.BuildFileName(videoPattern ?? string.Empty, ctx, naming);
		return string.IsNullOrWhiteSpace(built) ? null : built;
	}

	static async Task FinalizeTerminalDownloadQueueOutcomeAsync(
		TubeArrDbContext db,
		DownloadQueueEntity item,
		VideoEntity? video,
		int? primaryPlaylistId,
		CancellationToken ct,
		ILogger? logger)
	{
		var terminalStatus = item.Status;
		var terminalProgress = item.Progress;
		var terminalEsr = item.EstimatedSecondsRemaining;
		var terminalOutputPath = item.OutputPath;
		var terminalLastError = item.LastError;
		var queueId = item.Id;
		var channelId = item.ChannelId;
		var videoId = item.VideoId;

		db.Entry(item).State = EntityState.Detached;

		var live = await db.DownloadQueue.FirstOrDefaultAsync(q => q.Id == queueId, ct);
		if (live is null)
		{
			logger?.LogInformation(
				"Download queue row was removed before finalize; skipping queue persistence queueId={QueueId}",
				queueId);
			return;
		}

		live.Status = terminalStatus;
		live.Progress = terminalProgress;
		live.EstimatedSecondsRemaining = terminalEsr;
		live.OutputPath = terminalOutputPath;
		live.LastError = terminalLastError;
		live.EndedAtUtc = DateTimeOffset.UtcNow;

		if (live.Status == QueueJobStatuses.Completed)
		{
			db.DownloadHistory.Add(new DownloadHistoryEntity
			{
				ChannelId = channelId,
				VideoId = videoId,
				PlaylistId = primaryPlaylistId,
				EventType = 3, // imported
				SourceTitle = video?.Title ?? $"Video {videoId}",
				OutputPath = terminalOutputPath,
				Message = null,
				DownloadId = queueId.ToString(),
				Date = (live.EndedAtUtc ?? DateTimeOffset.UtcNow).UtcDateTime
			});

			db.DownloadQueue.Remove(live);
		}
		else if (live.Status is QueueJobStatuses.Failed or QueueJobStatuses.Aborted)
		{
			db.DownloadHistory.Add(new DownloadHistoryEntity
			{
				ChannelId = channelId,
				VideoId = videoId,
				PlaylistId = primaryPlaylistId,
				EventType = 4, // failed
				SourceTitle = video?.Title ?? $"Video {videoId}",
				OutputPath = terminalOutputPath,
				Message = terminalLastError,
				DownloadId = queueId.ToString(),
				Date = (live.EndedAtUtc ?? DateTimeOffset.UtcNow).UtcDateTime
			});

			db.DownloadQueue.Remove(live);
		}

		try
		{
			await db.SaveChangesAsync(ct);
		}
		catch (DbUpdateConcurrencyException)
		{
			logger?.LogWarning(
				"Download queue finalize lost a concurrency race queueId={QueueId} (row changed or removed during save).",
				queueId);
			db.ChangeTracker.Clear();
		}
	}

	static async Task RecoverOrphanedDownloadingItemsAsync(
		TubeArrDbContext db,
		string executablePath,
		CancellationToken ct,
		ILogger? logger)
	{
		var downloadingItems = await db.DownloadQueue
			.Where(q => q.Status == QueueJobStatuses.Running)
			.ToListAsync(ct);
		if (downloadingItems.Count == 0)
			return;

		var recoveryCutoff = DateTimeOffset.UtcNow.AddSeconds(-30);
		var recoveredCount = 0;

		foreach (var item in downloadingItems)
		{
			if (item.StartedAtUtc.HasValue && item.StartedAtUtc.Value > recoveryCutoff)
				continue;

			try
			{
				var processName = Path.GetFileNameWithoutExtension(executablePath);
				if (!string.IsNullOrWhiteSpace(processName) && Process.GetProcessesByName(processName).Length > 0)
					continue;
			}
			catch
			{
				return;
			}

			item.Status = QueueJobStatuses.Queued;
			item.StartedAtUtc = null;
			item.EndedAtUtc = null;
			item.Progress = 0;
			item.EstimatedSecondsRemaining = null;
			item.FormatSummary = null;
			item.LastError = null;
			recoveredCount++;
		}

		if (recoveredCount == 0)
			return;

		await db.SaveChangesAsync(ct);
		logger?.LogWarning(
			"Recovered {RecoveredCount} orphaned download queue item(s) with no active download process",
			recoveredCount);
	}

	/// <summary>Run the queue processor until no queued items remain. Worker count comes from yt-dlp settings (parallel downloads).</summary>
	public static async Task RunUntilEmptyAsync(
		IServiceScopeFactory scopeFactory,
		string contentRoot,
		CancellationToken ct = default,
		ILogger? logger = null,
		Func<CancellationToken, Task>? onItemProcessed = null)
	{
		var executablePath = "";
		var workerCount = MinDownloadQueueParallelWorkers;
		await using (var probeScope = scopeFactory.CreateAsyncScope())
		{
			var probeDb = probeScope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
			var cfg = await probeDb.YtDlpConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
			if (cfg is not null && cfg.Enabled)
			{
				var p = (cfg.ExecutablePath ?? "").Trim();
				if (!string.IsNullOrWhiteSpace(p))
					executablePath = p;
			}

			if (cfg is not null)
				workerCount = Math.Clamp(cfg.DownloadQueueParallelWorkers, MinDownloadQueueParallelWorkers, MaxDownloadQueueParallelWorkers);
		}

		ILogger? downloadLogger = logger;
		await using (var logScope = scopeFactory.CreateAsyncScope())
		{
			var factory = logScope.ServiceProvider.GetService<ILoggerFactory>();
			if (factory is not null)
				downloadLogger = factory.CreateLogger(DownloadQueueLogCategory);
		}

		_isProcessing = true;
		try
		{
			downloadLogger?.LogInformation(
				"Download queue processor started ({WorkerCount} parallel worker(s))",
				workerCount);
			var workers = new Task[workerCount];
			for (var w = 0; w < workerCount; w++)
				workers[w] = RunDownloadWorkerUntilEmptyAsync(executablePath, contentRoot, scopeFactory, ct, downloadLogger, onItemProcessed);
			await Task.WhenAll(workers);
			downloadLogger?.LogInformation("Download queue processor finished (no queued items)");
		}
		finally
		{
			_isProcessing = false;
		}
	}

	static async Task RunDownloadWorkerUntilEmptyAsync(
		string executablePath,
		string contentRoot,
		IServiceScopeFactory scopeFactory,
		CancellationToken ct,
		ILogger? logger,
		Func<CancellationToken, Task>? onItemProcessed)
	{
		while (!ct.IsCancellationRequested)
		{
			await Task.Run(() => DownloadQueueWorkerSync.DownloadUnpaused.Wait(ct), ct);
			await using var scope = scopeFactory.CreateAsyncScope();
			var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
			var browserCookieService = scope.ServiceProvider.GetService<IBrowserCookieService>();
			var backendRouter = scope.ServiceProvider.GetRequiredService<DownloadBackendRouter>();
			var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
			var persistQueue = scope.ServiceProvider.GetRequiredService<TubeArrDbPersistQueue>();
			var slskdHttp = scope.ServiceProvider.GetService<Integrations.Slskd.SlskdHttpClient>();
			if (!await ProcessOneAsync(db, executablePath, contentRoot, backendRouter, httpClientFactory, ct, logger, browserCookieService, persistQueue, slskdHttp))
				break;

			if (onItemProcessed is not null)
				await onItemProcessed(ct);
			await Task.Delay(1000, ct);
		}
	}

	static void TryCleanupPartialYtDlpArtifacts(string outputDir, string youtubeVideoId, ILogger? logger)
	{
		if (string.IsNullOrWhiteSpace(outputDir) || string.IsNullOrWhiteSpace(youtubeVideoId))
			return;
		try
		{
			foreach (var path in Directory.EnumerateFiles(outputDir, "*", SearchOption.TopDirectoryOnly))
			{
				var name = Path.GetFileName(path);
				if (!name.Contains(youtubeVideoId, StringComparison.OrdinalIgnoreCase))
					continue;
				var ext = Path.GetExtension(name);
				if (ext.Equals(".part", StringComparison.OrdinalIgnoreCase)
				    || ext.Equals(".ytdl", StringComparison.OrdinalIgnoreCase)
				    || name.Contains(".frag", StringComparison.OrdinalIgnoreCase))
				{
					try
					{
						File.Delete(path);
					}
					catch
					{
						/* best-effort */
					}
				}
			}
		}
		catch (Exception ex)
		{
			logger?.LogDebug(ex, "Partial download cleanup skipped for {Dir}", outputDir);
		}
	}

	public static bool LooksLikeYtDlpCookieAuthFailure(string? stderr)
	{
		if (string.IsNullOrWhiteSpace(stderr))
			return false;
		var t = stderr;
		if (t.Contains("Sign in to confirm", StringComparison.OrdinalIgnoreCase))
			return true;
		if (t.Contains("login required", StringComparison.OrdinalIgnoreCase))
			return true;
		if (t.Contains("Log in to", StringComparison.OrdinalIgnoreCase))
			return true;
		if (t.Contains("only available for registered users", StringComparison.OrdinalIgnoreCase))
			return true;
		if (t.Contains("HTTP Error 403", StringComparison.OrdinalIgnoreCase) && t.Contains("youtube", StringComparison.OrdinalIgnoreCase))
			return true;
		if (t.Contains("Unable to download webpage", StringComparison.OrdinalIgnoreCase) && t.Contains("403", StringComparison.OrdinalIgnoreCase))
			return true;
		if (t.Contains("cookies", StringComparison.OrdinalIgnoreCase)
			&& (t.Contains("invalid", StringComparison.OrdinalIgnoreCase) || t.Contains("expired", StringComparison.OrdinalIgnoreCase)))
			return true;
		return false;
	}

	static void TryOpenYoutubeInConfiguredBrowser(string browserKey, ILogger? logger)
	{
		try
		{
			var key = browserKey.Trim().ToLowerInvariant();
			string? exe = key switch
			{
				"chrome" => @"C:\Program Files\Google\Chrome\Application\chrome.exe",
				"edge" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
				"chromium" => @"C:\Program Files\Chromium\Application\chromium.exe",
				_ => null
			};
			if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = exe,
					Arguments = "https://www.youtube.com/",
					UseShellExecute = false
				});
				return;
			}

			Process.Start(new ProcessStartInfo
			{
				FileName = "https://www.youtube.com/",
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			logger?.LogWarning(ex, "Could not open YouTube in the browser before cookie export");
		}
	}

	internal static async Task<bool> TryRefreshBrowserCookiesForDownloadsAsync(
		TubeArrDbContext db,
		string contentRoot,
		IBrowserCookieService browserCookieService,
		CancellationToken ct,
		ILogger? logger)
	{
		await DownloadQueueWorkerSync.CookieRefreshMutex.WaitAsync(ct);
		try
		{
			logger?.LogInformation("Pausing download workers for automatic cookie refresh");
			DownloadQueueWorkerSync.DownloadUnpaused.Reset();
			try
			{
				var config = await db.YtDlpConfig.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
				if (config is null)
				{
					logger?.LogWarning("yt-dlp config is missing; cannot export cookies automatically");
					return false;
				}

				var browserKey = string.IsNullOrWhiteSpace(config.CookiesExportBrowser) ? "chrome" : config.CookiesExportBrowser.Trim();
				TryOpenYoutubeInConfiguredBrowser(browserKey, logger);
				await Task.Delay(2500, ct);

				var outputPath = YtDlpCookiesPathResolver.GetDefaultCookiesTxtPath(config.ExecutablePath, contentRoot);
				var dir = Path.GetDirectoryName(outputPath);
				if (!string.IsNullOrWhiteSpace(dir))
					Directory.CreateDirectory(dir);

				var exportResult = await browserCookieService.ExportBrowserCookiesAsync(browserKey, outputPath, reopenBrowser: true);
				if (!exportResult.Success)
				{
					logger?.LogWarning("Automatic cookie export failed: {Message}", exportResult.Message);
					return false;
				}

				var resolved = Path.GetFullPath(exportResult.CookiesPath ?? outputPath);
				config.CookiesPath = resolved;
				await db.SaveChangesAsync(ct);
				logger?.LogInformation(
					"Automatic cookie refresh saved cookies to {Path} ({Count} rows)",
					resolved,
					exportResult.CookieCount);
				return true;
			}
			finally
			{
				DownloadQueueWorkerSync.DownloadUnpaused.Set();
				logger?.LogInformation("Resuming download workers after cookie refresh attempt");
			}
		}
		finally
		{
			DownloadQueueWorkerSync.CookieRefreshMutex.Release();
		}
	}

	/// <summary>Strip #, URL fragments, and non-id characters so watch?v= matches yt-dlp output filenames.</summary>
	internal static string SanitizeYoutubeVideoIdForWatchUrl(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
			return "";
		var s = raw.Trim();
		if (s.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase))
		{
			var i = s.IndexOf("youtu.be/", StringComparison.OrdinalIgnoreCase);
			s = s[(i + 9)..];
		}
		else if (s.Contains("watch?v=", StringComparison.OrdinalIgnoreCase))
		{
			var i = s.IndexOf("v=", StringComparison.OrdinalIgnoreCase);
			s = s[(i + 2)..];
		}

		s = s.Trim().TrimStart('#');
		var cut = s.IndexOfAny("&?# \t\r\n".ToCharArray());
		if (cut >= 0)
			s = s[..cut];

		var sb = new StringBuilder();
		foreach (var c in s)
		{
			if (char.IsAsciiLetterOrDigit(c) || c is '_' or '-')
				sb.Append(c);
			else
				break;
		}

		return sb.ToString();
	}

	/// <summary>Single-line command preview for logs (matches argv passed to <see cref="ProcessStartInfo.ArgumentList"/>).</summary>
	internal static string FormatYtDlpInvocationForLog(string executablePath, IReadOnlyList<string> args)
	{
		var exe = string.IsNullOrWhiteSpace(executablePath) ? "yt-dlp" : executablePath;
		var parts = new List<string> { exe };
		foreach (var a in args)
		{
			if (a.Contains('"', StringComparison.Ordinal) || a.Contains(' ', StringComparison.Ordinal) || a.Contains('\t', StringComparison.Ordinal))
				parts.Add("\"" + a.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"");
			else
				parts.Add(a);
		}

		return string.Join(" ", parts);
	}

	internal static string Truncate(string? value, int max)
	{
		if (string.IsNullOrEmpty(value)) return "";
		var v = value.Trim();
		return v.Length <= max ? v : v.Substring(0, max) + "…";
	}

	/// <summary>
	/// Returns true if the error text indicates a permanent failure that should NOT be retried
	/// (video deleted, private, copyright strike, etc.).
	/// </summary>
	static bool IsPermanentDownloadError(string? userMessage, string? diagnosticDetails)
	{
		var combined = (userMessage ?? "") + " " + (diagnosticDetails ?? "");
		if (string.IsNullOrWhiteSpace(combined))
			return false;

		var permanentPatterns = new[]
		{
			"Video unavailable",
			"Private video",
			"This video has been removed",
			"This video is no longer available",
			"removed by the uploader",
			"account associated with this video has been terminated",
			"copyright claim",
			"copyright strike",
			"violates YouTube's Terms of Service",
			"Sign in to confirm your age",
			"Join this channel to get access",
			"This live event will begin",
			"Premieres in"
		};

		foreach (var pattern in permanentPatterns)
		{
			if (combined.Contains(pattern, StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
	}

	/// <summary>Show (channel) root folder — parent of playlist season folders when <see cref="ChannelEntity.PlaylistFolder"/> is used.</summary>
	internal static string? GetChannelShowRootPath(
		ChannelEntity channel,
		VideoEntity video,
		NamingConfigEntity naming,
		List<RootFolderEntity> rootFolders)
	{
		if (rootFolders.Count == 0)
			return null;
		var root = rootFolders[0].Path.Trim();
		if (!string.IsNullOrWhiteSpace(channel.Path))
		{
			return Path.IsPathRooted(channel.Path) ? channel.Path : Path.Combine(root, channel.Path);
		}

		var channelContext = new VideoFileNaming.NamingContext(Channel: channel, Video: video, Playlist: null, PlaylistIndex: null, QualityFull: null, Resolution: null, Extension: null);
		var channelFolderName = VideoFileNaming.BuildFolderName(naming.ChannelFolderFormat, channelContext, naming);
		if (string.IsNullOrWhiteSpace(channelFolderName))
			return null;

		return Path.Combine(root, channelFolderName);
	}

	internal static string? GetOutputDirectory(
		ChannelEntity channel,
		VideoEntity video,
		PlaylistEntity? playlist,
		NamingConfigEntity naming,
		List<RootFolderEntity> rootFolders,
		bool useCustomNfos = true,
		int? resolvedSeasonNumberForCustomNfoPlaylistFolder = null)
	{
		var channelBasePath = GetChannelShowRootPath(channel, video, naming, rootFolders);
		if (string.IsNullOrWhiteSpace(channelBasePath))
			return null;

		if (channel.PlaylistFolder == true)
		{
			if (useCustomNfos)
			{
				if (!resolvedSeasonNumberForCustomNfoPlaylistFolder.HasValue)
					return null;
				var seasonFolder = NfoLibraryExporter.FormatSeasonPlaylistFolderName(resolvedSeasonNumberForCustomNfoPlaylistFolder.Value);
				return Path.Combine(channelBasePath, seasonFolder);
			}
			else if (playlist is not null)
			{
				var playlistContext = new VideoFileNaming.NamingContext(
					Channel: channel,
					Video: video,
					Playlist: playlist,
					PlaylistIndex: null,
					QualityFull: null,
					Resolution: null,
					Extension: null);
				var playlistFolderName = VideoFileNaming.BuildFolderName(naming.PlaylistFolderFormat, playlistContext, naming);
				if (!string.IsNullOrWhiteSpace(playlistFolderName))
					return Path.Combine(channelBasePath, playlistFolderName);
			}
		}

		return channelBasePath;
	}

	internal static bool IsIntermediateYtDlpPartFile(string path)
	{
		var fileName = Path.GetFileNameWithoutExtension(path);
		if (string.IsNullOrWhiteSpace(fileName))
			return false;

		// yt-dlp separate stream parts commonly end with ".f137", ".f251", etc.
		return Regex.IsMatch(fileName, @"\.f[0-9A-Za-z]+$", RegexOptions.IgnoreCase);
	}

	internal static string? NormalizeFfmpegLocation(string? configuredPath)
	{
		if (string.IsNullOrWhiteSpace(configuredPath))
			return null;

		var path = configuredPath.Trim().Trim('"');
		if (string.IsNullOrWhiteSpace(path))
			return null;

		return path;
	}

	internal static bool IsAudioExtractionRequested(List<string> args, string? qualityProfileConfigPath)
	{
		if (args.Any(a => string.Equals(a, "-x", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(a, "--extract-audio", StringComparison.OrdinalIgnoreCase)))
			return true;
		if (string.IsNullOrEmpty(qualityProfileConfigPath) || !File.Exists(qualityProfileConfigPath))
			return false;
		try
		{
			return QualityProfileYtDlpConfigContent.ConfigTextMentionsAudioExtraction(File.ReadAllText(qualityProfileConfigPath));
		}
		catch
		{
			return false;
		}
	}

	internal static bool IsLikelyVideoContainer(string path)
	{
		var ext = Path.GetExtension(path)?.TrimStart('.').ToLowerInvariant();
		return ext switch
		{
			"mp4" or "webm" or "mkv" or "avi" or "mov" or "m4v" or "flv" or "wmv" or "mpg" or "mpeg" or "ts" or "3gp" => true,
			_ => false
		};
	}

}
