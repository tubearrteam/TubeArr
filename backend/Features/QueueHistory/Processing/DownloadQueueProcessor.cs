using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using TubeArr.Backend.Data;
using TubeArr.Backend.QualityProfile;

namespace TubeArr.Backend;

/// <summary>
/// Processes DownloadQueue items: runs yt-dlp for each queued video to the channel's root folder with the channel's quality profile.
/// </summary>
public static class DownloadQueueProcessor
{
	const int Queued = 0;
	const int Downloading = 1;
	const int Completed = 2;
	const int Failed = 3;
	const int DownloadTimeoutMs = 600_000; // 10 min per video

	static volatile bool _isProcessing;

	/// <summary>True if the download loop is currently running.</summary>
	public static bool IsProcessing => _isProcessing;

	/// <summary>Add monitored videos for a channel (optionally one playlist) to the queue and start processing in the background.</summary>
	public static async Task<int> EnqueueMonitoredVideosAsync(TubeArrDbContext db, int channelId, int? playlistNumber = null, CancellationToken ct = default, ILogger? logger = null)
	{
		int? targetPlaylistId = null;
		if (playlistNumber.HasValue && playlistNumber.Value > 1)
		{
			var orderedPlaylists = await db.Playlists.AsNoTracking()
				.Where(p => p.ChannelId == channelId)
				.OrderBy(p => p.Id)
				.ToListAsync(ct);
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
			.Select(c => new { c.FilterOutShorts, c.FilterOutLivestreams })
			.FirstOrDefaultAsync(ct);

		var videosQuery = db.Videos.Where(v => v.ChannelId == channelId && v.Monitored);
		if (filterChannelFlags is { FilterOutShorts: true })
			videosQuery = videosQuery.Where(v => !v.IsShort);
		if (filterChannelFlags is { FilterOutLivestreams: true })
			videosQuery = videosQuery.Where(v => !v.IsLivestream);
		if (targetPlaylistId.HasValue)
			videosQuery = videosQuery.Where(v => v.PlaylistId == targetPlaylistId.Value);

		var videoIds = await videosQuery
			.Select(v => v.Id)
			.ToListAsync(ct);
		var existingList = await db.DownloadQueue
			.Where(q => q.ChannelId == channelId && q.Status != Failed && q.Status != Completed)
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
				Status = Queued,
				QueuedAt = DateTimeOffset.UtcNow
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
			.Where(q => q.ChannelId == channelId && q.Status != Failed && q.Status != Completed)
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
				Status = Queued,
				QueuedAt = DateTimeOffset.UtcNow
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
			      !(c.FilterOutShorts && v.IsShort) &&
			      !(c.FilterOutLivestreams && v.IsLivestream)
			select new { v.Id, v.ChannelId }
		).ToListAsync(ct);

		var activeQueue = await db.DownloadQueue
			.Where(q => q.Status == Queued || q.Status == Downloading)
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
				Status = Queued,
				QueuedAt = DateTimeOffset.UtcNow
			});
		}

		await db.SaveChangesAsync(ct);
		logger?.LogInformation(
			"Enqueue missing on disk: added {Added} monitored video(s) (monitoredChannelVideos={Eligible})",
			toAdd.Count, candidates.Count);

		return toAdd.Count;
	}

	/// <summary>Process one queue item: run yt-dlp to download the video to the channel folder with the channel's quality profile.</summary>
	public static async Task<bool> ProcessOneAsync(TubeArrDbContext db, string executablePath, CancellationToken ct = default, ILogger? logger = null)
	{
		await RecoverOrphanedDownloadingItemsAsync(db, executablePath, ct, logger);

		// Peek the next id: nullable so an empty queue is not confused with Id==0.
		var nextId = await db.DownloadQueue
			.Where(q => q.Status == Queued)
			.OrderBy(q => q.Id)
			.Select(q => (int?)q.Id)
			.FirstOrDefaultAsync(ct);
		if (nextId is null)
			return false;

		var claimTime = DateTimeOffset.UtcNow;
		var claimedRows = await db.DownloadQueue
			.Where(q => q.Id == nextId.Value && q.Status == Queued)
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(q => q.Status, Downloading)
				.SetProperty(q => q.StartedAt, claimTime)
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
				item.Status = Failed;
				item.EstimatedSecondsRemaining = null;
				item.ErrorMessage = "Video or channel not found.";
				item.CompletedAt = DateTimeOffset.UtcNow;
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
				item.Status = Failed;
				item.EstimatedSecondsRemaining = null;
				item.ErrorMessage = "No quality profile exists. Create one in Settings â†’ Quality Profiles.";
				item.CompletedAt = DateTimeOffset.UtcNow;
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
				item.Status = Failed;
				item.EstimatedSecondsRemaining = null;
				item.ErrorMessage = "No root folder configured or channel folder could not be resolved.";
				item.CompletedAt = DateTimeOffset.UtcNow;
				await db.SaveChangesAsync(ct);
				logger?.LogWarning("Download failed: output directory unresolved queueId={QueueId} channelId={ChannelId} channelPath={ChannelPath} rootFolders={RootFolderCount}",
					item.Id, item.ChannelId, channel.Path, rootFolders.Count);
				return true;
			}

			Directory.CreateDirectory(outputDir);
			var builder = new YtDlpQualityProfileBuilder();
			var result = builder.Build(profile);
			var preferredOutputContainer = GetPreferredOutputContainer(profile);
			var ffmpegConfig = await db.FFmpegConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
			var ffmpegConfigured = ffmpegConfig is not null && ffmpegConfig.Enabled && !string.IsNullOrWhiteSpace(ffmpegConfig.ExecutablePath);
			var ffmpegLocation = NormalizeFfmpegLocation(ffmpegConfig?.ExecutablePath);
			var url = "https://www.youtube.com/watch?v=" + video.YoutubeVideoId;
			// yt-dlp output template: folder + filename. Use a safe pattern.
			var outputTemplate = Path.Combine(outputDir, "%(upload_date)s - %(title)s [%(id)s].%(ext)s");
			var args = new List<string>
			{
				"--no-warnings",
				"--encoding", "utf-8",
				"-f", result.Selector,
				"-S", result.Sort,
				"-o", outputTemplate,
				url
			};

			// Always pass configured FFmpeg location so yt-dlp can merge/remux/recode
			// even when ffmpeg is not globally available on PATH.
			if (!string.IsNullOrWhiteSpace(ffmpegLocation))
			{
				args.InsertRange(args.Count - 1, new[] { "--ffmpeg-location", ffmpegLocation! });
			}

			if (!string.IsNullOrWhiteSpace(preferredOutputContainer))
			{
				args.InsertRange(args.Count - 1, new[] { "--merge-output-format", preferredOutputContainer! });

				if (ffmpegConfigured)
				{
					if (CanKeepContainerAsIs(profile, preferredOutputContainer!))
					{
						args.InsertRange(args.Count - 1, new[] { "--remux-video", preferredOutputContainer! });
						logger?.LogInformation("Post-process action=remux queueId={QueueId} container={Container}", item.Id, preferredOutputContainer);
					}
					else
					{
						args.InsertRange(args.Count - 1, new[] { "--recode-video", preferredOutputContainer! });
						logger?.LogInformation("Post-process action=recode queueId={QueueId} container={Container}", item.Id, preferredOutputContainer);
					}
				}
			}

			var advancedArgs = GetAdvancedYtDlpArgs(profile);
			if (advancedArgs.Count > 0)
			{
				args.InsertRange(args.Count - 1, advancedArgs);
				logger?.LogInformation("Applying advanced yt-dlp args queueId={QueueId} argCount={ArgCount}", item.Id, advancedArgs.Count);
			}

			logger?.LogInformation("Running yt-dlp queueId={QueueId} profileId={ProfileId} profileName={ProfileName} outputDir={OutputDir} selector={Selector} sort={Sort} url={Url}",
				item.Id, result.ProfileId, result.ProfileName, outputDir, result.Selector, result.Sort, url);

			var lastProgressSaveAt = 0L;
			const int ProgressSaveIntervalMs = 500;
			async ValueTask OnProgress(YtDlpProcessRunner.DownloadProgressInfo progressInfo)
			{
				if (progressInfo.Progress.HasValue)
					item.Progress = progressInfo.Progress.Value;
				item.EstimatedSecondsRemaining = progressInfo.EstimatedSecondsRemaining;
				var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
				if (now - lastProgressSaveAt < ProgressSaveIntervalMs)
					return;
				lastProgressSaveAt = now;
				await db.SaveChangesAsync(ct);
			}

			var (_, stderr, exitCode) = await YtDlpProcessRunner.RunWithProgressAsync(
				executablePath,
				args,
				OnProgress,
				ct,
				DownloadTimeoutMs,
				logger,
				YtDlpProcessRunner.YtDlpProcessStyle.Download);
			if (exitCode != 0)
			{
				item.Status = Failed;
				item.ErrorMessage = string.IsNullOrWhiteSpace(stderr) ? $"yt-dlp exited with code {exitCode}" : stderr.Trim();
				item.EstimatedSecondsRemaining = null;
				logger?.LogWarning("yt-dlp failed queueId={QueueId} exitCode={ExitCode} stderr={Stderr}",
					item.Id, exitCode, Truncate(stderr, 2000));
			}
			else
			{
				// Find the output file. Our template includes [<id>] so this is deterministic.
				string? resolvedOutputPath = null;
				try
				{
					var expectedToken = $"[{video.YoutubeVideoId}]";
					var candidateFiles = Directory
						.EnumerateFiles(outputDir, "*", SearchOption.TopDirectoryOnly)
						.Where(p => Path.GetFileName(p).Contains(expectedToken, StringComparison.OrdinalIgnoreCase))
						.ToList();
					var nonIntermediateCandidates = candidateFiles
						.Where(p => !IsIntermediateYtDlpPartFile(p))
						.ToList();

					var preferredExt = string.IsNullOrWhiteSpace(preferredOutputContainer)
						? null
						: "." + preferredOutputContainer.Trim().TrimStart('.').ToLowerInvariant();

					if (!string.IsNullOrWhiteSpace(preferredExt))
					{
						resolvedOutputPath = nonIntermediateCandidates
							.Where(p => string.Equals(Path.GetExtension(p), preferredExt, StringComparison.OrdinalIgnoreCase))
							.OrderByDescending(p => File.GetLastWriteTimeUtc(p))
							.FirstOrDefault();
					}

					if (string.IsNullOrWhiteSpace(resolvedOutputPath))
					{
						resolvedOutputPath = nonIntermediateCandidates
							.OrderByDescending(p => File.GetLastWriteTimeUtc(p))
							.FirstOrDefault();
					}

					if (string.IsNullOrWhiteSpace(resolvedOutputPath) && !string.IsNullOrWhiteSpace(preferredExt))
					{
						resolvedOutputPath = candidateFiles
							.Where(p => string.Equals(Path.GetExtension(p), preferredExt, StringComparison.OrdinalIgnoreCase))
							.OrderByDescending(p => File.GetLastWriteTimeUtc(p))
							.FirstOrDefault();
					}

					if (string.IsNullOrWhiteSpace(resolvedOutputPath))
					{
						resolvedOutputPath = candidateFiles
							.OrderByDescending(p => File.GetLastWriteTimeUtc(p))
							.FirstOrDefault();
					}
				}
				catch
				{
					// ignore path scan issues; handled below
				}

				if (string.IsNullOrWhiteSpace(resolvedOutputPath) || !File.Exists(resolvedOutputPath))
				{
					item.Status = Failed;
					item.Progress = null;
					item.EstimatedSecondsRemaining = null;
					item.ErrorMessage = "yt-dlp reported success but no output file was found in the target folder. Check root folder / channel path settings and yt-dlp output.";
					logger?.LogWarning("yt-dlp success but no file found queueId={QueueId} outputDir={OutputDir} youtubeVideoId={YoutubeVideoId}", item.Id, outputDir, video.YoutubeVideoId);
				}
				else
				{
					var expectedToken = $"[{video.YoutubeVideoId}]";
					var allCandidates = Directory
						.EnumerateFiles(outputDir, "*", SearchOption.TopDirectoryOnly)
						.Where(p => Path.GetFileName(p).Contains(expectedToken, StringComparison.OrdinalIgnoreCase))
						.ToList();
					var keepLooksIntermediate = IsIntermediateYtDlpPartFile(resolvedOutputPath);

					if (keepLooksIntermediate)
					{
						item.Status = Failed;
						item.Progress = null;
						item.EstimatedSecondsRemaining = null;
						var baseMessage = ffmpegConfigured
							? "Download produced separate streams but no merged output file with audio. Check yt-dlp/ffmpeg logs and profile container constraints."
							: "Download produced separate streams but no merged output file with audio. Configure FFmpeg in Settings â†’ Tools to enable merging/remuxing.";
						var detail = string.IsNullOrWhiteSpace(stderr) ? null : Truncate(stderr, 1200);
						item.ErrorMessage = string.IsNullOrWhiteSpace(detail) ? baseMessage : $"{baseMessage} Details: {detail}";
						item.OutputPath = null;
						logger?.LogWarning("Download incomplete queueId={QueueId}: selected intermediate stream file keep={OutputPath} candidates={CandidateCount}",
							item.Id, resolvedOutputPath, allCandidates.Count);
						item.CompletedAt = DateTimeOffset.UtcNow;
						await db.SaveChangesAsync(ct);
						return true;
					}

					var expectsAudioTrack = IsLikelyVideoContainer(resolvedOutputPath) && !IsAudioExtractionRequested(args);
					if (expectsAudioTrack)
					{
						var (probeRan, hasAudio, probeError) = ProbeHasAudioStream(resolvedOutputPath, ffmpegLocation, logger);
						if (probeRan && !hasAudio)
						{
							item.Status = Failed;
							item.Progress = null;
							item.EstimatedSecondsRemaining = null;
							item.OutputPath = null;
							item.ErrorMessage = string.IsNullOrWhiteSpace(probeError)
								? "Downloaded video file has no audio stream. Check quality profile codec/container settings and FFmpeg post-processing options."
								: $"Downloaded video file has no audio stream. Details: {Truncate(probeError, 1200)}";
							logger?.LogWarning("Download failed queueId={QueueId}: output has no audio stream path={OutputPath}",
								item.Id, resolvedOutputPath);
							item.CompletedAt = DateTimeOffset.UtcNow;
							await db.SaveChangesAsync(ct);
							return true;
						}
					}

					// Ensure only the chosen output remains: delete any other media files for the same video id token.
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
						// best-effort cleanup; do not fail the download
					}

					item.Status = Completed;
					item.Progress = 1.0;
					item.EstimatedSecondsRemaining = 0;
					item.OutputPath = resolvedOutputPath;
					item.ErrorMessage = null;
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
					logger?.LogInformation("Download completed queueId={QueueId} outputPath={OutputPath}", item.Id, resolvedOutputPath);
				}
			}
		}
		catch (Exception ex) when (ex is not DbUpdateConcurrencyException)
		{
			item.Status = Failed;
			item.ErrorMessage = ex.Message ?? "Download failed.";
			item.EstimatedSecondsRemaining = null;
			logger?.LogError(ex, "Download exception queueId={QueueId} channelId={ChannelId} videoId={VideoId}", item.Id, item.ChannelId, item.VideoId);
		}

		item.CompletedAt = DateTimeOffset.UtcNow;
		if (item.Status == Completed)
		{
			db.DownloadHistory.Add(new DownloadHistoryEntity
			{
				ChannelId = item.ChannelId,
				VideoId = item.VideoId,
				PlaylistId = video?.PlaylistId,
				EventType = 3, // imported
				SourceTitle = video?.Title ?? $"Video {item.VideoId}",
				OutputPath = item.OutputPath,
				Message = null,
				DownloadId = item.Id.ToString(),
				Date = (item.CompletedAt ?? DateTimeOffset.UtcNow).UtcDateTime
			});

			// Completed items no longer remain in active queue.
			db.DownloadQueue.Remove(item);
		}

		await db.SaveChangesAsync(ct);
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
			.Where(q => q.Status == Downloading)
			.ToListAsync(ct);
		if (downloadingItems.Count == 0)
			return;

		var processName = Path.GetFileNameWithoutExtension(executablePath);
		var hasActiveDownloadProcess = false;
		try
		{
			hasActiveDownloadProcess = !string.IsNullOrWhiteSpace(processName) &&
				Process.GetProcessesByName(processName).Length > 0;
		}
		catch
		{
			// Process enumeration can fail in restricted environments; skip recovery in that case.
			return;
		}

		if (hasActiveDownloadProcess)
			return;

		var recoveryCutoff = DateTimeOffset.UtcNow.AddSeconds(-30);
		var recoveredCount = 0;

		foreach (var item in downloadingItems)
		{
			if (item.StartedAt.HasValue && item.StartedAt.Value > recoveryCutoff)
				continue;

			item.Status = Queued;
			item.StartedAt = null;
			item.CompletedAt = null;
			item.Progress = 0;
			item.EstimatedSecondsRemaining = null;
			item.ErrorMessage = null;
			recoveredCount++;
		}

		if (recoveredCount == 0)
			return;

		await db.SaveChangesAsync(ct);
		logger?.LogWarning(
			"Recovered {RecoveredCount} orphaned download queue item(s) with no active yt-dlp process",
			recoveredCount);
	}

	/// <summary>Run the queue processor loop until no queued items remain.</summary>
	public static async Task RunUntilEmptyAsync(
		TubeArrDbContext db,
		CancellationToken ct = default,
		ILogger? logger = null,
		Func<CancellationToken, Task>? onItemProcessed = null)
	{
		var executablePath = await YtDlpMetadataService.GetExecutablePathAsync(db, ct);
		if (string.IsNullOrWhiteSpace(executablePath))
		{
			logger?.LogWarning("Download queue not started: yt-dlp path is not configured. Set it in Settings â†’ Tools â†’ yt-dlp.");
			return;
		}
		_isProcessing = true;
		try
		{
			logger?.LogInformation("Download queue processor started");
			while (await ProcessOneAsync(db, executablePath, ct, logger))
			{
				if (onItemProcessed is not null)
					await onItemProcessed(ct);
				await Task.Delay(1000, ct);
			}
			logger?.LogInformation("Download queue processor finished (no queued items)");
		}
		finally
		{
			_isProcessing = false;
		}
	}

	static string Truncate(string? value, int max)
	{
		if (string.IsNullOrEmpty(value)) return "";
		var v = value.Trim();
		return v.Length <= max ? v : v.Substring(0, max) + "â€¦";
	}

	static string? GetOutputDirectory(
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

	static string? GetPreferredOutputContainer(QualityProfileEntity profile)
	{
		static string? FirstValue(string? json)
		{
			if (string.IsNullOrWhiteSpace(json))
				return null;
			try
			{
				var list = JsonSerializer.Deserialize<List<string>>(json);
				return list?.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
			}
			catch
			{
				return null;
			}
		}

		var preferred = FirstValue(profile.PreferredContainersJson);
		if (!string.IsNullOrWhiteSpace(preferred))
			return preferred.Trim().ToLowerInvariant();

		var allowed = FirstValue(profile.AllowedContainersJson);
		if (!string.IsNullOrWhiteSpace(allowed))
			return allowed.Trim().ToLowerInvariant();

		return null;
	}

	static bool IsIntermediateYtDlpPartFile(string path)
	{
		var fileName = Path.GetFileNameWithoutExtension(path);
		if (string.IsNullOrWhiteSpace(fileName))
			return false;

		// yt-dlp separate stream parts commonly end with ".f137", ".f251", etc.
		return Regex.IsMatch(fileName, @"\.f[0-9A-Za-z]+$", RegexOptions.IgnoreCase);
	}

	static bool CanKeepContainerAsIs(QualityProfileEntity profile, string container)
	{
		var target = (container ?? "").Trim().ToLowerInvariant();
		var allowedVideo = ParseCodecs(profile.AllowedVideoCodecsJson);
		var preferredVideo = ParseCodecs(profile.PreferredVideoCodecsJson);
		var allowedAudio = ParseCodecs(profile.AllowedAudioCodecsJson);
		var preferredAudio = ParseCodecs(profile.PreferredAudioCodecsJson);

		var effectiveVideo = preferredVideo.Count > 0 ? preferredVideo : allowedVideo;
		var effectiveAudio = preferredAudio.Count > 0 ? preferredAudio : allowedAudio;

		// Empty means "no restriction"; assume incompatible may appear so require recode for deterministic container output.
		if (effectiveVideo.Count == 0 && effectiveAudio.Count == 0)
			return false;

		static bool IsSubset(HashSet<string> source, params string[] allowed) =>
			source.Count > 0 && source.All(v => allowed.Contains(v, StringComparer.OrdinalIgnoreCase));

		return target switch
		{
			// MP4 remux-compatible codecs from the provided matrix.
			"mp4" => IsSubset(effectiveVideo, "AVC") && IsSubset(effectiveAudio, "MP4A"),

			// WebM remux-compatible codecs from the provided matrix.
			"webm" => IsSubset(effectiveVideo, "VP9", "AV1") && IsSubset(effectiveAudio, "OPUS"),

			// 3GP remux-compatible codecs (limited in current profile codec model).
			"3gp" => IsSubset(effectiveVideo, "AVC") && IsSubset(effectiveAudio, "MP4A"),

			// M4A is audio-only in this app model; remux only if audio codec is MP4A.
			"m4a" => IsSubset(effectiveAudio, "MP4A"),

			// Unknown/other containers default to recode for safer compatibility.
			_ => false
		};
	}

	static HashSet<string> ParseCodecs(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
			return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			var list = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
			return new HashSet<string>(
				list.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()),
				StringComparer.OrdinalIgnoreCase
			);
		}
		catch
		{
			return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		}
	}

	static string? NormalizeFfmpegLocation(string? configuredPath)
	{
		if (string.IsNullOrWhiteSpace(configuredPath))
			return null;

		var path = configuredPath.Trim().Trim('"');
		if (string.IsNullOrWhiteSpace(path))
			return null;

		return path;
	}

	static bool IsAudioExtractionRequested(List<string> args)
	{
		return args.Any(a => string.Equals(a, "-x", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(a, "--extract-audio", StringComparison.OrdinalIgnoreCase));
	}

	static bool IsLikelyVideoContainer(string path)
	{
		var ext = Path.GetExtension(path)?.TrimStart('.').ToLowerInvariant();
		return ext switch
		{
			"mp4" or "webm" or "mkv" or "avi" or "mov" or "m4v" or "flv" or "wmv" or "mpg" or "mpeg" or "ts" or "3gp" => true,
			_ => false
		};
	}

	static (bool probeRan, bool hasAudio, string? error) ProbeHasAudioStream(string mediaPath, string? ffmpegLocation, ILogger? logger)
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

	static string? ResolveFfprobePath(string? ffmpegLocation)
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

	static List<string> GetAdvancedYtDlpArgs(QualityProfileEntity profile)
	{
		var result = new List<string>();
		AppendBucket(result, profile.SelectionArgs);
		AppendBucket(result, profile.MuxArgs);
		AppendBucket(result, profile.AudioArgs);
		AppendBucket(result, profile.TimeArgs);
		AppendBucket(result, profile.SubtitleArgs);
		AppendBucket(result, profile.ThumbnailArgs);
		AppendBucket(result, profile.MetadataArgs);
		AppendBucket(result, profile.CleanupArgs);
		AppendBucket(result, profile.SponsorblockArgs);
		return result;
	}

	static void AppendBucket(List<string> target, string? bucket)
	{
		if (string.IsNullOrWhiteSpace(bucket))
			return;
		target.AddRange(SplitCliArgs(bucket));
	}

	static List<string> SplitCliArgs(string input)
	{
		var args = new List<string>();
		if (string.IsNullOrWhiteSpace(input))
			return args;

		var current = new System.Text.StringBuilder();
		var inSingleQuote = false;
		var inDoubleQuote = false;

		for (var i = 0; i < input.Length; i++)
		{
			var ch = input[i];
			if (ch == '\'' && !inDoubleQuote)
			{
				inSingleQuote = !inSingleQuote;
				continue;
			}
			if (ch == '"' && !inSingleQuote)
			{
				inDoubleQuote = !inDoubleQuote;
				continue;
			}

			if (!inSingleQuote && !inDoubleQuote && char.IsWhiteSpace(ch))
			{
				if (current.Length > 0)
				{
					args.Add(current.ToString());
					current.Clear();
				}
				continue;
			}

			current.Append(ch);
		}

		if (current.Length > 0)
		{
			args.Add(current.ToString());
		}

		return args;
	}
}
