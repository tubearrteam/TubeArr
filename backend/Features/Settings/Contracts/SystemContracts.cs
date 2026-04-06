namespace TubeArr.Backend.Contracts;

public record ServerSettingsResource(
	int Id,
	string BindAddress,
	int Port,
	int SslPort,
	bool EnableSsl,
	bool LaunchBrowser,
	string AuthenticationMethod,
	string AuthenticationRequired,
	bool AnalyticsEnabled,
	string Username,
	string Password,
	string PasswordConfirmation,
	string LogLevel,
	string ConsoleLogLevel,
	string Branch,
	string ApiKey,
	string SslCertPath,
	string SslCertPassword,
	string UrlBase,
	string InstanceName,
	string ApplicationUrl,
	bool UpdateAutomatically,
	string UpdateMechanism,
	string UpdateScriptPath,
	bool ProxyEnabled,
	string ProxyType,
	string ProxyHostname,
	int ProxyPort,
	string ProxyUsername,
	string ProxyPassword,
	string ProxyBypassFilter,
	bool ProxyBypassLocalAddresses,
	string CertificateValidation,
	string BackupFolder,
	int BackupInterval,
	int BackupRetention,
	int LogSizeLimit
);

public record ScheduledTaskDto(
	int Id,
	string Name,
	string TaskName,
	int Interval,
	string? LastExecution,
	string? LastStartTime,
	string? LastDuration,
	string? NextExecution
);

public record RootFolderCreateRequest(string? Path);
public record TagCreateRequest(string? Label);

public record QualityProfileSaveRequest(
	string? Name,
	bool? Enabled,
	int? MaxHeight,
	int? MinHeight,
	int? MinFps,
	int? MaxFps,
	bool? AllowHdr,
	bool? AllowSdr,
	string[]? AllowedVideoCodecs,
	string[]? PreferredVideoCodecs,
	string[]? AllowedAudioCodecs,
	string[]? PreferredAudioCodecs,
	string[]? AllowedContainers,
	string[]? PreferredContainers,
	bool? PreferSeparateStreams,
	bool? AllowMuxedFallback,
	int? FallbackMode,
	string? DegradeOrderJson,
	string? DegradeHeightStepsJson,
	int[]? DegradeHeightSteps,
	bool? FailIfBelowMinHeight,
	bool? RetryForBetterFormats,
	int? RetryWindowMinutes,
	string? SelectionArgs,
	string? MuxArgs,
	string? AudioArgs,
	string? TimeArgs,
	string? SubtitleArgs,
	string? ThumbnailArgs,
	string? MetadataArgs,
	string? CleanupArgs,
	string? SponsorblockArgs,
	string? ConfigText
);

public record ReleaseBuildRequest(
	string? YoutubeVideoId,
	string? VideoId,
	int? ChannelId,
	int? QualityProfileId,
	int[]? VideoIds
);

public record UiConfigUpdateRequest(
	string? Theme,
	bool? ShowRelativeDates,
	string? ShortDateFormat,
	string? LongDateFormat,
	string? TimeFormat,
	int? FirstDayOfWeek,
	bool? EnableColorImpairedMode,
	string? CalendarWeekColumnHeader,
	int? UiLanguage
);

public record YouTubeConfigUpdateRequest(
	string? ApiKey,
	bool? UseYouTubeApi,
	string[]? ApiPriorityMetadataItems
);

public record ExecutableConfigUpdateRequest(
	string? ExecutablePath,
	bool? Enabled
);

public record YtDlpConfigUpdateRequest(
	string? ExecutablePath,
	bool? Enabled,
	string? CookiesPath,
	string? CookiesExportBrowser,
	int? DownloadQueueParallelWorkers
);

public record ExportBrowserCookiesRequest(
	string? Browser,
	bool? ReopenBrowser
);

public record BinaryDownloadRequest(
	string? DownloadUrl,
	string? AssetName,
	string? ReleaseTag
);

public record MediaManagementConfigUpdateRequest(
	bool? CreateEmptyChannelFolders,
	bool? DeleteEmptyFolders,
	string? VideoTitleRequired,
	bool? SkipFreeSpaceCheckWhenImporting,
	int? MinimumFreeSpaceWhenImporting,
	bool? CopyUsingHardlinks,
	bool? UseScriptImport,
	string? ScriptImportPath,
	bool? ImportExtraFiles,
	string? ExtraFileExtensions,
	bool? AutoUnmonitorPreviouslyDownloadedVideos,
	string? DownloadPropersAndRepacks,
	bool? EnableMediaInfo,
	bool? UseCustomNfos,
	bool? DownloadLibraryThumbnails,
	string? RescanAfterRefresh,
	string? FileDate,
	string? RecycleBin,
	int? RecycleBinCleanupDays,
	bool? SetPermissionsLinux,
	string? ChmodFolder,
	string? ChownGroup
);

public record NamingConfigUpdateRequest(
	bool? RenameVideos,
	bool? ReplaceIllegalCharacters,
	int? ColonReplacementFormat,
	string? CustomColonReplacementFormat,
	int? MultiVideoStyle,
	string? StandardVideoFormat,
	string? DailyVideoFormat,
	string? EpisodicVideoFormat,
	string? StreamingVideoFormat,
	string? ChannelFolderFormat,
	string? PlaylistFolderFormat,
	string? SpecialsFolderFormat
);

public record PlexProviderConfigUpdateRequest(
	bool? Enabled,
	string? BasePath,
	bool? ExposeArtworkUrls,
	bool? IncludeChildrenByDefault,
	bool? VerboseRequestLogging
);
