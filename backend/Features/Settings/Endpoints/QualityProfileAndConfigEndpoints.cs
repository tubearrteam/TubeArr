using System.Net.Http;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;
using TubeArr.Backend.DownloadBackends;
using TubeArr.Backend.QualityProfile;
using TubeArr.Shared.Infrastructure;

namespace TubeArr.Backend;

internal static partial class QualityProfileAndConfigEndpoints
{
	internal static void Map(RouteGroupBuilder api)
	{
	// ---- Quality profiles (YouTube tokenized) ----
	static object ToQualityProfileResource(QualityProfileEntity p, string contentRoot) => new
	{
		id = p.Id,
		isReadOnly = p.Id == QualityProfileEntity.BuiltInDefaultProfileId,
		name = p.Name,
		enabled = p.Enabled,
		maxHeight = p.MaxHeight,
		minHeight = p.MinHeight,
		minFps = p.MinFps,
		maxFps = p.MaxFps,
		allowHdr = p.AllowHdr,
		allowSdr = p.AllowSdr,
		allowedVideoCodecs = string.IsNullOrEmpty(p.AllowedVideoCodecsJson) ? Array.Empty<string>() : System.Text.Json.JsonSerializer.Deserialize<string[]>(p.AllowedVideoCodecsJson) ?? Array.Empty<string>(),
		preferredVideoCodecs = string.IsNullOrEmpty(p.PreferredVideoCodecsJson) ? Array.Empty<string>() : System.Text.Json.JsonSerializer.Deserialize<string[]>(p.PreferredVideoCodecsJson) ?? Array.Empty<string>(),
		allowedAudioCodecs = string.IsNullOrEmpty(p.AllowedAudioCodecsJson) ? Array.Empty<string>() : System.Text.Json.JsonSerializer.Deserialize<string[]>(p.AllowedAudioCodecsJson) ?? Array.Empty<string>(),
		preferredAudioCodecs = string.IsNullOrEmpty(p.PreferredAudioCodecsJson) ? Array.Empty<string>() : System.Text.Json.JsonSerializer.Deserialize<string[]>(p.PreferredAudioCodecsJson) ?? Array.Empty<string>(),
		allowedContainers = string.IsNullOrEmpty(p.AllowedContainersJson) ? Array.Empty<string>() : System.Text.Json.JsonSerializer.Deserialize<string[]>(p.AllowedContainersJson) ?? Array.Empty<string>(),
		preferredContainers = string.IsNullOrEmpty(p.PreferredContainersJson) ? Array.Empty<string>() : System.Text.Json.JsonSerializer.Deserialize<string[]>(p.PreferredContainersJson) ?? Array.Empty<string>(),
		preferSeparateStreams = p.PreferSeparateStreams,
		allowMuxedFallback = p.AllowMuxedFallback,
		fallbackMode = p.FallbackMode,
		degradeOrderJson = p.DegradeOrderJson,
		degradeHeightStepsJson = p.DegradeHeightStepsJson,
		failIfBelowMinHeight = p.FailIfBelowMinHeight,
		retryForBetterFormats = p.RetryForBetterFormats,
		retryWindowMinutes = p.RetryWindowMinutes,
		selectionArgs = p.SelectionArgs,
		muxArgs = p.MuxArgs,
		audioArgs = p.AudioArgs,
		timeArgs = p.TimeArgs,
		subtitleArgs = p.SubtitleArgs,
		thumbnailArgs = p.ThumbnailArgs,
		metadataArgs = p.MetadataArgs,
		cleanupArgs = p.CleanupArgs,
		sponsorblockArgs = p.SponsorblockArgs,
		configText = GetQualityProfileConfigTextForDisplay(contentRoot, p),
		configFilePath = SafeFullPath(QualityProfileConfigPaths.GetConfigFilePath(contentRoot, p.Id))
	};

	static string GetQualityProfileConfigTextForDisplay(string contentRoot, QualityProfileEntity p)
	{
		var raw = QualityProfileConfigFileOperations.ReadConfigTextOrEmpty(contentRoot, p.Id);
		if (!string.IsNullOrWhiteSpace(raw))
			return raw;
		return QualityProfileYtDlpConfigContent.BuildConfigFileBodyFromEntity(p, ffmpegConfigured: true, logger: null, logContextId: p.Id);
	}

	static string SafeFullPath(string path)
	{
		try
		{
			return Path.GetFullPath(path);
		}
		catch
		{
			return path;
		}
	}

	static void ApplyQualityProfilePayload(QualityProfileSaveRequest request, QualityProfileEntity entity)
	{
		static string? SerializeArray<T>(T[]? values) => values is null ? null : JsonSerializer.Serialize(values);

		if (!string.IsNullOrWhiteSpace(request.Name))
			entity.Name = request.Name;
		if (request.Enabled.HasValue)
			entity.Enabled = request.Enabled.Value;
		entity.MaxHeight = request.MaxHeight;
		entity.MinHeight = request.MinHeight;
		entity.MinFps = request.MinFps;
		entity.MaxFps = request.MaxFps;
		if (request.AllowHdr.HasValue)
			entity.AllowHdr = request.AllowHdr.Value;
		if (request.AllowSdr.HasValue)
			entity.AllowSdr = request.AllowSdr.Value;
		entity.AllowedVideoCodecsJson = SerializeArray(request.AllowedVideoCodecs);
		entity.PreferredVideoCodecsJson = SerializeArray(request.PreferredVideoCodecs);
		entity.AllowedAudioCodecsJson = SerializeArray(request.AllowedAudioCodecs);
		entity.PreferredAudioCodecsJson = SerializeArray(request.PreferredAudioCodecs);
		entity.AllowedContainersJson = SerializeArray(request.AllowedContainers);
		entity.PreferredContainersJson = SerializeArray(request.PreferredContainers);
		if (request.PreferSeparateStreams.HasValue)
			entity.PreferSeparateStreams = request.PreferSeparateStreams.Value;
		if (request.AllowMuxedFallback.HasValue)
			entity.AllowMuxedFallback = request.AllowMuxedFallback.Value;
		if (request.FallbackMode.HasValue)
			entity.FallbackMode = request.FallbackMode.Value;
		entity.DegradeOrderJson = request.DegradeOrderJson;
		entity.DegradeHeightStepsJson = request.DegradeHeightSteps is null
			? request.DegradeHeightStepsJson
			: JsonSerializer.Serialize(request.DegradeHeightSteps);
		if (request.FailIfBelowMinHeight.HasValue)
			entity.FailIfBelowMinHeight = request.FailIfBelowMinHeight.Value;
		if (request.RetryForBetterFormats.HasValue)
			entity.RetryForBetterFormats = request.RetryForBetterFormats.Value;
		entity.RetryWindowMinutes = request.RetryWindowMinutes;
		entity.SelectionArgs = request.SelectionArgs;
		entity.MuxArgs = request.MuxArgs;
		entity.AudioArgs = request.AudioArgs;
		entity.TimeArgs = request.TimeArgs;
		entity.SubtitleArgs = request.SubtitleArgs;
		entity.ThumbnailArgs = request.ThumbnailArgs;
		entity.MetadataArgs = request.MetadataArgs;
		entity.CleanupArgs = request.CleanupArgs;
		entity.SponsorblockArgs = request.SponsorblockArgs;
	}
	
	api.MapGet("/qualityprofile", async (TubeArrDbContext db, IWebHostEnvironment env) =>
	{
		var root = env.ContentRootPath;
		var list = await db.QualityProfiles.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
		return Results.Json(list.Select(p => ToQualityProfileResource(p, root)).ToArray());
	});
	
	api.MapGet("/qualityprofile/schema", () => Results.Json(new
	{
		fallbackModes = new[] {
			new { value = 0, name = "Strict" },
			new { value = 1, name = "NextBestWithinCeiling" },
			new { value = 2, name = "DegradeResolution" },
			new { value = 3, name = "NextBest" }
		},
		videoCodecs = YouTubeVideoCodec.All,
		audioCodecs = YouTubeAudioCodec.All,
		containers = YouTubeContainer.All,
		heightLadder = YouTubeHeightLadder.Heights
	}));
	
	api.MapGet("/qualityprofile/{id:int}", async (int id, TubeArrDbContext db, IWebHostEnvironment env) =>
	{
		var p = await db.QualityProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
		return p is null ? Results.NotFound() : Results.Json(ToQualityProfileResource(p, env.ContentRootPath));
	});
	
	api.MapPost("/qualityprofile", async (QualityProfileSaveRequest request, TubeArrDbContext db, IWebHostEnvironment env) =>
	{
		var entity = new QualityProfileEntity { Enabled = true, FallbackMode = 1, PreferSeparateStreams = true, AllowMuxedFallback = true, AllowHdr = true, AllowSdr = true, FailIfBelowMinHeight = true };
		ApplyQualityProfilePayload(request, entity);
		var errors = QualityProfileValidation.Validate(entity);
		if (errors.Count > 0)
			return ApiErrorResults.BadRequest(TubeArrErrorCodes.ValidationFailed, string.Join(" ", errors), errors);
		db.QualityProfiles.Add(entity);
		await db.SaveChangesAsync();
		var ffmpegConfig = await db.FFmpegConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync();
		var ffmpegConfigured = ffmpegConfig is not null && ffmpegConfig.Enabled && !string.IsNullOrWhiteSpace(ffmpegConfig.ExecutablePath);
		if (request.ConfigText != null)
			QualityProfileConfigFileOperations.WriteConfigText(env.ContentRootPath, entity.Id, request.ConfigText);
		else
			await QualityProfileConfigFileOperations.EnsureConfigFileExistsAsync(env.ContentRootPath, entity, ffmpegConfigured, null, default);
		return Results.Json(ToQualityProfileResource(entity, env.ContentRootPath));
	});
	
	api.MapPut("/qualityprofile/{id:int}", async (int id, QualityProfileSaveRequest request, TubeArrDbContext db, IWebHostEnvironment env) =>
	{
		if (id == QualityProfileEntity.BuiltInDefaultProfileId)
			return Results.Json(new { message = "The built-in Default quality profile cannot be modified." }, statusCode: 403);
		var entity = await db.QualityProfiles.FirstOrDefaultAsync(x => x.Id == id);
		if (entity is null)
			return Results.NotFound();
		ApplyQualityProfilePayload(request, entity);
		var errors = QualityProfileValidation.Validate(entity);
		if (errors.Count > 0)
			return ApiErrorResults.BadRequest(TubeArrErrorCodes.ValidationFailed, string.Join(" ", errors), errors);
		await db.SaveChangesAsync();
		if (request.ConfigText != null)
			QualityProfileConfigFileOperations.WriteConfigText(env.ContentRootPath, entity.Id, request.ConfigText);
		return Results.Json(ToQualityProfileResource(entity, env.ContentRootPath));
	});
	
	api.MapDelete("/qualityprofile/{id:int}", async (int id, TubeArrDbContext db, IWebHostEnvironment env) =>
	{
		if (id == QualityProfileEntity.BuiltInDefaultProfileId)
			return Results.Json(new { message = "The built-in Default quality profile cannot be deleted." }, statusCode: 403);
		var inUse = await db.Channels.AnyAsync(c => c.QualityProfileId == id);
		if (inUse)
			return ApiErrorResults.BadRequest(TubeArrErrorCodes.Conflict, "Quality profile is in use by one or more channels.");
		var entity = await db.QualityProfiles.FirstOrDefaultAsync(x => x.Id == id);
		if (entity is null)
			return Results.NotFound();
		db.QualityProfiles.Remove(entity);
		await db.SaveChangesAsync();
		QualityProfileConfigFileOperations.DeleteProfileDirectory(env.ContentRootPath, id);
		return Results.Ok();
	});
	
	// Build yt-dlp download args from channel's quality profile (used when grabbing a release).
	api.MapPost("/release", async (ReleaseBuildRequest request, TubeArrDbContext db, IWebHostEnvironment env, ILogger<Program> logger) =>
	{
		var videoId = string.IsNullOrWhiteSpace(request.YoutubeVideoId)
			? request.VideoId
			: request.YoutubeVideoId;
		var channelId = request.ChannelId;
		var qualityProfileId = request.QualityProfileId;
	
		// Support videoIds (internal id array): look up first video to get YoutubeVideoId and ChannelId
		if ((string.IsNullOrWhiteSpace(videoId) || !channelId.HasValue) && request.VideoIds is { Length: > 0 })
		{
			var internalId = request.VideoIds[0];
			if (internalId > 0)
			{
				var video = await db.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == internalId);
				if (video != null)
				{
					if (string.IsNullOrWhiteSpace(videoId))
						videoId = video.YoutubeVideoId;
					if (!channelId.HasValue)
						channelId = video.ChannelId;
				}
			}
		}
	
		if (string.IsNullOrWhiteSpace(videoId))
			return ApiErrorResults.BadRequest(TubeArrErrorCodes.InvalidInput, "youtubeVideoId, videoId, or videoIds (with internal id) is required.");
	
		QualityProfileEntity? profile = null;
		if (qualityProfileId.HasValue && qualityProfileId.Value > 0)
			profile = await db.QualityProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == qualityProfileId.Value);
		else if (channelId.HasValue)
		{
			var channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == channelId.Value);
			if (channel?.QualityProfileId.HasValue == true)
				profile = await db.QualityProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == channel.QualityProfileId!.Value);
		}
	
		if (profile is null)
		{
			logger.LogWarning("Release build failed: no quality profile resolved for videoId={VideoId}, channelId={ChannelId}", videoId, channelId);
			return ApiErrorResults.BadRequest(TubeArrErrorCodes.NoQualityProfile, "No quality profile could be resolved. Assign a profile to the channel.", new { selector = "", sort = "", ytDlpArgs = Array.Empty<string>() });
		}
	
		var builder = new YtDlpQualityProfileBuilder();
		var result = builder.Build(profile);
		var ffmpegConfig = await db.FFmpegConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync();
		var ffmpegConfigured = ffmpegConfig is not null && ffmpegConfig.Enabled && !string.IsNullOrWhiteSpace(ffmpegConfig.ExecutablePath);
		await QualityProfileConfigFileOperations.EnsureConfigFileExistsAsync(env.ContentRootPath, profile, ffmpegConfigured, logger, default);
		var configPath = QualityProfileConfigPaths.GetConfigFilePath(env.ContentRootPath, profile.Id);
		logger.LogInformation("Yt-dlp quality profile for video {VideoId}: profile={ProfileName} (id={ProfileId}), config={ConfigPath}, selector={Selector}, sort={Sort}",
			videoId, result.ProfileName, result.ProfileId, configPath, result.Selector, result.Sort);

		var url = "https://www.youtube.com/watch?v=" + videoId;

		return Results.Json(new
		{
			profileId = result.ProfileId,
			profileName = result.ProfileName,
			selector = result.Selector,
			sort = result.Sort,
			fallbackPlanSummary = result.FallbackPlanSummary,
			configFilePath = SafeFullPath(configPath),
			ytDlpArgs = new[] { "--config", configPath },
			videoUrl = url,
			debugMetadata = result.DebugMetadata
		});
	});
	
	api.MapGet("/language", (IWebHostEnvironment env) =>
	{
		var languages = ProgramStartupHelpers.LoadAvailableLanguages(env.ContentRootPath);
		// Frontend expects camelCase keys (matches Shared/Localization/languages.json).
		return Results.Json(languages.Select(x => new
		{
			id = x.Id,
			name = x.Name,
			code = x.Code,
			dictionaryFile = x.DictionaryFile,
			enabled = x.Enabled
		}));
	});
	
	// ---- YouTube config (API key) ----
	static async Task<YouTubeConfigEntity> GetOrCreateYouTubeConfigAsync(TubeArrDbContext db)
	{
		var existing = await db.YouTubeConfig.OrderBy(x => x.Id).FirstOrDefaultAsync();
		if (existing is null)
		{
			existing = new YouTubeConfigEntity
			{
				Id = 1,
				UseYouTubeApi = false,
				ApiPriorityMetadataItemsJson = YouTubeDataApiMetadataService.SerializePriorityItems(Array.Empty<string>())
			};
			db.YouTubeConfig.Add(existing);
			await db.SaveChangesAsync();
		}

		if (string.IsNullOrWhiteSpace(existing.ApiPriorityMetadataItemsJson))
			existing.ApiPriorityMetadataItemsJson = YouTubeDataApiMetadataService.SerializePriorityItems(Array.Empty<string>());

		return existing;
	}
	
	api.MapGet("/config/ui", async (TubeArrDbContext db) =>
	{
		var existing = await db.UiConfig.OrderBy(x => x.Id).FirstOrDefaultAsync();
		if (existing is null)
		{
			existing = new UiConfigEntity { Id = 1 };
			db.UiConfig.Add(existing);
			await db.SaveChangesAsync();
		}
	
		return Results.Json(new
		{
			theme = existing.Theme,
			showRelativeDates = existing.ShowRelativeDates,
			shortDateFormat = existing.ShortDateFormat,
			longDateFormat = existing.LongDateFormat,
			timeFormat = existing.TimeFormat,
			firstDayOfWeek = existing.FirstDayOfWeek,
			enableColorImpairedMode = existing.EnableColorImpairedMode,
			calendarWeekColumnHeader = existing.CalendarWeekColumnHeader,
			uiLanguage = existing.UiLanguage
		});
	});
	
	api.MapPut("/config/ui", async (UiConfigUpdateRequest request, TubeArrDbContext db) =>
	{
		var existing = await db.UiConfig.OrderBy(x => x.Id).FirstOrDefaultAsync();
		if (existing is null)
		{
			existing = new UiConfigEntity { Id = 1 };
			db.UiConfig.Add(existing);
		}

		if (!string.IsNullOrEmpty(request.Theme))
			existing.Theme = request.Theme;
		if (request.ShowRelativeDates.HasValue)
			existing.ShowRelativeDates = request.ShowRelativeDates.Value;
		if (!string.IsNullOrEmpty(request.ShortDateFormat))
			existing.ShortDateFormat = request.ShortDateFormat;
		if (!string.IsNullOrEmpty(request.LongDateFormat))
			existing.LongDateFormat = request.LongDateFormat;
		if (!string.IsNullOrEmpty(request.TimeFormat))
			existing.TimeFormat = request.TimeFormat;
		if (request.FirstDayOfWeek.HasValue)
			existing.FirstDayOfWeek = request.FirstDayOfWeek.Value;
		if (request.EnableColorImpairedMode.HasValue)
			existing.EnableColorImpairedMode = request.EnableColorImpairedMode.Value;
		if (!string.IsNullOrEmpty(request.CalendarWeekColumnHeader))
			existing.CalendarWeekColumnHeader = request.CalendarWeekColumnHeader;
		if (request.UiLanguage.HasValue)
			existing.UiLanguage = request.UiLanguage.Value;
	
		await db.SaveChangesAsync();
	
		return Results.Json(new
		{
			theme = existing.Theme,
			showRelativeDates = existing.ShowRelativeDates,
			shortDateFormat = existing.ShortDateFormat,
			longDateFormat = existing.LongDateFormat,
			timeFormat = existing.TimeFormat,
			firstDayOfWeek = existing.FirstDayOfWeek,
			enableColorImpairedMode = existing.EnableColorImpairedMode,
			calendarWeekColumnHeader = existing.CalendarWeekColumnHeader,
			uiLanguage = existing.UiLanguage
		});
	});
	
	api.MapGet("/config/youtube", async (TubeArrDbContext db) =>
	{
		var config = await GetOrCreateYouTubeConfigAsync(db);
		var items = YouTubeDataApiMetadataService.ParsePriorityItems(config.ApiPriorityMetadataItemsJson);
		return Results.Json(new
		{
			apiKey = config.ApiKey ?? "",
			useYouTubeApi = config.UseYouTubeApi,
			apiPriorityMetadataItems = items
		});
	});
	
	api.MapPut("/config/youtube", async (YouTubeConfigUpdateRequest request, TubeArrDbContext db) =>
	{
		var config = await GetOrCreateYouTubeConfigAsync(db);
	
		if (request.ApiKey is not null)
		{
			config.ApiKey = request.ApiKey.Trim();
		}
		if (request.UseYouTubeApi.HasValue)
		{
			config.UseYouTubeApi = request.UseYouTubeApi.Value;
		}
		if (request.ApiPriorityMetadataItems is not null)
		{
			config.ApiPriorityMetadataItemsJson = YouTubeDataApiMetadataService.SerializePriorityItems(request.ApiPriorityMetadataItems);
		}
	
		await db.SaveChangesAsync();
		var items = YouTubeDataApiMetadataService.ParsePriorityItems(config.ApiPriorityMetadataItemsJson);
		return Results.Json(new
		{
			apiKey = config.ApiKey ?? "",
			useYouTubeApi = config.UseYouTubeApi,
			apiPriorityMetadataItems = items
		});
	});
	
	// ---- yt-dlp config ----
	static async Task<YtDlpConfigEntity> GetOrCreateYtDlpConfigAsync(TubeArrDbContext db)
	{
		var existing = await db.YtDlpConfig.OrderBy(x => x.Id).FirstOrDefaultAsync();
		if (existing is null)
		{
			existing = new YtDlpConfigEntity { Id = 1 };
			db.YtDlpConfig.Add(existing);
			await db.SaveChangesAsync();
		}
		return existing;
	}
	
	api.MapGet("/config/ytdlp", async (TubeArrDbContext db) =>
	{
		var config = await GetOrCreateYtDlpConfigAsync(db);
		var cookiesPath = (config.CookiesPath ?? "").Trim();
		if (!string.IsNullOrEmpty(cookiesPath))
		{
			try
			{
				cookiesPath = Path.GetFullPath(cookiesPath);
			}
			catch
			{
				// keep stored value if invalid for GetFullPath
			}
		}
		return Results.Json(new
		{
			executablePath = config.ExecutablePath ?? "",
			enabled = config.Enabled,
			cookiesPath,
			cookiesExportBrowser = string.IsNullOrWhiteSpace(config.CookiesExportBrowser) ? "chrome" : config.CookiesExportBrowser,
			downloadQueueParallelWorkers = Math.Clamp(
				config.DownloadQueueParallelWorkers,
				DownloadQueueProcessor.MinDownloadQueueParallelWorkers,
				DownloadQueueProcessor.MaxDownloadQueueParallelWorkers),
			downloadTransientMaxRetries = Math.Clamp(config.DownloadTransientMaxRetries, 0, 10),
			downloadRetryDelaysSecondsJson = string.IsNullOrWhiteSpace(config.DownloadRetryDelaysSecondsJson)
				? "[30,60,120]"
				: config.DownloadRetryDelaysSecondsJson.Trim()
		});
	});
	
	api.MapPut("/config/ytdlp", async (YtDlpConfigUpdateRequest request, TubeArrDbContext db) =>
	{
		var config = await GetOrCreateYtDlpConfigAsync(db);
	
		if (request.ExecutablePath is not null)
		{
			config.ExecutablePath = request.ExecutablePath.Trim();
		}
		if (request.Enabled.HasValue)
		{
			config.Enabled = request.Enabled.Value;
		}
		if (request.CookiesPath is not null)
		{
			config.CookiesPath = request.CookiesPath.Trim();
		}
		if (request.CookiesExportBrowser is not null)
		{
			var b = request.CookiesExportBrowser.Trim().ToLowerInvariant();
			if (b is not ("chrome" or "edge" or "chromium"))
			{
				return ApiErrorResults.BadRequest(TubeArrErrorCodes.ValidationFailed, "Cookies export browser must be chrome, edge, or chromium.", new[] { new { propertyName = "cookiesExportBrowser", errorMessage = "Invalid browser." } });
			}
			config.CookiesExportBrowser = b;
		}

		if (request.DownloadQueueParallelWorkers.HasValue)
		{
			config.DownloadQueueParallelWorkers = Math.Clamp(
				request.DownloadQueueParallelWorkers.Value,
				DownloadQueueProcessor.MinDownloadQueueParallelWorkers,
				DownloadQueueProcessor.MaxDownloadQueueParallelWorkers);
		}

		if (request.DownloadTransientMaxRetries.HasValue)
			config.DownloadTransientMaxRetries = Math.Clamp(request.DownloadTransientMaxRetries.Value, 0, 10);

		if (request.DownloadRetryDelaysSecondsJson is not null)
		{
			var trimmed = request.DownloadRetryDelaysSecondsJson.Trim();
			if (string.IsNullOrEmpty(trimmed))
				config.DownloadRetryDelaysSecondsJson = "[30,60,120]";
			else
			{
				_ = DownloadRetryPolicy.ParseRetryDelaysSecondsJson(trimmed);
				config.DownloadRetryDelaysSecondsJson = trimmed;
			}
		}
	
		// Validation: executablePath required when enabled
		if (config.Enabled && string.IsNullOrWhiteSpace(config.ExecutablePath))
		{
			return ApiErrorResults.BadRequest(TubeArrErrorCodes.ValidationFailed, "Executable path is required when yt-dlp is enabled.", new[] { new { propertyName = "executablePath", errorMessage = "Executable path is required." } });
		}
	
		await db.SaveChangesAsync();
		return Results.Json(new
		{
			executablePath = config.ExecutablePath ?? "",
			enabled = config.Enabled,
			cookiesPath = config.CookiesPath ?? "",
			cookiesExportBrowser = string.IsNullOrWhiteSpace(config.CookiesExportBrowser) ? "chrome" : config.CookiesExportBrowser,
			downloadQueueParallelWorkers = config.DownloadQueueParallelWorkers,
			downloadTransientMaxRetries = Math.Clamp(config.DownloadTransientMaxRetries, 0, 10),
			downloadRetryDelaysSecondsJson = string.IsNullOrWhiteSpace(config.DownloadRetryDelaysSecondsJson)
				? "[30,60,120]"
				: config.DownloadRetryDelaysSecondsJson.Trim()
		});
	});
	
	api.MapPost("/config/ytdlp/test", async (TubeArrDbContext db, ILogger<Program> logger) =>
	{
		var config = await GetOrCreateYtDlpConfigAsync(db);
		var path = (config.ExecutablePath ?? "").Trim();
		if (string.IsNullOrWhiteSpace(path))
		{
			return ApiErrorResults.BadRequest(TubeArrErrorCodes.YtDlpNotConfigured, "yt-dlp executable path is not configured.");
		}
	
		try
		{
			using var process = new System.Diagnostics.Process();
			process.StartInfo.FileName = path;
			process.StartInfo.Arguments = "--version";
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.RedirectStandardError = true;
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.CreateNoWindow = true;
			process.Start();
			var stdout = await process.StandardOutput.ReadToEndAsync();
			var stderr = await process.StandardError.ReadToEndAsync();
			await process.WaitForExitAsync();
			if (process.ExitCode == 0)
			{
				logger.LogInformation("yt-dlp test succeeded: {Version}", (stdout ?? "").Trim());
				return Results.Json(new { success = true, message = (stdout ?? "").Trim() });
			}
			logger.LogWarning("yt-dlp test failed: exitCode={ExitCode}, stderr={Stderr}", process.ExitCode, stderr ?? "");
			return ApiErrorResults.BadRequest(TubeArrErrorCodes.OperationFailed, string.IsNullOrWhiteSpace(stderr) ? "yt-dlp exited with code " + process.ExitCode : stderr.Trim());
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "yt-dlp test failed: executablePath={Path}", path);
			return ApiErrorResults.BadRequest(TubeArrErrorCodes.OperationFailed, ex.Message);
		}
	});
	
	api.MapGet("/config/ytdlp/releases", async (IHttpClientFactory httpClientFactory) =>
	{
		try
		{
			var client = httpClientFactory.CreateClient("GitHub");
			using var response = await client.GetAsync("repos/yt-dlp/yt-dlp/releases?per_page=30");
			if (!response.IsSuccessStatusCode)
				return Results.Json(new { message = "Failed to fetch releases" }, statusCode: 502);
			await using var stream = await response.Content.ReadAsStreamAsync();
			using var doc = await JsonDocument.ParseAsync(stream);
			var releases = new List<object>();
			foreach (var rel in doc.RootElement.EnumerateArray())
			{
				var tagName = rel.TryGetProperty("tag_name", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
				var name = rel.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() ?? tagName : tagName;
				var publishedAt = rel.TryGetProperty("published_at", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";
				var assets = new List<object>();
				if (rel.TryGetProperty("assets", out var a) && a.ValueKind == JsonValueKind.Array)
				{
					foreach (var asset in a.EnumerateArray())
					{
						var assetName = asset.TryGetProperty("name", out var an) && an.ValueKind == JsonValueKind.String ? an.GetString() ?? "" : "";
						var url = asset.TryGetProperty("browser_download_url", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() ?? "" : "";
						var id = asset.TryGetProperty("id", out var idEl) ? idEl.GetInt64() : 0L;
						var size = asset.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0L;
						if (!string.IsNullOrWhiteSpace(assetName) && !string.IsNullOrWhiteSpace(url))
							assets.Add(new { id, name = assetName, browser_download_url = url, size });
					}
				}
				releases.Add(new { tag_name = tagName, name, published_at = publishedAt, assets });
			}
			return Results.Json(releases);
		}
		catch (Exception ex)
		{
			return Results.Json(new { message = ex.Message }, statusCode: 500);
		}
	});
	
	api.MapPost("/config/ytdlp/download", async (BinaryDownloadRequest request, IHttpClientFactory httpClientFactory, IWebHostEnvironment env, TubeArrDbContext db) =>
	{
		var downloadUrl = request.DownloadUrl ?? "";
		if (string.IsNullOrWhiteSpace(downloadUrl))
			return ApiErrorResults.BadRequest(TubeArrErrorCodes.InvalidInput, "downloadUrl is required.");
		if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri) || !uri.Host.EndsWith("github.com", StringComparison.OrdinalIgnoreCase))
			return ApiErrorResults.BadRequest(TubeArrErrorCodes.InvalidInput, "Invalid download URL.");
		var assetName = request.AssetName?.Trim();
		if (string.IsNullOrWhiteSpace(assetName))
			assetName = uri.Segments.Length > 0 ? uri.Segments[^1].TrimEnd('/') : "yt-dlp";
		if (assetName.Contains("..") || Path.GetFileName(assetName) != assetName)
			return ApiErrorResults.BadRequest(TubeArrErrorCodes.InvalidInput, "Invalid asset name.");
		var appRoot = env.ContentRootPath;
		var ytdlpDir = Path.Combine(appRoot, "yt-dlp");
		var savePath = Path.GetFullPath(Path.Combine(ytdlpDir, assetName));
		if (!savePath.StartsWith(Path.GetFullPath(ytdlpDir), StringComparison.OrdinalIgnoreCase))
			return ApiErrorResults.BadRequest(TubeArrErrorCodes.InvalidInput, "Invalid asset name.");
		try
		{
			Directory.CreateDirectory(ytdlpDir);
			using var client = httpClientFactory.CreateClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "TubeArr");
			client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/octet-stream");
			using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
			response.EnsureSuccessStatusCode();
			await using var stream = await response.Content.ReadAsStreamAsync();
			await using var fileStream = File.Create(savePath);
			await stream.CopyToAsync(fileStream);
			string executablePath;
			if (GitHubReleaseArchiveExtractor.IsArchiveAssetName(assetName))
			{
				var sanitized = GitHubReleaseArchiveExtractor.BuildSanitizedExtractFolderName(request.ReleaseTag, assetName);
				var extractDir = Path.GetFullPath(Path.Combine(ytdlpDir, sanitized));
				if (!extractDir.StartsWith(Path.GetFullPath(ytdlpDir), StringComparison.OrdinalIgnoreCase))
					return ApiErrorResults.BadRequest(TubeArrErrorCodes.InvalidInput, "Invalid release tag.");
				var (extractOk, extractErr) = await GitHubReleaseArchiveExtractor.TryExtractToDirectoryAsync(savePath, extractDir, default);
				if (!extractOk)
					return Results.Json(new { success = false, message = "Extract failed: " + (extractErr ?? "unknown") }, statusCode: 500);
				var exeName = OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp";
				var found = GitHubReleaseArchiveExtractor.FindDescendantFile(extractDir, exeName);
				if (string.IsNullOrEmpty(found))
				{
					var foundNames = Directory.Exists(extractDir)
						? string.Join(", ", Directory.EnumerateFileSystemEntries(extractDir).Select(Path.GetFileName))
						: "empty";
					return Results.Json(new { success = false, message = $"{exeName} not found in archive. Root: {foundNames}" }, statusCode: 500);
				}
				GitHubReleaseArchiveExtractor.TryEnsureUnixExecutable(found);
				executablePath = found;
				try { File.Delete(savePath); } catch { /* ignore */ }
			}
			else
			{
				executablePath = savePath;
			}
			var config = await GetOrCreateYtDlpConfigAsync(db);
			config.ExecutablePath = executablePath;
			config.Enabled = true;
			await db.SaveChangesAsync();
			return Results.Json(new { success = true, savePath = executablePath, executablePath });
		}
		catch (Exception ex)
		{
			return Results.Json(new { success = false, message = ex.Message }, statusCode: 500);
		}
	});
	
	api.MapPost("/config/ytdlp/update", async (TubeArrDbContext db, ILogger<Program> logger) =>
	{
		var config = await GetOrCreateYtDlpConfigAsync(db);
		var path = (config.ExecutablePath ?? "").Trim();
		if (string.IsNullOrWhiteSpace(path))
			return ApiErrorResults.BadRequest(TubeArrErrorCodes.YtDlpNotConfigured, "yt-dlp executable path is not configured.");
		try
		{
			using var process = new System.Diagnostics.Process();
			process.StartInfo.FileName = path;
			process.StartInfo.Arguments = "-U";
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.RedirectStandardError = true;
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.CreateNoWindow = true;
			process.Start();
			var stdout = await process.StandardOutput.ReadToEndAsync();
			var stderr = await process.StandardError.ReadToEndAsync();
			await process.WaitForExitAsync();
			if (process.ExitCode == 0)
			{
				logger.LogInformation("yt-dlp update succeeded");
				return Results.Json(new { success = true, message = stdout?.Trim() ?? "Updated." });
			}
			logger.LogWarning("yt-dlp update failed: exitCode={ExitCode}, stderr={Stderr}", process.ExitCode, stderr ?? "");
			return ApiErrorResults.BadRequest(TubeArrErrorCodes.OperationFailed, (stderr ?? "").Trim());
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "yt-dlp update failed: path={Path}", path);
			return Results.Json(new { success = false, message = ex.Message }, statusCode: 500);
		}
	});

	api.MapPost("/config/ytdlp/auto-detect-cookies", async (TubeArrDbContext db, IWebHostEnvironment env) =>
	{
		var config = await GetOrCreateYtDlpConfigAsync(db);

		var cookiesPaths = YtDlpCookiesPathResolver
			.EnumerateDefaultCookieSearchPaths(config.ExecutablePath, env.ContentRootPath)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		string? foundCookiesPath = null;
		foreach (var path in cookiesPaths)
		{
			if (File.Exists(path))
			{
				foundCookiesPath = Path.GetFullPath(path);
				break;
			}
		}

		if (string.IsNullOrWhiteSpace(foundCookiesPath))
		{
			return Results.Json(new
			{
				success = false,
				message = "No cookies file found next to yt-dlp or in legacy export locations. Use Export browser cookies in Settings, or python scripts/export-browser-cookies.py."
			}, statusCode: 404);
		}

		// Found cookies file - update config
		config.CookiesPath = foundCookiesPath;
		await db.SaveChangesAsync();

		return Results.Json(new
		{
			success = true,
			message = $"Cookies file auto-detected and configured: {foundCookiesPath}",
			cookiesPath = config.CookiesPath ?? ""
		});
	});

	api.MapPost("/config/ytdlp/export-browser-cookies", async (ExportBrowserCookiesRequest request, TubeArrDbContext db, IWebHostEnvironment env, IBrowserCookieService cookieService, ILogger<Program> logger) =>
	{
		var config = await GetOrCreateYtDlpConfigAsync(db);
		var outputPath = YtDlpCookiesPathResolver.GetDefaultCookiesTxtPath(config.ExecutablePath, env.ContentRootPath);
		var cookiesDir = Path.GetDirectoryName(outputPath);
		if (!string.IsNullOrWhiteSpace(cookiesDir))
			Directory.CreateDirectory(cookiesDir);

		var browserFromRequest = string.IsNullOrWhiteSpace(request.Browser) ? null : request.Browser.Trim();
		var browserFromConfig = string.IsNullOrWhiteSpace(config.CookiesExportBrowser) ? "chrome" : config.CookiesExportBrowser.Trim();
		var browserKey = (browserFromRequest ?? browserFromConfig).ToLowerInvariant();

		logger.LogInformation("HTTP cookie export: browser={Browser} reopenBrowser={Reopen} outputPath={Output}",
			browserKey, request.ReopenBrowser ?? true, outputPath);

		var result = await cookieService.ExportBrowserCookiesAsync(
			browserKey,
			outputPath,
			reopenBrowser: request.ReopenBrowser ?? true
		);

		if (!result.Success)
		{
			logger.LogWarning("HTTP cookie export failed: {Message}", result.Message);
			return ApiErrorResults.BadRequest(TubeArrErrorCodes.OperationFailed, result.Message);
		}

		var resolvedCookiesPath = Path.GetFullPath(result.CookiesPath ?? outputPath);
		config.CookiesPath = resolvedCookiesPath;
		await db.SaveChangesAsync();

		logger.LogInformation("HTTP cookie export succeeded: cookieCount={Count} cookiesPath={CookiesPath}",
			result.CookieCount, config.CookiesPath);

		return Results.Json(new
		{
			success = true,
			message = $"Successfully exported {result.CookieCount} cookies and auto-configured TubeArr",
			cookiesPath = resolvedCookiesPath,
			cookieCount = result.CookieCount
		});
	});
	
	// ---- FFmpeg config ----
	static async Task<FFmpegConfigEntity> GetOrCreateFFmpegConfigAsync(TubeArrDbContext db)
	{
		var existing = await db.FFmpegConfig.OrderBy(x => x.Id).FirstOrDefaultAsync();
		if (existing is null)
		{
			existing = new FFmpegConfigEntity { Id = 1 };
			db.FFmpegConfig.Add(existing);
			await db.SaveChangesAsync();
		}
		return existing;
	}
	
	api.MapGet("/config/ffmpeg", async (TubeArrDbContext db) =>
	{
		var config = await GetOrCreateFFmpegConfigAsync(db);
		return Results.Json(new
		{
			executablePath = config.ExecutablePath ?? "",
			enabled = config.Enabled
		});
	});
	
	api.MapPut("/config/ffmpeg", async (ExecutableConfigUpdateRequest request, TubeArrDbContext db) =>
	{
		var config = await GetOrCreateFFmpegConfigAsync(db);
	
		if (request.ExecutablePath is not null)
		{
			config.ExecutablePath = request.ExecutablePath.Trim();
		}
		if (request.Enabled.HasValue)
		{
			config.Enabled = request.Enabled.Value;
		}
	
		if (config.Enabled && string.IsNullOrWhiteSpace(config.ExecutablePath))
		{
			return ApiErrorResults.BadRequest(TubeArrErrorCodes.ValidationFailed, "Executable path is required when FFmpeg is enabled.", new[] { new { propertyName = "executablePath", errorMessage = "Executable path is required." } });
		}
	
		await db.SaveChangesAsync();
		return Results.Json(new
		{
			executablePath = config.ExecutablePath ?? "",
			enabled = config.Enabled
		});
	});
	
	api.MapGet("/config/ffmpeg/releases", async (IHttpClientFactory httpClientFactory) =>
	{
		try
		{
			var client = httpClientFactory.CreateClient("GitHub");
			using var response = await client.GetAsync("repos/BtbN/FFmpeg-Builds/releases?per_page=30");
			if (!response.IsSuccessStatusCode)
				return Results.Json(new { message = "Failed to fetch releases" }, statusCode: 502);
			await using var stream = await response.Content.ReadAsStreamAsync();
			using var doc = await JsonDocument.ParseAsync(stream);
			var releases = new List<object>();
			foreach (var rel in doc.RootElement.EnumerateArray())
			{
				var tagName = rel.TryGetProperty("tag_name", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
				var name = rel.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() ?? tagName : tagName;
				var publishedAt = rel.TryGetProperty("published_at", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";
				var assets = new List<object>();
				if (rel.TryGetProperty("assets", out var a) && a.ValueKind == JsonValueKind.Array)
				{
					foreach (var asset in a.EnumerateArray())
					{
						var assetName = asset.TryGetProperty("name", out var an) && an.ValueKind == JsonValueKind.String ? an.GetString() ?? "" : "";
						var url = asset.TryGetProperty("browser_download_url", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() ?? "" : "";
						var id = asset.TryGetProperty("id", out var idEl) ? idEl.GetInt64() : 0L;
						var size = asset.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0L;
						if (!string.IsNullOrWhiteSpace(assetName) && !string.IsNullOrWhiteSpace(url))
							assets.Add(new { id, name = assetName, browser_download_url = url, size });
					}
				}
				releases.Add(new { tag_name = tagName, name, published_at = publishedAt, assets });
			}
			return Results.Json(releases);
		}
		catch (Exception ex)
		{
			return Results.Json(new { message = ex.Message }, statusCode: 500);
		}
	});
	
	api.MapPost("/config/ffmpeg/download", async (BinaryDownloadRequest request, IHttpClientFactory httpClientFactory, IWebHostEnvironment env, TubeArrDbContext db) =>
	{
		var downloadUrl = request.DownloadUrl ?? "";
		if (string.IsNullOrWhiteSpace(downloadUrl))
			return ApiErrorResults.BadRequest(TubeArrErrorCodes.InvalidInput, "downloadUrl is required.");
		if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri) || !uri.Host.EndsWith("github.com", StringComparison.OrdinalIgnoreCase))
			return ApiErrorResults.BadRequest(TubeArrErrorCodes.InvalidInput, "Invalid download URL.");
		var assetName = request.AssetName?.Trim();
		if (string.IsNullOrWhiteSpace(assetName))
			assetName = uri.Segments.Length > 0 ? uri.Segments[^1].TrimEnd('/') : "ffmpeg.zip";
		if (assetName.Contains("..") || Path.GetFileName(assetName) != assetName)
			return ApiErrorResults.BadRequest(TubeArrErrorCodes.InvalidInput, "Invalid asset name.");
		var appRoot = env.ContentRootPath;
		var ffmpegDir = Path.Combine(appRoot, "ffmpeg");
		Directory.CreateDirectory(ffmpegDir);
		var savePath = Path.GetFullPath(Path.Combine(ffmpegDir, assetName));
		if (!savePath.StartsWith(Path.GetFullPath(ffmpegDir), StringComparison.OrdinalIgnoreCase))
			return ApiErrorResults.BadRequest(TubeArrErrorCodes.InvalidInput, "Invalid asset name.");
		string executablePath;
		try
		{
			using var client = httpClientFactory.CreateClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "TubeArr");
			client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/octet-stream");
			using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
			response.EnsureSuccessStatusCode();
			await using var stream = await response.Content.ReadAsStreamAsync();
			await using (var fileStream = File.Create(savePath))
				await stream.CopyToAsync(fileStream);
		}
		catch (Exception ex)
		{
			return Results.Json(new { success = false, message = ex.Message }, statusCode: 500);
		}
		if (GitHubReleaseArchiveExtractor.IsArchiveAssetName(assetName))
		{
			var sanitized = GitHubReleaseArchiveExtractor.BuildSanitizedExtractFolderName(request.ReleaseTag, assetName);
			var extractDir = Path.GetFullPath(Path.Combine(ffmpegDir, sanitized));
			if (!extractDir.StartsWith(Path.GetFullPath(ffmpegDir), StringComparison.OrdinalIgnoreCase))
				return ApiErrorResults.BadRequest(TubeArrErrorCodes.InvalidInput, "Invalid release tag.");
			var (extractOk, extractErr) = await GitHubReleaseArchiveExtractor.TryExtractToDirectoryAsync(savePath, extractDir, default);
			if (!extractOk)
				return Results.Json(new { success = false, message = "Extract failed: " + (extractErr ?? "unknown") }, statusCode: 500);
			var ffmpegFullBase = Path.GetFullPath(ffmpegDir);
			var ffmpegName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
			var binDir = Path.GetFullPath(Path.Combine(extractDir, "bin"));
			if (!binDir.StartsWith(ffmpegFullBase, StringComparison.OrdinalIgnoreCase))
				return ApiErrorResults.BadRequest(TubeArrErrorCodes.InvalidInput, "Invalid path.");
			if (!Directory.Exists(binDir))
			{
				var subBin = Directory.EnumerateDirectories(extractDir)
					.Select(d => Path.GetFullPath(Path.Combine(d, "bin")))
					.Where(d => d.StartsWith(ffmpegFullBase, StringComparison.OrdinalIgnoreCase))
					.FirstOrDefault(d => Directory.Exists(d));
				binDir = subBin ?? binDir;
			}
			var ffmpegExe = Path.GetFullPath(Path.Combine(binDir, ffmpegName));
			if (!ffmpegExe.StartsWith(ffmpegFullBase, StringComparison.OrdinalIgnoreCase))
				return ApiErrorResults.BadRequest(TubeArrErrorCodes.InvalidInput, "Invalid path.");
			if (!File.Exists(ffmpegExe))
			{
				var found = Directory.Exists(extractDir)
					? string.Join(", ", Directory.EnumerateFileSystemEntries(extractDir)
						.Where(e => Path.GetFullPath(e).StartsWith(ffmpegFullBase, StringComparison.OrdinalIgnoreCase))
						.Select(Path.GetFileName))
					: "empty";
				return Results.Json(new { success = false, message = "ffmpeg executable not found in archive (bin/ffmpeg). Root: " + found }, statusCode: 500);
			}
			executablePath = ffmpegExe;
			try { File.Delete(savePath); } catch { /* ignore */ }
		}
		else
		{
			executablePath = savePath;
		}
		try
		{
			var config = await GetOrCreateFFmpegConfigAsync(db);
			config.ExecutablePath = executablePath;
			config.Enabled = true;
			await db.SaveChangesAsync();
			return Results.Json(new { success = true, savePath = executablePath, executablePath });
		}
		catch (Exception ex)
		{
			return Results.Json(new { success = false, message = ex.Message }, statusCode: 500);
		}
	});
	
	api.MapPost("/config/ffmpeg/test", async (TubeArrDbContext db, ILogger<Program> logger) =>
	{
		var config = await GetOrCreateFFmpegConfigAsync(db);
		var path = (config.ExecutablePath ?? "").Trim();
		if (string.IsNullOrWhiteSpace(path))
		{
			return ApiErrorResults.BadRequest(TubeArrErrorCodes.OperationFailed, "FFmpeg executable path is not configured.");
		}
	
		try
		{
			using var process = new System.Diagnostics.Process();
			process.StartInfo.FileName = path;
			process.StartInfo.Arguments = "-version";
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.RedirectStandardError = true;
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.CreateNoWindow = true;
			process.Start();
			var stdout = await process.StandardOutput.ReadToEndAsync();
			var stderr = await process.StandardError.ReadToEndAsync();
			await process.WaitForExitAsync();
			if (process.ExitCode == 0)
			{
				var versionLine = (stdout ?? stderr ?? "").Trim().Split('\n').FirstOrDefault() ?? "";
				logger.LogInformation("FFmpeg test succeeded: {Version}", versionLine);
				return Results.Json(new { success = true, message = versionLine });
			}
			logger.LogWarning("FFmpeg test failed: exitCode={ExitCode}, stderr={Stderr}", process.ExitCode, stderr ?? "");
			return ApiErrorResults.BadRequest(TubeArrErrorCodes.OperationFailed, string.IsNullOrWhiteSpace(stderr) ? "FFmpeg exited with code " + process.ExitCode : stderr.Trim());
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "FFmpeg test failed: executablePath={Path}", path);
			return ApiErrorResults.BadRequest(TubeArrErrorCodes.OperationFailed, ex.Message);
		}
	});
	
	api.MapGet("/config/mediamanagement", async (TubeArrDbContext db) =>
	{
		var existing = await db.MediaManagementConfig.OrderBy(x => x.Id).FirstOrDefaultAsync();
		if (existing is null)
		{
			existing = new MediaManagementConfigEntity { Id = 1 };
			db.MediaManagementConfig.Add(existing);
			await db.SaveChangesAsync();
		}
	
		return Results.Json(new
		{
			createEmptyChannelFolders = existing.CreateEmptyChannelFolders,
			deleteEmptyFolders = existing.DeleteEmptyFolders,
			videoTitleRequired = existing.VideoTitleRequired,
			skipFreeSpaceCheckWhenImporting = existing.SkipFreeSpaceCheckWhenImporting,
			minimumFreeSpaceWhenImporting = existing.MinimumFreeSpaceWhenImporting,
			copyUsingHardlinks = existing.CopyUsingHardlinks,
			useScriptImport = existing.UseScriptImport,
			scriptImportPath = existing.ScriptImportPath,
			importExtraFiles = existing.ImportExtraFiles,
			extraFileExtensions = existing.ExtraFileExtensions,
			autoUnmonitorPreviouslyDownloadedVideos = existing.AutoUnmonitorPreviouslyDownloadedVideos,
			downloadPropersAndRepacks = existing.DownloadPropersAndRepacks,
			enableMediaInfo = existing.EnableMediaInfo,
			useCustomNfos = existing.UseCustomNfos,
			downloadLibraryThumbnails = existing.DownloadLibraryThumbnails,
			rescanAfterRefresh = existing.RescanAfterRefresh,
			fileDate = existing.FileDate,
			recycleBin = existing.RecycleBin,
			recycleBinCleanupDays = existing.RecycleBinCleanupDays,
			setPermissionsLinux = existing.SetPermissionsLinux,
			chmodFolder = existing.ChmodFolder,
			chownGroup = existing.ChownGroup
		});
	});

	api.MapGet("/config/plex-provider", async (TubeArrDbContext db, CancellationToken ct) =>
	{
		var existing = await db.PlexProviderConfig.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		if (existing is null)
		{
			existing = new PlexProviderConfigEntity { Id = 1, ExposeArtworkUrls = true };
			db.PlexProviderConfig.Add(existing);
			await db.SaveChangesAsync(ct);
		}

		return Results.Json(new
		{
			enabled = existing.Enabled,
			basePath = existing.BasePath ?? "",
			exposeArtworkUrls = existing.ExposeArtworkUrls,
			includeChildrenByDefault = existing.IncludeChildrenByDefault,
			verboseRequestLogging = existing.VerboseRequestLogging
		});
	});

	api.MapPut("/config/plex-provider", async (PlexProviderConfigUpdateRequest request, TubeArrDbContext db, CancellationToken ct) =>
	{
		var existing = await db.PlexProviderConfig.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		if (existing is null)
		{
			existing = new PlexProviderConfigEntity { Id = 1, ExposeArtworkUrls = true };
			db.PlexProviderConfig.Add(existing);
		}

		var wasEnabled = existing.Enabled;
		if (request.Enabled.HasValue)
			existing.Enabled = request.Enabled.Value;
		if (request.BasePath is not null)
			existing.BasePath = request.BasePath.Trim();
		if (request.ExposeArtworkUrls.HasValue)
			existing.ExposeArtworkUrls = request.ExposeArtworkUrls.Value;
		if (request.IncludeChildrenByDefault.HasValue)
			existing.IncludeChildrenByDefault = request.IncludeChildrenByDefault.Value;
		if (request.VerboseRequestLogging.HasValue)
			existing.VerboseRequestLogging = request.VerboseRequestLogging.Value;

		if (request.Enabled == true && !wasEnabled)
		{
			var mm = await db.MediaManagementConfig.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
			if (mm is null)
			{
				mm = new MediaManagementConfigEntity { Id = 1, DownloadLibraryThumbnails = true };
				db.MediaManagementConfig.Add(mm);
			}
			else
				mm.DownloadLibraryThumbnails = true;
		}

		await db.SaveChangesAsync(ct);

		return Results.Json(new
		{
			enabled = existing.Enabled,
			basePath = existing.BasePath ?? "",
			exposeArtworkUrls = existing.ExposeArtworkUrls,
			includeChildrenByDefault = existing.IncludeChildrenByDefault,
			verboseRequestLogging = existing.VerboseRequestLogging
		});
	});
	
	api.MapPut("/config/mediamanagement", async (MediaManagementConfigUpdateRequest request, TubeArrDbContext db) =>
	{
		var existing = await db.MediaManagementConfig.OrderBy(x => x.Id).FirstOrDefaultAsync();
		if (existing is null)
		{
			existing = new MediaManagementConfigEntity { Id = 1 };
			db.MediaManagementConfig.Add(existing);
		}

		if (request.CreateEmptyChannelFolders.HasValue)
			existing.CreateEmptyChannelFolders = request.CreateEmptyChannelFolders.Value;
		if (request.DeleteEmptyFolders.HasValue)
			existing.DeleteEmptyFolders = request.DeleteEmptyFolders.Value;
		if (request.VideoTitleRequired is not null)
			existing.VideoTitleRequired = request.VideoTitleRequired;
		if (request.SkipFreeSpaceCheckWhenImporting.HasValue)
			existing.SkipFreeSpaceCheckWhenImporting = request.SkipFreeSpaceCheckWhenImporting.Value;
		if (request.MinimumFreeSpaceWhenImporting.HasValue)
			existing.MinimumFreeSpaceWhenImporting = request.MinimumFreeSpaceWhenImporting.Value;
		if (request.CopyUsingHardlinks.HasValue)
			existing.CopyUsingHardlinks = request.CopyUsingHardlinks.Value;
		if (request.UseScriptImport.HasValue)
			existing.UseScriptImport = request.UseScriptImport.Value;
		if (request.ScriptImportPath is not null)
			existing.ScriptImportPath = request.ScriptImportPath;
		if (request.ImportExtraFiles.HasValue)
			existing.ImportExtraFiles = request.ImportExtraFiles.Value;
		if (request.ExtraFileExtensions is not null)
			existing.ExtraFileExtensions = request.ExtraFileExtensions;
		if (request.AutoUnmonitorPreviouslyDownloadedVideos.HasValue)
			existing.AutoUnmonitorPreviouslyDownloadedVideos = request.AutoUnmonitorPreviouslyDownloadedVideos.Value;
		if (request.DownloadPropersAndRepacks is not null)
			existing.DownloadPropersAndRepacks = request.DownloadPropersAndRepacks;
		if (request.EnableMediaInfo.HasValue)
			existing.EnableMediaInfo = request.EnableMediaInfo.Value;
		if (request.UseCustomNfos.HasValue)
			existing.UseCustomNfos = request.UseCustomNfos.Value;
		if (request.DownloadLibraryThumbnails.HasValue)
			existing.DownloadLibraryThumbnails = request.DownloadLibraryThumbnails.Value;
		if (request.RescanAfterRefresh is not null)
			existing.RescanAfterRefresh = request.RescanAfterRefresh;
		if (request.FileDate is not null)
			existing.FileDate = request.FileDate;
		if (request.RecycleBin is not null)
			existing.RecycleBin = request.RecycleBin;
		if (request.RecycleBinCleanupDays.HasValue)
			existing.RecycleBinCleanupDays = request.RecycleBinCleanupDays.Value;
		if (request.SetPermissionsLinux.HasValue)
			existing.SetPermissionsLinux = request.SetPermissionsLinux.Value;
		if (request.ChmodFolder is not null)
			existing.ChmodFolder = request.ChmodFolder;
		if (request.ChownGroup is not null)
			existing.ChownGroup = request.ChownGroup;
	
		await db.SaveChangesAsync();
	
		return Results.Json(new
		{
			createEmptyChannelFolders = existing.CreateEmptyChannelFolders,
			deleteEmptyFolders = existing.DeleteEmptyFolders,
			videoTitleRequired = existing.VideoTitleRequired,
			skipFreeSpaceCheckWhenImporting = existing.SkipFreeSpaceCheckWhenImporting,
			minimumFreeSpaceWhenImporting = existing.MinimumFreeSpaceWhenImporting,
			copyUsingHardlinks = existing.CopyUsingHardlinks,
			useScriptImport = existing.UseScriptImport,
			scriptImportPath = existing.ScriptImportPath,
			importExtraFiles = existing.ImportExtraFiles,
			extraFileExtensions = existing.ExtraFileExtensions,
			autoUnmonitorPreviouslyDownloadedVideos = existing.AutoUnmonitorPreviouslyDownloadedVideos,
			downloadPropersAndRepacks = existing.DownloadPropersAndRepacks,
			enableMediaInfo = existing.EnableMediaInfo,
			useCustomNfos = existing.UseCustomNfos,
			downloadLibraryThumbnails = existing.DownloadLibraryThumbnails,
			rescanAfterRefresh = existing.RescanAfterRefresh,
			fileDate = existing.FileDate,
			recycleBin = existing.RecycleBin,
			recycleBinCleanupDays = existing.RecycleBinCleanupDays,
			setPermissionsLinux = existing.SetPermissionsLinux,
			chmodFolder = existing.ChmodFolder,
			chownGroup = existing.ChownGroup
		});
	});

	api.MapPost("/config/mediamanagement/remove-managed-nfos", async (TubeArrDbContext db, ILoggerFactory loggerFactory, CancellationToken ct) =>
	{
		var logger = loggerFactory.CreateLogger("ManagedNfoRemoval");
		var r = await ManagedNfoRemovalRunner.RunAsync(db, logger, ct);
		return Results.Json(new
		{
			showFoldersScanned = r.ShowFoldersScanned,
			nfosDeleted = r.NfosDeleted,
			nfosAlreadyMissing = r.NfosAlreadyMissing,
			message = r.Message
		});
	});

	MapServerSettingsEndpoints(api);
	}

	static partial void MapServerSettingsEndpoints(RouteGroupBuilder api);
}
