using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Channels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    YoutubeChannelId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "TEXT", nullable: true),
                    BannerUrl = table.Column<string>(type: "TEXT", nullable: true),
                    TitleSlug = table.Column<string>(type: "TEXT", nullable: false),
                    Monitored = table.Column<bool>(type: "INTEGER", nullable: false),
                    Added = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    QualityProfileId = table.Column<int>(type: "INTEGER", nullable: true),
                    Path = table.Column<string>(type: "TEXT", nullable: true),
                    RootFolderPath = table.Column<string>(type: "TEXT", nullable: true),
                    Tags = table.Column<string>(type: "TEXT", nullable: true),
                    MonitorNewItems = table.Column<int>(type: "INTEGER", nullable: true),
                    PlaylistFolder = table.Column<bool>(type: "INTEGER", nullable: true),
                    ChannelType = table.Column<string>(type: "TEXT", nullable: true),
                    RoundRobinLatestVideoCount = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Channels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommandQueueJobs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CommandId = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    JobType = table.Column<string>(type: "TEXT", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    QueuedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandQueueJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomFilters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    FiltersJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFilters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DownloadHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChannelId = table.Column<int>(type: "INTEGER", nullable: false),
                    VideoId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlaylistId = table.Column<int>(type: "INTEGER", nullable: true),
                    EventType = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceTitle = table.Column<string>(type: "TEXT", nullable: false),
                    OutputPath = table.Column<string>(type: "TEXT", nullable: true),
                    Message = table.Column<string>(type: "TEXT", nullable: true),
                    DownloadId = table.Column<string>(type: "TEXT", nullable: true),
                    Date = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DownloadQueue",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VideoId = table.Column<int>(type: "INTEGER", nullable: false),
                    ChannelId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Progress = table.Column<double>(type: "REAL", nullable: true),
                    EstimatedSecondsRemaining = table.Column<int>(type: "INTEGER", nullable: true),
                    OutputPath = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    QueuedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadQueue", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FFmpegConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExecutablePath = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FFmpegConfig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportExclusions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    YoutubeChannelId = table.Column<string>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportExclusions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportListOptionsConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ListSyncLevel = table.Column<string>(type: "TEXT", nullable: false),
                    ListSyncTag = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportListOptionsConfig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaManagementConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreateEmptyChannelFolders = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeleteEmptyFolders = table.Column<bool>(type: "INTEGER", nullable: false),
                    VideoTitleRequired = table.Column<string>(type: "TEXT", nullable: false),
                    SkipFreeSpaceCheckWhenImporting = table.Column<bool>(type: "INTEGER", nullable: false),
                    MinimumFreeSpaceWhenImporting = table.Column<int>(type: "INTEGER", nullable: false),
                    CopyUsingHardlinks = table.Column<bool>(type: "INTEGER", nullable: false),
                    UseScriptImport = table.Column<bool>(type: "INTEGER", nullable: false),
                    ScriptImportPath = table.Column<string>(type: "TEXT", nullable: false),
                    ImportExtraFiles = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExtraFileExtensions = table.Column<string>(type: "TEXT", nullable: false),
                    AutoUnmonitorPreviouslyDownloadedVideos = table.Column<bool>(type: "INTEGER", nullable: false),
                    DownloadPropersAndRepacks = table.Column<string>(type: "TEXT", nullable: false),
                    EnableMediaInfo = table.Column<bool>(type: "INTEGER", nullable: false),
                    RescanAfterRefresh = table.Column<string>(type: "TEXT", nullable: false),
                    FileDate = table.Column<string>(type: "TEXT", nullable: false),
                    RecycleBin = table.Column<string>(type: "TEXT", nullable: false),
                    RecycleBinCleanupDays = table.Column<int>(type: "INTEGER", nullable: false),
                    SetPermissionsLinux = table.Column<bool>(type: "INTEGER", nullable: false),
                    ChmodFolder = table.Column<string>(type: "TEXT", nullable: false),
                    ChownGroup = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaManagementConfig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NamingConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RenameVideos = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReplaceIllegalCharacters = table.Column<bool>(type: "INTEGER", nullable: false),
                    ColonReplacementFormat = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomColonReplacementFormat = table.Column<string>(type: "TEXT", nullable: false),
                    MultiVideoStyle = table.Column<int>(type: "INTEGER", nullable: false),
                    StandardVideoFormat = table.Column<string>(type: "TEXT", nullable: false),
                    DailyVideoFormat = table.Column<string>(type: "TEXT", nullable: false),
                    EpisodicVideoFormat = table.Column<string>(type: "TEXT", nullable: false),
                    StreamingVideoFormat = table.Column<string>(type: "TEXT", nullable: false),
                    ChannelFolderFormat = table.Column<string>(type: "TEXT", nullable: false),
                    PlaylistFolderFormat = table.Column<string>(type: "TEXT", nullable: false),
                    SpecialsFolderFormat = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NamingConfig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Playlists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChannelId = table.Column<int>(type: "INTEGER", nullable: false),
                    YoutubePlaylistId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Monitored = table.Column<bool>(type: "INTEGER", nullable: false),
                    Added = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Playlists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QualityProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxHeight = table.Column<int>(type: "INTEGER", nullable: true),
                    MinHeight = table.Column<int>(type: "INTEGER", nullable: true),
                    MinFps = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxFps = table.Column<int>(type: "INTEGER", nullable: true),
                    AllowHdr = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowSdr = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowedVideoCodecsJson = table.Column<string>(type: "TEXT", nullable: true),
                    PreferredVideoCodecsJson = table.Column<string>(type: "TEXT", nullable: true),
                    AllowedAudioCodecsJson = table.Column<string>(type: "TEXT", nullable: true),
                    PreferredAudioCodecsJson = table.Column<string>(type: "TEXT", nullable: true),
                    AllowedContainersJson = table.Column<string>(type: "TEXT", nullable: true),
                    PreferredContainersJson = table.Column<string>(type: "TEXT", nullable: true),
                    PreferSeparateStreams = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowMuxedFallback = table.Column<bool>(type: "INTEGER", nullable: false),
                    FallbackMode = table.Column<int>(type: "INTEGER", nullable: false),
                    DegradeOrderJson = table.Column<string>(type: "TEXT", nullable: true),
                    DegradeHeightStepsJson = table.Column<string>(type: "TEXT", nullable: true),
                    FailIfBelowMinHeight = table.Column<bool>(type: "INTEGER", nullable: false),
                    RetryForBetterFormats = table.Column<bool>(type: "INTEGER", nullable: false),
                    RetryWindowMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    SelectionArgs = table.Column<string>(type: "TEXT", nullable: true),
                    MuxArgs = table.Column<string>(type: "TEXT", nullable: true),
                    AudioArgs = table.Column<string>(type: "TEXT", nullable: true),
                    TimeArgs = table.Column<string>(type: "TEXT", nullable: true),
                    SubtitleArgs = table.Column<string>(type: "TEXT", nullable: true),
                    ThumbnailArgs = table.Column<string>(type: "TEXT", nullable: true),
                    MetadataArgs = table.Column<string>(type: "TEXT", nullable: true),
                    CleanupArgs = table.Column<string>(type: "TEXT", nullable: true),
                    SponsorblockArgs = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RootFolders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Path = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RootFolders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServerSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BindAddress = table.Column<string>(type: "TEXT", nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    UrlBase = table.Column<string>(type: "TEXT", nullable: false),
                    InstanceName = table.Column<string>(type: "TEXT", nullable: false),
                    ApplicationUrl = table.Column<string>(type: "TEXT", nullable: false),
                    EnableSsl = table.Column<bool>(type: "INTEGER", nullable: false),
                    SslPort = table.Column<int>(type: "INTEGER", nullable: false),
                    SslCertPath = table.Column<string>(type: "TEXT", nullable: false),
                    SslCertPassword = table.Column<string>(type: "TEXT", nullable: false),
                    LaunchBrowser = table.Column<bool>(type: "INTEGER", nullable: false),
                    AuthenticationMethod = table.Column<string>(type: "TEXT", nullable: false),
                    AuthenticationRequired = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    Password = table.Column<string>(type: "TEXT", nullable: false),
                    ApiKey = table.Column<string>(type: "TEXT", nullable: false),
                    CertificateValidation = table.Column<string>(type: "TEXT", nullable: false),
                    ProxyEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProxyType = table.Column<string>(type: "TEXT", nullable: false),
                    ProxyHostname = table.Column<string>(type: "TEXT", nullable: false),
                    ProxyPort = table.Column<int>(type: "INTEGER", nullable: false),
                    ProxyUsername = table.Column<string>(type: "TEXT", nullable: false),
                    ProxyPassword = table.Column<string>(type: "TEXT", nullable: false),
                    ProxyBypassFilter = table.Column<string>(type: "TEXT", nullable: false),
                    ProxyBypassLocalAddresses = table.Column<bool>(type: "INTEGER", nullable: false),
                    LogLevel = table.Column<string>(type: "TEXT", nullable: false),
                    ConsoleLogLevel = table.Column<string>(type: "TEXT", nullable: false),
                    LogSizeLimit = table.Column<int>(type: "INTEGER", nullable: false),
                    AnalyticsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Branch = table.Column<string>(type: "TEXT", nullable: false),
                    UpdateAutomatically = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdateMechanism = table.Column<string>(type: "TEXT", nullable: false),
                    UpdateScriptPath = table.Column<string>(type: "TEXT", nullable: false),
                    BackupFolder = table.Column<string>(type: "TEXT", nullable: false),
                    BackupInterval = table.Column<int>(type: "INTEGER", nullable: false),
                    BackupRetention = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Label = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UiConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirstDayOfWeek = table.Column<int>(type: "INTEGER", nullable: false),
                    CalendarWeekColumnHeader = table.Column<string>(type: "TEXT", nullable: false),
                    ShortDateFormat = table.Column<string>(type: "TEXT", nullable: false),
                    LongDateFormat = table.Column<string>(type: "TEXT", nullable: false),
                    TimeFormat = table.Column<string>(type: "TEXT", nullable: false),
                    ShowRelativeDates = table.Column<bool>(type: "INTEGER", nullable: false),
                    Theme = table.Column<string>(type: "TEXT", nullable: false),
                    EnableColorImpairedMode = table.Column<bool>(type: "INTEGER", nullable: false),
                    UiLanguage = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UiConfig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VideoFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VideoId = table.Column<int>(type: "INTEGER", nullable: false),
                    ChannelId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlaylistId = table.Column<int>(type: "INTEGER", nullable: true),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    DateAdded = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Videos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChannelId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlaylistId = table.Column<int>(type: "INTEGER", nullable: true),
                    YoutubeVideoId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "TEXT", nullable: true),
                    UploadDateUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    AirDateUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    AirDate = table.Column<string>(type: "TEXT", nullable: false),
                    Overview = table.Column<string>(type: "TEXT", nullable: true),
                    Runtime = table.Column<int>(type: "INTEGER", nullable: false),
                    Monitored = table.Column<bool>(type: "INTEGER", nullable: false),
                    Added = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Videos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YouTubeConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ApiKey = table.Column<string>(type: "TEXT", nullable: false),
                    UseYouTubeApi = table.Column<bool>(type: "INTEGER", nullable: false),
                    ApiPriorityMetadataItemsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YouTubeConfig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YtDlpConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExecutablePath = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YtDlpConfig", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Channels_YoutubeChannelId",
                table: "Channels",
                column: "YoutubeChannelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommandQueueJobs_CommandId",
                table: "CommandQueueJobs",
                column: "CommandId");

            migrationBuilder.CreateIndex(
                name: "IX_CommandQueueJobs_Status",
                table: "CommandQueueJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadHistory_ChannelId",
                table: "DownloadHistory",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadHistory_Date",
                table: "DownloadHistory",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadHistory_EventType",
                table: "DownloadHistory",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadHistory_PlaylistId",
                table: "DownloadHistory",
                column: "PlaylistId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadHistory_VideoId",
                table: "DownloadHistory",
                column: "VideoId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadQueue_ChannelId",
                table: "DownloadQueue",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadQueue_Status",
                table: "DownloadQueue",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ImportExclusions_YoutubeChannelId",
                table: "ImportExclusions",
                column: "YoutubeChannelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Playlists_ChannelId",
                table: "Playlists",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_Playlists_YoutubePlaylistId",
                table: "Playlists",
                column: "YoutubePlaylistId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VideoFiles_ChannelId",
                table: "VideoFiles",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoFiles_PlaylistId",
                table: "VideoFiles",
                column: "PlaylistId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoFiles_VideoId",
                table: "VideoFiles",
                column: "VideoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Videos_ChannelId",
                table: "Videos",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_Videos_PlaylistId",
                table: "Videos",
                column: "PlaylistId");

            migrationBuilder.CreateIndex(
                name: "IX_Videos_UploadDateUtc",
                table: "Videos",
                column: "UploadDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Videos_YoutubeVideoId",
                table: "Videos",
                column: "YoutubeVideoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Channels");

            migrationBuilder.DropTable(
                name: "CommandQueueJobs");

            migrationBuilder.DropTable(
                name: "CustomFilters");

            migrationBuilder.DropTable(
                name: "DownloadHistory");

            migrationBuilder.DropTable(
                name: "DownloadQueue");

            migrationBuilder.DropTable(
                name: "FFmpegConfig");

            migrationBuilder.DropTable(
                name: "ImportExclusions");

            migrationBuilder.DropTable(
                name: "ImportListOptionsConfig");

            migrationBuilder.DropTable(
                name: "MediaManagementConfig");

            migrationBuilder.DropTable(
                name: "NamingConfig");

            migrationBuilder.DropTable(
                name: "Playlists");

            migrationBuilder.DropTable(
                name: "QualityProfiles");

            migrationBuilder.DropTable(
                name: "RootFolders");

            migrationBuilder.DropTable(
                name: "ServerSettings");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "UiConfig");

            migrationBuilder.DropTable(
                name: "VideoFiles");

            migrationBuilder.DropTable(
                name: "Videos");

            migrationBuilder.DropTable(
                name: "YouTubeConfig");

            migrationBuilder.DropTable(
                name: "YtDlpConfig");
        }
    }
}
