using System.Text;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.QualityProfile;

namespace TubeArr.Backend.DownloadBackends;

public sealed class YtDlpDownloadBackend : IDownloadBackend
{
	public DownloadBackendKind Kind => DownloadBackendKind.YtDlp;

	const int DownloadTimeoutMs = 600_000;
	const bool AutomaticBrowserCookieRefreshOnAuthFailure = false;

	readonly ILogger<YtDlpDownloadBackend> _logger;

	public YtDlpDownloadBackend(ILogger<YtDlpDownloadBackend> logger)
	{
		_logger = logger;
	}

	public async Task<DownloadAttemptResult> DownloadAsync(DownloadRequest request, CancellationToken cancellationToken)
	{
		if (request.YtDlpProfileHints is null)
		{
			return new DownloadAttemptResult
			{
				Success = false,
				SelectedBackend = Kind,
				FailureStage = DownloadFailureStage.InvalidConfiguration,
				StructuredErrorCode = "MissingYtDlpProfileHints",
				UserMessage = "Internal error: yt-dlp profile hints missing."
			};
		}

		var hints = request.YtDlpProfileHints;
		var url = request.WatchUrl;
		var rawProfileConfig = request.RawQualityProfileConfigText;

		{
			var cookiesPath = request.CookiesPath;
			string? resolvedCookiesPath = request.ResolvedCookiesPath;
			var cookiesFileReadable = request.CookiesFileReadable;

			var sanitizedProfileConfig = QualityProfileYtDlpConfigContent.SanitizeConfigTextForYtDlp(rawProfileConfig);
			if (!string.IsNullOrWhiteSpace(cookiesPath))
				sanitizedProfileConfig = QualityProfileYtDlpConfigContent.RemoveCookiesDirectivesFromConfigText(sanitizedProfileConfig);

			if (!string.Equals(rawProfileConfig, sanitizedProfileConfig, StringComparison.Ordinal))
			{
				_logger.LogInformation(
					"Adjusted quality profile config text for download queueId={QueueId} profileId={ProfileId}",
					request.QueueId, hints.ProfileId);
			}

			var ffmpegLocation = DownloadQueueProcessor.NormalizeFfmpegLocation(request.FfmpegExecutablePath);
			var mergedConfigBody = QualityProfileYtDlpConfigContent.BuildMergedDownloadConfigBody(
				sanitizedProfileConfig,
				request.OutputTemplate,
				ffmpegLocation,
				netscapeCookiesPath: null);
			var mergedConfigPath = Path.Combine(Path.GetTempPath(), $"tubearr-ytdlp-{hints.ProfileId}-{Guid.NewGuid():N}.conf");
			await File.WriteAllTextAsync(mergedConfigPath, mergedConfigBody, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);

			var args = new List<string> { "--ignore-config", "--continue" };
			var appendedCookiesToArgv = YtDlpCommandBuilder.TryAppendYoutubeCookiesFile(args, cookiesPath);
			args.Add("--config-locations");
			args.Add(mergedConfigPath);
			args.Add(url);

			_logger.LogInformation(
				"yt-dlp auth queueId={QueueId} cookiesPathConfigured={Configured} cookiesFileReadable={Readable} resolvedCookiesPath={ResolvedPath}",
				request.QueueId,
				!string.IsNullOrWhiteSpace(cookiesPath),
				cookiesFileReadable,
				resolvedCookiesPath ?? "");

			_logger.LogInformation(
				"yt-dlp cookie injection queueId={QueueId} cookiesOnArgvOnly=True argvAppendedCookies={ArgvCookies}",
				request.QueueId, appendedCookiesToArgv);

			_logger.LogInformation("yt-dlp invocation queueId={QueueId} {Invocation}",
				request.QueueId, DownloadQueueProcessor.FormatYtDlpInvocationForLog(request.YtDlpExecutablePath, args));

			_logger.LogInformation("Running yt-dlp queueId={QueueId} profileId={ProfileId} profileName={ProfileName} outputDir={OutputDir} selector={Selector} sort={Sort} url={Url}",
				request.QueueId, hints.ProfileId, hints.ProfileName, request.OutputDirectory, hints.Selector, hints.Sort, url);

			async ValueTask OnProgress(YtDlpProcessRunner.DownloadProgressInfo p)
			{
				if (request.OnProgress is not null)
					await request.OnProgress(new DownloadProgressInfo(p.Progress, p.EstimatedSecondsRemaining));
			}

			var (_, stderr, exitCode) = await YtDlpProcessRunner.RunWithProgressAsync(
				request.YtDlpExecutablePath,
				args,
				OnProgress,
				cancellationToken,
				DownloadTimeoutMs,
				_logger,
				YtDlpProcessRunner.YtDlpProcessStyle.Download);
			try
			{
				File.Delete(mergedConfigPath);
			}
			catch
			{
				/* best-effort */
			}

			if (exitCode != 0)
			{
				if (AutomaticBrowserCookieRefreshOnAuthFailure
					&& request.BrowserCookieService is not null
					&& DownloadQueueProcessor.LooksLikeYtDlpCookieAuthFailure(stderr))
				{
					_logger.LogWarning(
						"yt-dlp reported cookie/auth failure (automatic refresh is disabled in this build) queueId={QueueId} exitCode={ExitCode} stderr={Stderr}",
						request.QueueId, exitCode, DownloadQueueProcessor.Truncate(stderr, 2000));
				}

				_logger.LogWarning("yt-dlp failed queueId={QueueId} exitCode={ExitCode} stderr={Stderr}",
					request.QueueId, exitCode, DownloadQueueProcessor.Truncate(stderr, 2000));
				return new DownloadAttemptResult
				{
					Success = false,
					SelectedBackend = Kind,
					FailureStage = DownloadFailureStage.YtDlpProcessFailed,
					StructuredErrorCode = $"YtDlpExit{exitCode}",
					UserMessage = string.IsNullOrWhiteSpace(stderr) ? $"yt-dlp exited with code {exitCode}" : stderr.Trim(),
					DiagnosticDetails = DownloadQueueProcessor.Truncate(stderr, 4000)
				};
			}

			var youtubeVideoIdForYtDlp = DownloadQueueProcessor.SanitizeYoutubeVideoIdForWatchUrl(request.YoutubeVideoId);
			string? resolvedOutputPath = null;
			try
			{
				var expectedToken = $"[{youtubeVideoIdForYtDlp}]";
				var candidateFiles = Directory
					.EnumerateFiles(request.OutputDirectory, "*", SearchOption.TopDirectoryOnly)
					.Where(p => Path.GetFileName(p).Contains(expectedToken, StringComparison.OrdinalIgnoreCase))
					.ToList();
				var nonIntermediateCandidates = candidateFiles
					.Where(p => !DownloadQueueProcessor.IsIntermediateYtDlpPartFile(p))
					.ToList();

				var preferredExt = string.IsNullOrWhiteSpace(request.PreferredOutputContainer)
					? null
					: "." + request.PreferredOutputContainer.Trim().TrimStart('.').ToLowerInvariant();

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
				_logger.LogWarning("yt-dlp success but no file found queueId={QueueId} outputDir={OutputDir} youtubeVideoId={YoutubeVideoId}", request.QueueId, request.OutputDirectory, youtubeVideoIdForYtDlp);
				return new DownloadAttemptResult
				{
					Success = false,
					SelectedBackend = Kind,
					FailureStage = DownloadFailureStage.OutputNotFound,
					StructuredErrorCode = "OutputFileMissing",
					UserMessage = "yt-dlp reported success but no output file was found in the target folder. Check root folder / channel path settings and yt-dlp output.",
					DiagnosticDetails = DownloadQueueProcessor.Truncate(stderr, 2000)
				};
			}

			var expectedToken2 = $"[{youtubeVideoIdForYtDlp}]";
			var allCandidates = Directory
				.EnumerateFiles(request.OutputDirectory, "*", SearchOption.TopDirectoryOnly)
				.Where(p => Path.GetFileName(p).Contains(expectedToken2, StringComparison.OrdinalIgnoreCase))
				.ToList();
			var keepLooksIntermediate = DownloadQueueProcessor.IsIntermediateYtDlpPartFile(resolvedOutputPath);

			if (keepLooksIntermediate)
			{
				var baseMessage = request.FfmpegConfigured
					? "Download produced separate streams but no merged output file with audio. Check yt-dlp/ffmpeg logs and profile container constraints."
					: "Download produced separate streams but no merged output file with audio. Configure FFmpeg in Settings to enable merging/remuxing.";
				var detail = string.IsNullOrWhiteSpace(stderr) ? null : DownloadQueueProcessor.Truncate(stderr, 1200);
				var msg = string.IsNullOrWhiteSpace(detail) ? baseMessage : $"{baseMessage} Details: {detail}";
				return new DownloadAttemptResult
				{
					Success = false,
					SelectedBackend = Kind,
					FailureStage = DownloadFailureStage.OutputNotFound,
					StructuredErrorCode = "IntermediateStreamOnly",
					UserMessage = msg,
					DiagnosticDetails = detail
				};
			}

			var expectsAudioTrack = DownloadQueueProcessor.IsLikelyVideoContainer(resolvedOutputPath) &&
				!DownloadQueueProcessor.IsAudioExtractionRequested(args, request.QualityProfileConfigPath);
			if (expectsAudioTrack)
			{
				var (probeRan, hasAudio, probeError) = DownloadQueueProcessor.ProbeHasAudioStream(resolvedOutputPath, ffmpegLocation, _logger);
				if (probeRan && !hasAudio)
				{
					return new DownloadAttemptResult
					{
						Success = false,
						SelectedBackend = Kind,
						FailureStage = DownloadFailureStage.YtDlpProcessFailed,
						StructuredErrorCode = "NoAudioInVideo",
						UserMessage = string.IsNullOrWhiteSpace(probeError)
							? "Downloaded video file has no audio stream. Check quality profile codec/container settings and FFmpeg post-processing options."
							: $"Downloaded video file has no audio stream. Details: {DownloadQueueProcessor.Truncate(probeError, 1200)}",
						DiagnosticDetails = probeError
					};
				}
			}

			_logger.LogInformation("yt-dlp download finished queueId={QueueId} outputPath={OutputPath}", request.QueueId, resolvedOutputPath);
			return new DownloadAttemptResult
			{
				Success = true,
				SelectedBackend = Kind,
				FailureStage = DownloadFailureStage.None,
				PrimaryOutputPath = resolvedOutputPath,
				OutputFiles = new[] { resolvedOutputPath },
				ChosenFormatSummary = $"selector={hints.Selector}; sort={hints.Sort}"
			};
		}
	}
}
