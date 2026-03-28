using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using TubeArr.Backend.Data;
using TubeArr.Backend.DownloadBackends;
using TubeArr.Backend.QualityProfile;
using TubeArr.Shared.Infrastructure;

namespace TubeArr.Backend;

/// <summary>
/// Processes DownloadQueue items: runs yt-dlp for each queued video to the channel's root folder with the channel's quality profile.
/// </summary>
public static class DownloadQueueProcessor
{
	/// <summary>When true, failed downloads that look like cookie/auth errors pause the queue and export browser cookies once, then retry. Keep false to avoid touching the browser during downloads.</summary>
	const bool AutomaticBrowserCookieRefreshOnAuthFailure = false;

	/// <summary>Must match <c>Logging:LogLevel</c> in appsettings so download/cookie diagnostics are not filtered by category.</summary>
	const string DownloadQueueLogCategory = "TubeArr.Backend.DownloadQueueProcessor";

	const int DownloadTimeoutMs = 600_000; // 10 min per video

	/// <summary>Allowed range for <c>YtDlpConfig.DownloadQueueParallelWorkers</c> (settings UI and DB).</summary>
	public const int MinDownloadQueueParallelWorkers = 1;
	public const int MaxDownloadQueueParallelWorkers = 10;

	/// <summary>When reset, download workers wait before claiming the next queue item (reserved for automatic cookie refresh).</summary>
	static readonly ManualResetEventSlim DownloadUnpaused = new(true);

	static readonly SemaphoreSlim CookieRefreshMutex = new(1, 1);

	static volatile bool _isProcessing;

	/// <summary>True if the download loop is currently running.</summary>
	public static bool IsProcessing => _isProcessing;

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
			videosQuery = videosQuery.Where(v => v.PlaylistId == targetPlaylistId.Value);

		var videoIds = await videosQuery
			.Select(v => v.Id)
			.ToListAsync(ct);
		var existingList = await db.DownloadQueue
			.Where(q => q.ChannelId == channelId && q.Status != QueueJobStatuses.Failed && q.Status != QueueJobStatuses.Completed)
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
			.Where(q => q.ChannelId == channelId && q.Status != QueueJobStatuses.Failed && q.Status != QueueJobStatuses.Completed)
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
		CancellationToken ct = default,
		ILogger? logger = null,
		IBrowserCookieService? browserCookieService = null)
	{
		await RecoverOrphanedDownloadingItemsAsync(db, executablePath, ct, logger);

		// Peek the next id: nullable so an empty queue is not confused with Id==0.
		var nextId = await db.DownloadQueue
			.Where(q => q.Status == QueueJobStatuses.Queued)
			.OrderBy(q => q.Id)
			.Select(q => (int?)q.Id)
			.FirstOrDefaultAsync(ct);
		if (nextId is null)
			return false;

		var claimTime = DateTimeOffset.UtcNow;
		var claimedRows = await db.DownloadQueue
			.Where(q => q.Id == nextId.Value && q.Status == QueueJobStatuses.Queued)
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(q => q.Status, QueueJobStatuses.Running)
				.SetProperty(q => q.StartedAtUtc, claimTime)
				.SetProperty(q => q.Progress, 0.0)
				.SetProperty(q => q.EstimatedSecondsRemaining, (int?)null),
			ct);
		// Lost race versus another processor, user cleared the queue, or status changed.
		if (claimedRows == 0)
			return true;

		try
		{
			var item = await db.DownloadQueue.FirstAsync(q => q.Id == nextId.Value, ct);

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
				await db.SaveChangesAsync(ct);
				return true;
			}

			logger?.LogInformation("Starting download queueId={QueueId} channelId={ChannelId} videoId={VideoId} youtubeVideoId={YoutubeVideoId} title={Title}",
				item.Id, item.ChannelId, item.VideoId, video.YoutubeVideoId, video.Title);

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
				item.LastError = "No quality profile exists. Create one in Settings â†’ Quality Profiles.";
				item.EndedAtUtc = DateTimeOffset.UtcNow;
				await db.SaveChangesAsync(ct);
				logger?.LogWarning("Download failed: no quality profile available queueId={QueueId} channelId={ChannelId}", item.Id, item.ChannelId);
				return true;
			}

			var rootFolders = await db.RootFolders.AsNoTracking().ToListAsync(ct);
			var naming = await db.NamingConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct) ?? new NamingConfigEntity { Id = 1 };
			PlaylistEntity? playlist = null;
			if (channel.PlaylistFolder == true && video.PlaylistId.HasValue)
			{
				playlist = await db.Playlists.AsNoTracking().FirstOrDefaultAsync(p => p.Id == video.PlaylistId.Value, ct);
			}

			var outputDir = GetOutputDirectory(channel, video, playlist, naming, rootFolders);
			if (string.IsNullOrWhiteSpace(outputDir))
			{
				item.Status = QueueJobStatuses.Failed;
				item.EstimatedSecondsRemaining = null;
				item.LastError = "No root folder configured or channel folder could not be resolved.";
				item.EndedAtUtc = DateTimeOffset.UtcNow;
				await db.SaveChangesAsync(ct);
				logger?.LogWarning("Download failed: output directory unresolved queueId={QueueId} channelId={ChannelId} channelPath={ChannelPath} rootFolders={RootFolderCount}",
					item.Id, item.ChannelId, channel.Path, rootFolders.Count);
				return true;
			}

			Directory.CreateDirectory(outputDir);
			var ytProfileBuild = new YtDlpQualityProfileBuilder().Build(profile);
			var ffmpegConfig = await db.FFmpegConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
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
				await db.SaveChangesAsync(ct);
				logger?.LogWarning("Download failed: empty video id queueId={QueueId} raw={Raw}", item.Id, video.YoutubeVideoId);
				return true;
			}

			var url = "https://www.youtube.com/watch?v=" + youtubeVideoIdForYtDlp;
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
				await db.SaveChangesAsync(ct);
				return true;
			}

			if (!File.Exists(configPath))
			{
				item.Status = QueueJobStatuses.Failed;
				item.EstimatedSecondsRemaining = null;
				item.LastError = "Quality profile config file not found: " + configPath;
				item.EndedAtUtc = DateTimeOffset.UtcNow;
				await db.SaveChangesAsync(ct);
				logger?.LogWarning("Download failed: missing quality profile config queueId={QueueId} path={ConfigPath}", item.Id, configPath);
				return true;
			}

			logger?.LogInformation("yt-dlp quality profile config queueId={QueueId} path={ConfigPath}", item.Id, configPath);

			var rawProfileConfig = File.Exists(configPath)
				? await File.ReadAllTextAsync(configPath, ct)
				: "";

			var lastProgressSaveAt = 0L;
			const int ProgressSaveIntervalMs = 500;
			async ValueTask OnDownloadProgress(DownloadProgressInfo p)
			{
				if (p.Progress.HasValue)
					item.Progress = p.Progress.Value;
				item.EstimatedSecondsRemaining = p.EstimatedSecondsRemaining;
				var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
				if (now - lastProgressSaveAt < ProgressSaveIntervalMs)
					return;
				lastProgressSaveAt = now;
				await db.SaveChangesAsync(ct);
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
			var attemptResult = await backendRouter.Get(DownloadBackendKind.YtDlp).DownloadAsync(ytReq, ct);

			if (!attemptResult.Success)
			{
				item.Status = QueueJobStatuses.Failed;
				item.Progress = null;
				item.EstimatedSecondsRemaining = null;
				item.LastError = attemptResult.UserMessage ?? "Download failed.";
				logger?.LogWarning(
					"Download failed queueId={QueueId} backend={Backend} stage={Stage} code={Code} detail={Detail}",
					item.Id,
					backendLabel,
					attemptResult.FailureStage,
					attemptResult.StructuredErrorCode,
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
						item.LastError = attemptResult.UserMessage ?? "Download produced an intermediate stream file only.";
						item.OutputPath = null;
						item.EndedAtUtc = DateTimeOffset.UtcNow;
						await db.SaveChangesAsync(ct);
						return true;
					}

					try
					{
						var keepFullPath = Path.GetFullPath(resolvedOutputPath);
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

						logger?.LogInformation("Cleanup complete queueId={QueueId} kept={OutputPath}", item.Id, resolvedOutputPath);
					}
					catch
					{
						// best-effort cleanup
					}

					item.Status = QueueJobStatuses.Completed;
					item.Progress = 1.0;
					item.EstimatedSecondsRemaining = 0;
					item.OutputPath = resolvedOutputPath;
					item.LastError = null;
					try
					{
						var fileInfo = new FileInfo(resolvedOutputPath);
						var rootPrefix = rootFolders
							.Select(r => (r.Path ?? "").Trim())
							.Where(p => !string.IsNullOrWhiteSpace(p))
							.OrderByDescending(p => p.Length)
							.FirstOrDefault(p =>
								resolvedOutputPath.StartsWith(
									Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
									StringComparison.OrdinalIgnoreCase));
						var relativePath = !string.IsNullOrWhiteSpace(rootPrefix)
							? Path.GetRelativePath(rootPrefix!, resolvedOutputPath)
							: Path.GetFileName(resolvedOutputPath);
						var existingVideoFile = await db.VideoFiles.FirstOrDefaultAsync(vf => vf.VideoId == video.Id, ct);
						if (existingVideoFile is null)
						{
							db.VideoFiles.Add(new VideoFileEntity
							{
								VideoId = video.Id,
								ChannelId = video.ChannelId,
								PlaylistId = video.PlaylistId,
								Path = resolvedOutputPath,
								RelativePath = relativePath,
								Size = fileInfo.Exists ? fileInfo.Length : 0,
								DateAdded = DateTimeOffset.UtcNow
							});
						}
						else
						{
							existingVideoFile.ChannelId = video.ChannelId;
							existingVideoFile.PlaylistId = video.PlaylistId;
							existingVideoFile.Path = resolvedOutputPath;
							existingVideoFile.RelativePath = relativePath;
							existingVideoFile.Size = fileInfo.Exists ? fileInfo.Length : 0;
							existingVideoFile.DateAdded = DateTimeOffset.UtcNow;
						}
					}
					catch (Exception ex)
					{
						logger?.LogWarning(ex, "Failed to upsert video file tracking queueId={QueueId} videoId={VideoId}", item.Id, video.Id);
					}

					logger?.LogInformation("Download completed queueId={QueueId} outputPath={OutputPath} backend={Backend}", item.Id, resolvedOutputPath, backendLabel);
				}
			}
		}
		catch (Exception ex) when (ex is not DbUpdateConcurrencyException)
		{
			item.Status = QueueJobStatuses.Failed;
			item.LastError = ex.Message ?? "Download failed.";
			item.EstimatedSecondsRemaining = null;
			logger?.LogError(ex, "Download exception queueId={QueueId} channelId={ChannelId} videoId={VideoId}", item.Id, item.ChannelId, item.VideoId);
		}

		// Final persist: detach and reload the queue row so we do not update/delete a stale entity
		// (e.g. user removed the item while a download was still running).
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
			return true;
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
				PlaylistId = video?.PlaylistId,
				EventType = 3, // imported
				SourceTitle = video?.Title ?? $"Video {videoId}",
				OutputPath = terminalOutputPath,
				Message = null,
				DownloadId = queueId.ToString(),
				Date = (live.EndedAtUtc ?? DateTimeOffset.UtcNow).UtcDateTime
			});

			// Completed items no longer remain in active queue.
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

		return true;
		}
		catch (DbUpdateConcurrencyException ex)
		{
			logger?.LogWarning(ex, "Download queue row was removed or updated concurrently queueId={QueueId}.", nextId!.Value);
			db.ChangeTracker.Clear();
			return true;
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
			await Task.Run(() => DownloadUnpaused.Wait(ct), ct);
			await using var scope = scopeFactory.CreateAsyncScope();
			var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
			var browserCookieService = scope.ServiceProvider.GetService<IBrowserCookieService>();
			var backendRouter = scope.ServiceProvider.GetRequiredService<DownloadBackendRouter>();
			if (!await ProcessOneAsync(db, executablePath, contentRoot, backendRouter, ct, logger, browserCookieService))
				break;

			if (onItemProcessed is not null)
				await onItemProcessed(ct);
			await Task.Delay(1000, ct);
		}
	}

	internal static bool LooksLikeYtDlpCookieAuthFailure(string? stderr)
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
		await CookieRefreshMutex.WaitAsync(ct);
		try
		{
			logger?.LogInformation("Pausing download workers for automatic cookie refresh");
			DownloadUnpaused.Reset();
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
				DownloadUnpaused.Set();
				logger?.LogInformation("Resuming download workers after cookie refresh attempt");
			}
		}
		finally
		{
			CookieRefreshMutex.Release();
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
		return v.Length <= max ? v : v.Substring(0, max) + "â€¦";
	}

	internal static string? GetOutputDirectory(
		ChannelEntity channel,
		VideoEntity video,
		PlaylistEntity? playlist,
		NamingConfigEntity naming,
		List<RootFolderEntity> rootFolders)
	{
		if (rootFolders.Count == 0)
			return null;
		var root = rootFolders[0].Path.Trim();
		// Use channel.Path if set (full path), else root + channel folder from naming
		if (!string.IsNullOrWhiteSpace(channel.Path))
		{
			var channelBasePath = Path.IsPathRooted(channel.Path) ? channel.Path : Path.Combine(root, channel.Path);
			if (channel.PlaylistFolder == true && playlist is not null)
			{
				var context = new VideoFileNaming.NamingContext(
					Channel: channel,
					Video: video,
					Playlist: playlist,
					PlaylistIndex: null,
					QualityFull: null,
					Resolution: null,
					Extension: null);
				var playlistFolderName = VideoFileNaming.BuildFolderName(naming.PlaylistFolderFormat, context, naming);
				if (!string.IsNullOrWhiteSpace(playlistFolderName))
					return Path.Combine(channelBasePath, playlistFolderName);
			}

			return channelBasePath;
		}
		var channelContext = new VideoFileNaming.NamingContext(Channel: channel, Video: video, Playlist: null, PlaylistIndex: null, QualityFull: null, Resolution: null, Extension: null);
		var channelFolderName = VideoFileNaming.BuildFolderName(naming.ChannelFolderFormat, channelContext, naming);
		if (string.IsNullOrWhiteSpace(channelFolderName))
			return null;

		var channelPath = Path.Combine(root, channelFolderName);
		if (channel.PlaylistFolder == true && playlist is not null)
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
				return Path.Combine(channelPath, playlistFolderName);
		}

		return channelPath;
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

	internal static (bool probeRan, bool hasAudio, string? error) ProbeHasAudioStream(string mediaPath, string? ffmpegLocation, ILogger? logger)
	{
		try
		{
			var ffprobePath = ResolveFfprobePath(ffmpegLocation);
			if (string.IsNullOrWhiteSpace(ffprobePath))
			{
				return (false, false, "ffprobe path unavailable");
			}

			if (!File.Exists(ffprobePath))
			{
				return (false, false, $"ffprobe not found: {ffprobePath}");
			}

			var startInfo = new ProcessStartInfo
			{
				FileName = ffprobePath,
				Arguments = $"-v error -select_streams a -show_entries stream=codec_type -of csv=p=0 \"{mediaPath}\"",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			};

			using var process = Process.Start(startInfo);
			if (process is null)
			{
				return (false, false, "failed to start ffprobe");
			}

			if (!process.WaitForExit(15000))
			{
				try { process.Kill(true); } catch { }
				return (false, false, "ffprobe timed out");
			}

			var stdout = process.StandardOutput.ReadToEnd();
			var stderr = process.StandardError.ReadToEnd();
			var hasAudio = process.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdout);
			return (true, hasAudio, process.ExitCode == 0 ? null : stderr);
		}
		catch (Exception ex)
		{
			logger?.LogDebug(ex, "Audio stream probe failed for path={Path}", mediaPath);
			return (false, false, ex.Message);
		}
	}

	internal static string? ResolveFfprobePath(string? ffmpegLocation)
	{
		if (string.IsNullOrWhiteSpace(ffmpegLocation))
		{
			return null;
		}

		var location = ffmpegLocation.Trim().Trim('"');
		if (string.IsNullOrWhiteSpace(location))
		{
			return null;
		}

		if (Directory.Exists(location))
		{
			return Path.Combine(location, OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");
		}

		if (File.Exists(location))
		{
			var directory = Path.GetDirectoryName(location);
			if (string.IsNullOrWhiteSpace(directory))
			{
				return null;
			}

			return Path.Combine(directory, OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");
		}

		// Handle configured paths that may not currently exist but still point to an ffmpeg binary location.
		var asDirectory = Path.GetDirectoryName(location);
		if (string.IsNullOrWhiteSpace(asDirectory))
		{
			return null;
		}

		return Path.Combine(asDirectory, OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");
	}
}
