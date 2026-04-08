using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Data;
using TubeArr.Backend.Integrations.Slskd;
using TubeArr.Backend.Media.Nfo;

namespace TubeArr.Backend;

public static partial class DownloadQueueProcessor
{
	/// <summary>Runs slskd search/transfer/compliance when acquisition order or fallback demands it. Returns true if this worker tick finished the item or parked it for manual UI (no yt-dlp on this tick).</summary>
	internal static async Task<bool> TrySlskdAcquisitionFlowAsync(
		TubeArrDbContext db,
		DownloadQueueEntity item,
		VideoEntity video,
		ChannelEntity channel,
		QualityProfileEntity profile,
		string outputDir,
		string contentRoot,
		string youtubeVideoIdForYtDlp,
		string? ytDlpExecutablePath,
		SlskdHttpClient slskdHttp,
		SlskdConfigEntity slskdCfg,
		NamingConfigEntity naming,
		PlaylistEntity? playlist,
		int? primaryPlaylistId,
		int? seasonForPlaylistFolder,
		IReadOnlyList<RootFolderEntity> rootFolders,
		bool useCustomNfos,
		bool exportLibraryThumbnails,
		IHttpClientFactory httpClientFactory,
		FFmpegConfigEntity? ffmpegConfig,
		CancellationToken ct,
		ILogger? logger)
	{
		var order = AcquisitionOrderKindExtensions.ParseOrDefault(slskdCfg.AcquisitionOrder);
		var ext = ExternalAcquisitionJsonSerializer.TryDeserialize(item.ExternalAcquisitionJson) ?? new ExternalAcquisitionState();
		var slskdReady = slskdCfg.Enabled
			&& !string.IsNullOrWhiteSpace(slskdCfg.BaseUrl)
			&& !string.IsNullOrWhiteSpace(slskdCfg.ApiKey);

		var ytReady = !string.IsNullOrWhiteSpace(ytDlpExecutablePath)
			&& await db.YtDlpConfig.AsNoTracking().AnyAsync(c => c.Id == 1 && c.Enabled, ct);

		if (ext.Phase == ExternalAcquisitionPhases.PendingYtDlp)
			return false;

		var phaseEmpty = string.IsNullOrEmpty(ext.Phase) || ext.Phase == ExternalAcquisitionPhases.None;
		var slskdPrimaryNew = phaseEmpty && (order == AcquisitionOrderKind.SlskdFirst || order == AcquisitionOrderKind.SlskdOnly);
		var slskdContinue = !phaseEmpty
			&& ext.ActiveProvider == "slskd"
			&& ext.Phase is not ExternalAcquisitionPhases.Failed
			&& ext.Phase != ExternalAcquisitionPhases.Done;
		var shouldRunSlskd = slskdReady && (slskdPrimaryNew || slskdContinue);

		if (order == AcquisitionOrderKind.SlskdOnly && !slskdReady)
		{
			item.LastError = "slskd-only order: enable slskd and set base URL + API key in Settings.";
			item.Status = QueueJobStatuses.Failed;
			item.ExternalWorkPending = 0;
			return true;
		}

		if (!shouldRunSlskd)
			return false;

		using var http = slskdHttp.CreateClient(slskdCfg.BaseUrl.Trim(), slskdCfg.ApiKey);

		if (slskdPrimaryNew && phaseEmpty)
		{
			ext.ActiveProvider = "slskd";
			ext.Phase = ExternalAcquisitionPhases.PendingSearch;
			ext.ResumeProcessor = true;
			item.ExternalWorkPending = 1;
			item.AcquisitionMethodsJson = AcquisitionMethodsJsonHelper.MergeOne(item.AcquisitionMethodsJson, AcquisitionMethodIds.Slskd);
		}

		if (ext.Phase == ExternalAcquisitionPhases.PendingSearch)
		{
			var stages = SlskdQueryGenerator.BuildStages(video, channel);
			var merged = new List<ExternalDownloadCandidateDto>();
			var stageIdx = 0;
			foreach (var q in stages)
			{
				var body = new Dictionary<string, object?>
				{
					["searchText"] = q,
					["searchTimeout"] = Math.Clamp(slskdCfg.SearchTimeoutSeconds, 5, 600)
				};
				var post = await SlskdHttpClient.PostJsonAsync(http, "api/v0/searches", body, ct);
				if (!post.Ok)
				{
					ext.LastSlskdError = post.Error ?? $"HTTP {post.StatusCode}";
					logger?.LogWarning("slskd search POST failed queueId={QueueId} {Err}", item.Id, ext.LastSlskdError);
					stageIdx++;
					continue;
				}

				if (!SlskdSearchResultParser.TryParseSearchId(post.Body ?? "", out var searchId))
				{
					stageIdx++;
					continue;
				}

				ext.SearchId = searchId;
				var deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Clamp(slskdCfg.SearchTimeoutSeconds, 5, 600) + 10);
				while (DateTimeOffset.UtcNow < deadline && !ct.IsCancellationRequested)
				{
					var get = await SlskdHttpClient.GetAsync(http, $"api/v0/searches/{searchId:N}?includeResponses=true", ct);
					if (!get.Ok || string.IsNullOrEmpty(get.Body))
					{
						await Task.Delay(500, ct);
						continue;
					}

					if (!SlskdSearchResultParser.TryParseSearchComplete(get.Body, out var complete, out var files))
					{
						await Task.Delay(500, ct);
						continue;
					}

					foreach (var f in files)
					{
						var dto = new ExternalDownloadCandidateDto
						{
							Id = Guid.NewGuid().ToString("N"),
							Username = f.Username,
							Filename = f.Filename,
							Size = f.Size,
							Extension = string.IsNullOrEmpty(f.Extension) ? Path.GetExtension(f.Filename).TrimStart('.') : f.Extension,
							DurationSeconds = f.DurationSeconds,
							BitrateKbps = f.BitrateKbps
						};
						SlskdCandidateScorer.ScoreAndAttach(dto, video, channel, q, stageIdx);
						merged.Add(dto);
					}

					if (complete)
						break;
					await Task.Delay(500, ct);
				}

				stageIdx++;
				var highEnough = merged.Any(c => c.MatchScore >= slskdCfg.AutoPickMinScore && c.Confidence == "high");
				if (highEnough && !slskdCfg.ManualReviewOnly)
					break;
			}

			merged = merged.OrderByDescending(c => c.MatchScore).Take(Math.Max(1, slskdCfg.MaxCandidatesStored)).ToList();
			ext.Candidates = merged;
			ext.Phase = ExternalAcquisitionPhases.CandidatesReady;

			if (merged.Count == 0)
			{
				item.LastError = "slskd: no files matched search.";
				item.ExternalWorkPending = 0;
				ext.Phase = ExternalAcquisitionPhases.Failed;
				if (TryPrepareYtDlpFallbackAfterSlskdFailure(order, slskdCfg, ytReady, ext, item))
				{
					item.Status = QueueJobStatuses.Running;
					item.LastError = null;
				}
				else
					item.Status = QueueJobStatuses.Failed;

				item.ExternalAcquisitionJson = ExternalAcquisitionJsonSerializer.Serialize(ext);
				return true;
			}

			ExternalDownloadCandidateDto? pick = null;
			if (!slskdCfg.ManualReviewOnly)
				pick = merged.FirstOrDefault(c => c.MatchScore >= slskdCfg.AutoPickMinScore && c.Confidence == "high");

			if (pick is null)
			{
				ext.Phase = ExternalAcquisitionPhases.AwaitingManualPick;
				item.ExternalWorkPending = 0;
				item.Progress = null;
				item.LastError = "slskd: select a candidate in the video panel (or enable auto-pick).";
				item.ExternalAcquisitionJson = ExternalAcquisitionJsonSerializer.Serialize(ext);
				logger?.LogInformation("slskd awaiting manual pick queueId={QueueId} candidates={Count}", item.Id, merged.Count);
				return true;
			}

			ext.ChosenCandidate = pick;
			ext.Phase = ExternalAcquisitionPhases.QueuedTransfer;
		}

		if (ext.Phase == ExternalAcquisitionPhases.QueuedTransfer && ext.ChosenCandidate is { } chosen)
		{
			var encUser = Uri.EscapeDataString(chosen.Username);
			var payload = new[] { new { filename = chosen.Filename, size = chosen.Size } };
			var post = await SlskdHttpClient.PostJsonAsync(http, $"api/v0/transfers/downloads/{encUser}", payload, ct);
			if (!post.Ok || !SlskdTransferParser.TryParseEnqueueResponse(post.Body ?? "", out var tid))
			{
				ext.Phase = ExternalAcquisitionPhases.Failed;
				item.LastError = "slskd: could not enqueue download: " + (post.Error ?? post.Body ?? "unknown");
				item.ExternalWorkPending = 0;
				if (TryPrepareYtDlpFallbackAfterSlskdFailure(order, slskdCfg, ytReady, ext, item))
				{
					item.Status = QueueJobStatuses.Running;
					item.LastError = null;
				}
				else
					item.Status = QueueJobStatuses.Failed;

				item.ExternalAcquisitionJson = ExternalAcquisitionJsonSerializer.Serialize(ext);
				return true;
			}

			ext.TransferUsername = chosen.Username;
			ext.TransferId = tid;
			ext.Phase = ExternalAcquisitionPhases.Transferring;
			item.ExternalWorkPending = 1;
			item.Progress = 0;
		}

		if (ext.Phase == ExternalAcquisitionPhases.Transferring && ext.TransferId is { } trId && !string.IsNullOrEmpty(ext.TransferUsername))
		{
			var encUser = Uri.EscapeDataString(ext.TransferUsername);
			var get = await SlskdHttpClient.GetAsync(http, $"api/v0/transfers/downloads/{encUser}/{trId:N}", ct);
			if (!get.Ok || string.IsNullOrEmpty(get.Body))
			{
				item.ExternalWorkPending = 1;
				item.ExternalAcquisitionJson = ExternalAcquisitionJsonSerializer.Serialize(ext);
				return true;
			}

			if (SlskdTransferParser.IsTerminalFailure(get.Body, out var failMsg))
			{
				ext.Phase = ExternalAcquisitionPhases.Failed;
				item.LastError = "slskd transfer failed: " + (failMsg ?? "unknown");
				item.ExternalWorkPending = 0;
				if (TryPrepareYtDlpFallbackAfterSlskdFailure(order, slskdCfg, ytReady, ext, item))
				{
					item.Status = QueueJobStatuses.Running;
					item.LastError = null;
				}
				else
					item.Status = QueueJobStatuses.Failed;

				item.ExternalAcquisitionJson = ExternalAcquisitionJsonSerializer.Serialize(ext);
				return true;
			}

			if (SlskdTransferParser.IsTransferCompleted(get.Body, out var pct, out var tfname, out _))
			{
				ext.Phase = ExternalAcquisitionPhases.DownloadedLocal;
				var local = SlskdLocalPathResolver.TryResolveCompletedPath(
					string.IsNullOrWhiteSpace(slskdCfg.LocalDownloadsPath) ? null : slskdCfg.LocalDownloadsPath.Trim(),
					ext.ChosenCandidate?.Filename ?? "",
					tfname);
				if (string.IsNullOrEmpty(local) || !File.Exists(local))
				{
					item.LastError = "slskd: completed transfer but file not found on TubeArr host. Set Local downloads path to slskd's downloads directory (shared path).";
					item.Status = QueueJobStatuses.Failed;
					item.ExternalWorkPending = 0;
					ext.Phase = ExternalAcquisitionPhases.Failed;
					item.ExternalAcquisitionJson = ExternalAcquisitionJsonSerializer.Serialize(ext);
					return true;
				}

				var extTarget = Path.GetExtension(local);
				if (string.IsNullOrEmpty(extTarget))
					extTarget = ".mkv";
				var staging = Path.Combine(outputDir, $"slskd-staging [{youtubeVideoIdForYtDlp}]{extTarget}");
				try
				{
					Directory.CreateDirectory(outputDir);
					File.Copy(local, staging, overwrite: true);
				}
				catch (Exception ex)
				{
					item.LastError = "slskd: could not copy file to channel folder: " + ex.Message;
					item.Status = QueueJobStatuses.Failed;
					item.ExternalWorkPending = 0;
					ext.Phase = ExternalAcquisitionPhases.Failed;
					item.ExternalAcquisitionJson = ExternalAcquisitionJsonSerializer.Serialize(ext);
					return true;
				}

				var ffmpegPath = ffmpegConfig is { Enabled: true } ? ffmpegConfig.ExecutablePath : null;
				var decision = PostDownloadCompliance.Evaluate(staging, profile, slskdCfg, ffmpegPath, out var reason);
				decision = PostDownloadCompliance.ApplyTranscodeReviewPolicy(decision, slskdCfg);
				if (decision == PostDownloadDecision.Rejected)
				{
					item.LastError = "slskd import rejected: " + (reason ?? "quality check");
					item.Status = QueueJobStatuses.Failed;
					item.ExternalWorkPending = 0;
					ext.Phase = ExternalAcquisitionPhases.Failed;
					ext.ComplianceSummary = reason;
					item.ExternalAcquisitionJson = ExternalAcquisitionJsonSerializer.Serialize(ext);
					try { File.Delete(staging); } catch { /* ignore */ }
					return true;
				}

				if (decision == PostDownloadDecision.ManualReview)
				{
					ext.Phase = ExternalAcquisitionPhases.ManualReview;
					item.ExternalWorkPending = 0;
					item.LastError = "slskd: manual review required (transcode/remux). " + (reason ?? "");
					item.ExternalAcquisitionJson = ExternalAcquisitionJsonSerializer.Serialize(ext);
					return true;
				}

				var working = staging;
				if (decision == PostDownloadDecision.RemuxWithCopy && !string.IsNullOrWhiteSpace(ffmpegPath))
				{
					var remuxOut = Path.Combine(outputDir, $"slskd-remux [{youtubeVideoIdForYtDlp}].mp4");
					var (ok, _, err) = await FfmpegPostDownloadRunner.RemuxCopyAsync(ffmpegPath.Trim(), staging, remuxOut, ct, logger);
					if (ok)
					{
						try { if (!slskdCfg.KeepOriginalAfterTranscode) File.Delete(staging); } catch { /* ignore */ }
						working = remuxOut;
					}
					else
					{
						item.LastError = "slskd remux failed: " + err;
						item.Status = QueueJobStatuses.Failed;
						item.ExternalWorkPending = 0;
						ext.Phase = ExternalAcquisitionPhases.Failed;
						item.ExternalAcquisitionJson = ExternalAcquisitionJsonSerializer.Serialize(ext);
						return true;
					}
				}
				else if (decision == PostDownloadDecision.Transcode && !string.IsNullOrWhiteSpace(ffmpegPath))
				{
					var maxH = profile.MaxHeight ?? 1080;
					var tcOut = Path.Combine(outputDir, $"slskd-transcode [{youtubeVideoIdForYtDlp}].mp4");
					var (ok, _, err) = await FfmpegPostDownloadRunner.TranscodeToMaxHeightAsync(ffmpegPath.Trim(), staging, tcOut, maxH, ct, logger);
					if (ok)
					{
						try { if (!slskdCfg.KeepOriginalAfterTranscode) File.Delete(staging); } catch { /* ignore */ }
						working = tcOut;
					}
					else
					{
						item.LastError = "slskd transcode failed: " + err;
						item.Status = QueueJobStatuses.Failed;
						item.ExternalWorkPending = 0;
						ext.Phase = ExternalAcquisitionPhases.Failed;
						item.ExternalAcquisitionJson = ExternalAcquisitionJsonSerializer.Serialize(ext);
						return true;
					}
				}

				var expectedToken = $"[{youtubeVideoIdForYtDlp}]";
				var workingOutputPath = await ApplyUserVideoNamingToDownloadedFileAsync(
					db,
					working,
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
						try { File.Delete(full); } catch { /* ignore */ }
					}
				}
				catch { /* ignore */ }

				item.Status = QueueJobStatuses.Completed;
				item.Progress = 1.0;
				item.EstimatedSecondsRemaining = 0;
				item.OutputPath = workingOutputPath;
				item.LastError = null;
				item.ExternalWorkPending = 0;
				ext.Phase = ExternalAcquisitionPhases.Done;
				ext.ComplianceSummary = decision.ToString();
				item.ExternalAcquisitionJson = ExternalAcquisitionJsonSerializer.Serialize(ext);

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
					logger?.LogWarning(ex, "Failed to upsert video file tracking queueId={QueueId}", item.Id);
				}

				if (useCustomNfos)
				{
					try
					{
						await NfoLibraryExporter.WriteForCompletedDownloadAsync(
							db, channel, video, playlist, primaryPlaylistId, workingOutputPath, naming, rootFolders.ToList(), ct);
					}
					catch (Exception ex)
					{
						logger?.LogWarning(ex, "NFO export failed queueId={QueueId}", item.Id);
					}
				}

				if (exportLibraryThumbnails)
				{
					try
					{
						await PlexLibraryArtworkExporter.WriteForCompletedDownloadAsync(
							db, channel, video, playlist, primaryPlaylistId, workingOutputPath, naming, rootFolders.ToList(), httpClientFactory, logger, ct);
					}
					catch (Exception ex)
					{
						logger?.LogWarning(ex, "Library thumbnail export failed queueId={QueueId}", item.Id);
					}
				}

				try
				{
					await PlexNotificationRefresher.TryAfterVideoFileImportedAsync(
						db, httpClientFactory, SystemMiscEndpoints.GetNotificationSchemaJson(), logger, ct);
				}
				catch (Exception ex)
				{
					logger?.LogDebug(ex, "Plex notification refresh skipped queueId={QueueId}", item.Id);
				}

				logger?.LogInformation("slskd download completed queueId={QueueId} path={Path}", item.Id, workingOutputPath);
				return true;
			}

			item.Progress = pct ?? item.Progress;
			item.ExternalWorkPending = 1;
			item.ExternalAcquisitionJson = ExternalAcquisitionJsonSerializer.Serialize(ext);
			return true;
		}

		item.ExternalAcquisitionJson = ExternalAcquisitionJsonSerializer.Serialize(ext);
		return true;
	}

	static bool TryPrepareYtDlpFallbackAfterSlskdFailure(
		AcquisitionOrderKind order,
		SlskdConfigEntity cfg,
		bool ytReady,
		ExternalAcquisitionState ext,
		DownloadQueueEntity item)
	{
		if (order != AcquisitionOrderKind.SlskdFirst || !cfg.FallbackToYtDlpOnSlskdFailure || !ytReady || ext.FallbackUsed)
			return false;
		ext.FallbackUsed = true;
		ext.PrimaryFailureSummary = item.LastError;
		ext.ActiveProvider = "yt-dlp";
		ext.Phase = ExternalAcquisitionPhases.PendingYtDlp;
		ext.ResumeProcessor = false;
		item.ExternalAcquisitionJson = ExternalAcquisitionJsonSerializer.Serialize(ext);
		item.ExternalWorkPending = 0;
		return true;
	}

	internal static bool TryPrepareSlskdFallbackAfterYtDlpFailure(
		DownloadQueueEntity item,
		ExternalAcquisitionState ext,
		SlskdConfigEntity cfg,
		AcquisitionOrderKind order,
		bool slskdReady,
		string? lastYtError)
	{
		if (order != AcquisitionOrderKind.YtDlpFirst || !cfg.FallbackToSlskdOnYtDlpFailure || !slskdReady || ext.FallbackUsed)
			return false;
		ext.FallbackUsed = true;
		ext.PrimaryFailureSummary = lastYtError;
		ext.ActiveProvider = "slskd";
		ext.Phase = ExternalAcquisitionPhases.PendingSearch;
		ext.Candidates.Clear();
		ext.ChosenCandidate = null;
		ext.SearchId = null;
		ext.TransferId = null;
		ext.TransferUsername = null;
		item.Status = QueueJobStatuses.Running;
		item.Progress = 0;
		item.EstimatedSecondsRemaining = null;
		item.LastError = null;
		item.ExternalWorkPending = 1;
		item.ExternalAcquisitionJson = ExternalAcquisitionJsonSerializer.Serialize(ext);
		item.AcquisitionMethodsJson = AcquisitionMethodsJsonHelper.MergeOne(item.AcquisitionMethodsJson, AcquisitionMethodIds.Slskd);
		return true;
	}
}
