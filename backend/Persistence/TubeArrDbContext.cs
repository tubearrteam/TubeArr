using Microsoft.EntityFrameworkCore;

namespace TubeArr.Backend.Data;

public sealed class TubeArrDbContext : DbContext
{
	public TubeArrDbContext(DbContextOptions<TubeArrDbContext> options)
		: base(options)
	{
	}

	public DbSet<ChannelEntity> Channels => Set<ChannelEntity>();
	public DbSet<ChannelTagEntity> ChannelTags => Set<ChannelTagEntity>();
	public DbSet<ChannelCustomPlaylistEntity> ChannelCustomPlaylists => Set<ChannelCustomPlaylistEntity>();
	public DbSet<PlaylistEntity> Playlists => Set<PlaylistEntity>();
	public DbSet<PlaylistVideoEntity> PlaylistVideos => Set<PlaylistVideoEntity>();
	public DbSet<VideoEntity> Videos => Set<VideoEntity>();
	public DbSet<ServerSettingsEntity> ServerSettings => Set<ServerSettingsEntity>();
	public DbSet<UiConfigEntity> UiConfig => Set<UiConfigEntity>();
	public DbSet<MediaManagementConfigEntity> MediaManagementConfig => Set<MediaManagementConfigEntity>();
	public DbSet<PlexProviderConfigEntity> PlexProviderConfig => Set<PlexProviderConfigEntity>();
	public DbSet<NamingConfigEntity> NamingConfig => Set<NamingConfigEntity>();
	public DbSet<QualityProfileEntity> QualityProfiles => Set<QualityProfileEntity>();
	public DbSet<YtDlpConfigEntity> YtDlpConfig => Set<YtDlpConfigEntity>();
	public DbSet<FFmpegConfigEntity> FFmpegConfig => Set<FFmpegConfigEntity>();
	public DbSet<YouTubeConfigEntity> YouTubeConfig => Set<YouTubeConfigEntity>();
	public DbSet<RootFolderEntity> RootFolders => Set<RootFolderEntity>();
	public DbSet<DownloadQueueEntity> DownloadQueue => Set<DownloadQueueEntity>();
	public DbSet<DownloadHistoryEntity> DownloadHistory => Set<DownloadHistoryEntity>();
	public DbSet<VideoFileEntity> VideoFiles => Set<VideoFileEntity>();
	public DbSet<CommandQueueJobEntity> CommandQueueJobs => Set<CommandQueueJobEntity>();
	public DbSet<TagEntity> Tags => Set<TagEntity>();
	public DbSet<CustomFilterEntity> CustomFilters => Set<CustomFilterEntity>();
	public DbSet<ScheduledTaskStateEntity> ScheduledTaskStates => Set<ScheduledTaskStateEntity>();
	public DbSet<ScheduledTaskRunHistoryEntity> ScheduledTaskRunHistory => Set<ScheduledTaskRunHistoryEntity>();
	public DbSet<ScheduledTaskIntervalOverrideEntity> ScheduledTaskIntervalOverrides => Set<ScheduledTaskIntervalOverrideEntity>();
	public DbSet<NotificationConnectionEntity> NotificationConnections => Set<NotificationConnectionEntity>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<ChannelEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.HasIndex(x => x.YoutubeChannelId).IsUnique();
			entity.Property(x => x.YoutubeChannelId).IsRequired();
			entity.Property(x => x.Title).IsRequired();
			entity.Property(x => x.TitleSlug).IsRequired();
			entity.Property(x => x.ThumbnailUrl);
		});

		modelBuilder.Entity<ChannelTagEntity>(entity =>
		{
			entity.HasKey(x => new { x.ChannelId, x.TagId });
			entity.HasIndex(x => x.TagId);
			entity.HasOne<ChannelEntity>()
				.WithMany()
				.HasForeignKey(x => x.ChannelId)
				.OnDelete(DeleteBehavior.Cascade);
			entity.HasOne<TagEntity>()
				.WithMany()
				.HasForeignKey(x => x.TagId)
				.OnDelete(DeleteBehavior.Restrict);
		});

		modelBuilder.Entity<ChannelCustomPlaylistEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.HasIndex(x => x.ChannelId);
			entity.Property(x => x.Name).IsRequired();
			entity.Property(x => x.RulesJson).IsRequired();
			entity.HasOne<ChannelEntity>()
				.WithMany()
				.HasForeignKey(x => x.ChannelId)
				.OnDelete(DeleteBehavior.Cascade);
		});

		modelBuilder.Entity<PlaylistEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.HasIndex(x => x.ChannelId);
			entity.HasIndex(x => x.YoutubePlaylistId).IsUnique();
			entity.Property(x => x.Title).IsRequired();
			entity.Property(x => x.YoutubePlaylistId).IsRequired();
			entity.Property(x => x.ThumbnailUrl);
			entity.HasIndex(x => new { x.ChannelId, x.SeasonIndex }).IsUnique(false);
		});

		modelBuilder.Entity<PlaylistVideoEntity>(entity =>
		{
			entity.HasKey(x => new { x.PlaylistId, x.VideoId });
			entity.HasIndex(x => x.VideoId);
			entity.HasIndex(x => x.PlaylistId);
			entity.Property(x => x.PlaylistItemId);
			entity.HasOne<PlaylistEntity>()
				.WithMany()
				.HasForeignKey(x => x.PlaylistId)
				.OnDelete(DeleteBehavior.Cascade);
			entity.HasOne<VideoEntity>()
				.WithMany()
				.HasForeignKey(x => x.VideoId)
				.OnDelete(DeleteBehavior.Cascade);
		});

		modelBuilder.Entity<VideoEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.HasIndex(x => x.ChannelId);
			entity.HasIndex(x => x.YoutubeVideoId).IsUnique();
			entity.HasIndex(x => x.UploadDateUtc);
			entity.Property(x => x.Title).IsRequired();
			entity.Property(x => x.YoutubeVideoId).IsRequired();
			entity.Property(x => x.AirDate).IsRequired();
			entity.Property(x => x.ThumbnailUrl);
			entity.HasIndex(x => new { x.ChannelId, x.PlexSeasonIndex, x.PlexEpisodeIndex }).IsUnique(false);
		});

		modelBuilder.Entity<ServerSettingsEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.BindAddress).IsRequired();
			entity.Property(x => x.UrlBase).IsRequired();
			entity.Property(x => x.InstanceName).IsRequired();
			entity.Property(x => x.ApplicationUrl).IsRequired();
			entity.Property(x => x.SslCertPath).IsRequired();
			entity.Property(x => x.SslCertPassword).IsRequired();
			entity.Property(x => x.AuthenticationMethod).IsRequired();
			entity.Property(x => x.AuthenticationRequired).IsRequired();
			entity.Property(x => x.Username).IsRequired();
			entity.Property(x => x.Password).IsRequired();
			entity.Property(x => x.ApiKey).IsRequired();
			entity.Property(x => x.CertificateValidation).IsRequired();
			entity.Property(x => x.ProxyType).IsRequired();
			entity.Property(x => x.ProxyHostname).IsRequired();
			entity.Property(x => x.ProxyUsername).IsRequired();
			entity.Property(x => x.ProxyPassword).IsRequired();
			entity.Property(x => x.ProxyBypassFilter).IsRequired();
			entity.Property(x => x.LogLevel).IsRequired();
			entity.Property(x => x.ConsoleLogLevel).IsRequired();
			entity.Property(x => x.Branch).IsRequired();
			entity.Property(x => x.UpdateMechanism).IsRequired();
			entity.Property(x => x.UpdateScriptPath).IsRequired();
			entity.Property(x => x.BackupFolder).IsRequired();
		});

		modelBuilder.Entity<UiConfigEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.CalendarWeekColumnHeader).IsRequired();
			entity.Property(x => x.ShortDateFormat).IsRequired();
			entity.Property(x => x.LongDateFormat).IsRequired();
			entity.Property(x => x.TimeFormat).IsRequired();
			entity.Property(x => x.Theme).IsRequired();
		});

		modelBuilder.Entity<MediaManagementConfigEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.VideoTitleRequired).IsRequired();
			entity.Property(x => x.DownloadPropersAndRepacks).IsRequired();
			entity.Property(x => x.RescanAfterRefresh).IsRequired();
			entity.Property(x => x.FileDate).IsRequired();
			entity.Property(x => x.ScriptImportPath).IsRequired();
			entity.Property(x => x.ExtraFileExtensions).IsRequired();
			entity.Property(x => x.RecycleBin).IsRequired();
			entity.Property(x => x.ChmodFolder).IsRequired();
			entity.Property(x => x.ChownGroup).IsRequired();
		});

		modelBuilder.Entity<PlexProviderConfigEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.BasePath).IsRequired();
		});

		modelBuilder.Entity<NamingConfigEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.CustomColonReplacementFormat).IsRequired();
			entity.Property(x => x.StandardVideoFormat).IsRequired();
			entity.Property(x => x.DailyVideoFormat).IsRequired();
			entity.Property(x => x.EpisodicVideoFormat).IsRequired();
			entity.Property(x => x.StreamingVideoFormat).IsRequired();
			entity.Property(x => x.ChannelFolderFormat).IsRequired();
			entity.Property(x => x.PlaylistFolderFormat).IsRequired();
			entity.Property(x => x.SpecialsFolderFormat).IsRequired();
		});

		modelBuilder.Entity<QualityProfileEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.Name).IsRequired();
		});

		modelBuilder.Entity<YtDlpConfigEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.ExecutablePath).IsRequired();
		});

		modelBuilder.Entity<FFmpegConfigEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.ExecutablePath).IsRequired();
		});

		modelBuilder.Entity<YouTubeConfigEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.ApiKey).IsRequired();
			entity.Property(x => x.ApiPriorityMetadataItemsJson).IsRequired();
		});

		modelBuilder.Entity<RootFolderEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.Path).IsRequired();
		});

		modelBuilder.Entity<DownloadQueueEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.HasIndex(x => x.Status);
			entity.HasIndex(x => x.ChannelId);
			entity.HasOne<VideoEntity>()
				.WithMany()
				.HasForeignKey(x => x.VideoId)
				.OnDelete(DeleteBehavior.Cascade);
			entity.HasOne<ChannelEntity>()
				.WithMany()
				.HasForeignKey(x => x.ChannelId)
				.OnDelete(DeleteBehavior.Cascade);
		});

		modelBuilder.Entity<DownloadHistoryEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.HasIndex(x => x.ChannelId);
			entity.HasIndex(x => x.VideoId);
			entity.HasIndex(x => x.PlaylistId);
			entity.HasIndex(x => x.EventType);
			entity.HasIndex(x => x.Date);
			entity.Property(x => x.SourceTitle).IsRequired();
			entity.HasOne<VideoEntity>()
				.WithMany()
				.HasForeignKey(x => x.VideoId)
				.OnDelete(DeleteBehavior.Cascade);
			entity.HasOne<ChannelEntity>()
				.WithMany()
				.HasForeignKey(x => x.ChannelId)
				.OnDelete(DeleteBehavior.Cascade);
			entity.HasOne<PlaylistEntity>()
				.WithMany()
				.HasForeignKey(x => x.PlaylistId)
				.OnDelete(DeleteBehavior.SetNull);
		});

		modelBuilder.Entity<VideoFileEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.HasIndex(x => x.VideoId).IsUnique();
			entity.HasIndex(x => x.ChannelId);
			entity.HasIndex(x => x.PlaylistId);
			entity.Property(x => x.Path).IsRequired();
			entity.Property(x => x.RelativePath).IsRequired();
			entity.Property(x => x.MediaInfoJson);
			entity.ToTable("VideoFile");
			entity.HasOne<VideoEntity>()
				.WithMany()
				.HasForeignKey(x => x.VideoId)
				.OnDelete(DeleteBehavior.Cascade);
			entity.HasOne<ChannelEntity>()
				.WithMany()
				.HasForeignKey(x => x.ChannelId)
				.OnDelete(DeleteBehavior.Cascade);
			entity.HasOne<PlaylistEntity>()
				.WithMany()
				.HasForeignKey(x => x.PlaylistId)
				.OnDelete(DeleteBehavior.SetNull);
		});

		modelBuilder.Entity<CommandQueueJobEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.HasIndex(x => x.Status);
			entity.HasIndex(x => x.CommandId);
			entity.HasIndex(x => x.Category);
			entity.HasIndex(x => x.ChannelId);
			entity.Property(x => x.Name).IsRequired();
			entity.Property(x => x.JobType).IsRequired();
			entity.Property(x => x.PayloadJson).IsRequired();
			entity.Property(x => x.Status).IsRequired();
		});

		modelBuilder.Entity<TagEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.Label).IsRequired();
		});

		modelBuilder.Entity<CustomFilterEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.Type).IsRequired();
			entity.Property(x => x.Label).IsRequired();
			entity.Property(x => x.FiltersJson).IsRequired();
		});

		modelBuilder.Entity<ScheduledTaskStateEntity>(entity =>
		{
			entity.HasKey(x => x.TaskName);
			entity.Property(x => x.TaskName).IsRequired();
		});

		modelBuilder.Entity<ScheduledTaskRunHistoryEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.HasIndex(x => x.CompletedAt);
			entity.Property(x => x.TaskName).IsRequired();
		});

		modelBuilder.Entity<ScheduledTaskIntervalOverrideEntity>(entity =>
		{
			entity.HasKey(x => x.TaskName);
			entity.Property(x => x.TaskName).IsRequired();
		});

		modelBuilder.Entity<NotificationConnectionEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.PayloadJson).IsRequired();
		});
	}
}

