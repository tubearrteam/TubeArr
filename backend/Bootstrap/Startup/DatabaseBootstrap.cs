using System.Data;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class DatabaseBootstrap
{
	public static void EnsureDatabaseInitialized(IServiceProvider services)
	{
		using var scope = services.CreateScope();
		var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseBootstrap");
		var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();

		var sw = Stopwatch.StartNew();
		logger.LogInformation("Database initialization starting.");

		db.Database.Migrate();

		var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
		TryApplyBundledFfmpegPath(db, configuration, logger);
		TryApplyBundledYtDlpPath(db, configuration, logger);

		TryRepairSqliteYtDlpConfigDownloadQueueParallelWorkers(db, logger);
		TryRepairSqliteDownloadQueueExternalAcquisitionAndSlskd(db, logger);

		TryEnableSqliteWal(db, logger);

		logger.LogInformation("Database migration completed in {ElapsedMs} ms.", sw.ElapsedMilliseconds);
	}

	public static void RunDeferredMaintenance(IServiceProvider services, CancellationToken cancellationToken)
	{
		using var scope = services.CreateScope();
		var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseBootstrap");
		var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();

		RunDeferredMaintenance(db, logger, cancellationToken);
	}

	public static void RunDeferredMaintenance(
		TubeArrDbContext db,
		ILogger logger,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var sw = Stopwatch.StartNew();
		logger.LogInformation("Deferred database maintenance starting.");

		var movedCount = ProgramDbQueueHelpers.MoveCompletedQueueItemsToHistoryBatched(db, logger: logger);
		logger.LogInformation(
			"Deferred queue/history cleanup completed. Moved {MovedCount} queue rows in {ElapsedMs} ms.",
			movedCount,
			sw.ElapsedMilliseconds);
	}

	/// <summary>
	/// When the EF history lists a migration as applied but the SQLite file predates the DDL (restore, manual edit, or failed mid-migration),
	/// <see cref="YtDlpConfigEntity.DownloadQueueParallelWorkers"/> may be missing while the model expects it.
	/// </summary>
	static void TryRepairSqliteYtDlpConfigDownloadQueueParallelWorkers(TubeArrDbContext db, ILogger logger)
	{
		if (db.Database.ProviderName is not string pn || !pn.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
			return;

		try
		{
			var conn = db.Database.GetDbConnection();
			var wasOpen = conn.State == ConnectionState.Open;
			if (!wasOpen)
				conn.Open();
			try
			{
				using var check = conn.CreateCommand();
				check.CommandText = "SELECT COUNT(1) FROM pragma_table_info('YtDlpConfig') WHERE name='DownloadQueueParallelWorkers'";
				var exists = Convert.ToInt64(check.ExecuteScalar() ?? 0L) > 0;
				if (exists)
					return;

				logger.LogWarning(
					"SQLite schema repair: YtDlpConfig.DownloadQueueParallelWorkers column missing; adding it (INTEGER NOT NULL DEFAULT 1).");

				using var alter = conn.CreateCommand();
				alter.CommandText = "ALTER TABLE YtDlpConfig ADD COLUMN DownloadQueueParallelWorkers INTEGER NOT NULL DEFAULT 1";
				alter.ExecuteNonQuery();
			}
			finally
			{
				if (!wasOpen)
					conn.Close();
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "SQLite schema repair for YtDlpConfig.DownloadQueueParallelWorkers failed.");
			throw;
		}
	}

	/// <summary>
	/// When <c>__EFMigrationsHistory</c> lists <c>AddSlskdConfigAndExternalAcquisition</c> as applied but the SQLite file
	/// never got the DDL (restore, manual edit, or failed migration), <see cref="DownloadQueueEntity"/> columns and
	/// <c>SlskdConfig</c> may be missing while the model expects them.
	/// </summary>
	static void TryRepairSqliteDownloadQueueExternalAcquisitionAndSlskd(TubeArrDbContext db, ILogger logger)
	{
		if (db.Database.ProviderName is not string pn || !pn.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
			return;

		try
		{
			var conn = db.Database.GetDbConnection();
			var wasOpen = conn.State == ConnectionState.Open;
			if (!wasOpen)
				conn.Open();
			try
			{
				static bool ColumnExists(IDbConnection connection, string table, string column)
				{
					using var check = connection.CreateCommand();
					check.CommandText = $"SELECT COUNT(1) FROM pragma_table_info('{table}') WHERE name='{column}'";
					return Convert.ToInt64(check.ExecuteScalar() ?? 0L) > 0;
				}

				if (!ColumnExists(conn, "DownloadQueue", "ExternalAcquisitionJson"))
				{
					logger.LogWarning(
						"SQLite schema repair: DownloadQueue.ExternalAcquisitionJson missing; adding column (TEXT NULL).");
					using var alter = conn.CreateCommand();
					alter.CommandText = "ALTER TABLE DownloadQueue ADD COLUMN ExternalAcquisitionJson TEXT NULL";
					alter.ExecuteNonQuery();
				}

				if (!ColumnExists(conn, "DownloadQueue", "ExternalWorkPending"))
				{
					logger.LogWarning(
						"SQLite schema repair: DownloadQueue.ExternalWorkPending missing; adding column (INTEGER NOT NULL DEFAULT 0).");
					using var alter = conn.CreateCommand();
					alter.CommandText = "ALTER TABLE DownloadQueue ADD COLUMN ExternalWorkPending INTEGER NOT NULL DEFAULT 0";
					alter.ExecuteNonQuery();
				}

				using (var indexCheck = conn.CreateCommand())
				{
					indexCheck.CommandText =
						"SELECT COUNT(1) FROM sqlite_master WHERE type='index' AND name='IX_DownloadQueue_Status_ExternalWorkPending'";
					var indexExists = Convert.ToInt64(indexCheck.ExecuteScalar() ?? 0L) > 0;
					if (!indexExists)
					{
						logger.LogWarning("SQLite schema repair: creating IX_DownloadQueue_Status_ExternalWorkPending.");
						using var idx = conn.CreateCommand();
						idx.CommandText =
							"CREATE INDEX IF NOT EXISTS IX_DownloadQueue_Status_ExternalWorkPending ON DownloadQueue (Status, ExternalWorkPending)";
						idx.ExecuteNonQuery();
					}
				}

				using (var tableCheck = conn.CreateCommand())
				{
					tableCheck.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name='SlskdConfig'";
					var slskdExists = Convert.ToInt64(tableCheck.ExecuteScalar() ?? 0L) > 0;
					if (!slskdExists)
					{
						logger.LogWarning("SQLite schema repair: SlskdConfig table missing; creating with default row.");
						using var create = conn.CreateCommand();
						create.CommandText =
							"""
							CREATE TABLE "SlskdConfig" (
								"Id" INTEGER NOT NULL CONSTRAINT "PK_SlskdConfig" PRIMARY KEY AUTOINCREMENT,
								"Enabled" INTEGER NOT NULL,
								"BaseUrl" TEXT NOT NULL,
								"ApiKey" TEXT NOT NULL,
								"LocalDownloadsPath" TEXT NOT NULL,
								"SearchTimeoutSeconds" INTEGER NOT NULL,
								"MaxCandidatesStored" INTEGER NOT NULL,
								"AutoPickMinScore" INTEGER NOT NULL,
								"ManualReviewOnly" INTEGER NOT NULL,
								"RetryAttempts" INTEGER NOT NULL,
								"AcquisitionOrder" INTEGER NOT NULL,
								"FallbackToSlskdOnYtDlpFailure" INTEGER NOT NULL,
								"FallbackToYtDlpOnSlskdFailure" INTEGER NOT NULL,
								"HigherQualityHandling" INTEGER NOT NULL,
								"RequireManualReviewOnTranscode" INTEGER NOT NULL,
								"KeepOriginalAfterTranscode" INTEGER NOT NULL
							);
							""";
						create.ExecuteNonQuery();

						using var insert = conn.CreateCommand();
						insert.CommandText =
							"""
							INSERT INTO "SlskdConfig" ("Id", "Enabled", "BaseUrl", "ApiKey", "LocalDownloadsPath", "SearchTimeoutSeconds", "MaxCandidatesStored", "AutoPickMinScore", "ManualReviewOnly", "RetryAttempts", "AcquisitionOrder", "FallbackToSlskdOnYtDlpFailure", "FallbackToYtDlpOnSlskdFailure", "HigherQualityHandling", "RequireManualReviewOnTranscode", "KeepOriginalAfterTranscode")
							VALUES (1, 0, '', '', '', 30, 50, 85, 1, 2, 0, 1, 1, 0, 1, 0);
							""";
						insert.ExecuteNonQuery();
					}
				}
			}
			finally
			{
				if (!wasOpen)
					conn.Close();
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "SQLite schema repair for DownloadQueue external acquisition / SlskdConfig failed.");
			throw;
		}
	}

	/// <summary>
	/// When <c>TubeArr:BundledFfmpegPath</c> is set (e.g. Docker <c>ENV TubeArr__BundledFfmpegPath=/usr/bin/ffmpeg</c>),
	/// use it if FFmpeg is not configured or the saved path no longer exists (e.g. ephemeral download under content root).
	/// Uses the single application row <see cref="FFmpegConfigEntity"/> with <c>Id = 1</c>, matching API get-or-create behavior.
	/// </summary>
	static void TryApplyBundledFfmpegPath(TubeArrDbContext db, IConfiguration configuration, ILogger logger)
	{
		var bundled = configuration["TubeArr:BundledFfmpegPath"]?.Trim();
		if (string.IsNullOrEmpty(bundled))
			return;

		if (!File.Exists(bundled))
		{
			logger.LogWarning(
				"TubeArr:BundledFfmpegPath is set to '{BundledPath}' but that file does not exist; skipping bundled FFmpeg bootstrap.",
				bundled);
			return;
		}

		const int ffmpegConfigId = 1;
		var row = db.FFmpegConfig.Find(ffmpegConfigId);
		var current = (row?.ExecutablePath ?? "").Trim();
		if (!string.IsNullOrEmpty(current) && File.Exists(current))
			return;

		bool enabledAfter;
		if (row is null)
		{
			row = new FFmpegConfigEntity
			{
				Id = ffmpegConfigId,
				ExecutablePath = bundled,
				Enabled = true
			};
			db.FFmpegConfig.Add(row);
			enabledAfter = row.Enabled;
		}
		else
		{
			row.ExecutablePath = bundled;
			enabledAfter = row.Enabled;
		}

		db.SaveChanges();
		logger.LogInformation(
			"FFmpeg executable path set to bundled default '{BundledPath}' (Enabled={Enabled}).",
			bundled,
			enabledAfter);
	}

	/// <summary>
	/// When <c>TubeArr:BundledYtDlpPath</c> is set (e.g. Docker <c>ENV TubeArr__BundledYtDlpPath=/usr/local/bin/yt-dlp</c>),
	/// use it if yt-dlp is not configured or the saved path no longer exists.
	/// Uses the single application row <see cref="YtDlpConfigEntity"/> with <c>Id = 1</c>, matching API get-or-create behavior.
	/// </summary>
	static void TryApplyBundledYtDlpPath(TubeArrDbContext db, IConfiguration configuration, ILogger logger)
	{
		var bundled = configuration["TubeArr:BundledYtDlpPath"]?.Trim();
		if (string.IsNullOrEmpty(bundled))
			return;

		if (!File.Exists(bundled))
		{
			logger.LogWarning(
				"TubeArr:BundledYtDlpPath is set to '{BundledPath}' but that file does not exist; skipping bundled yt-dlp bootstrap.",
				bundled);
			return;
		}

		const int ytDlpConfigId = 1;
		var row = db.YtDlpConfig.Find(ytDlpConfigId);
		var current = (row?.ExecutablePath ?? "").Trim();
		if (!string.IsNullOrEmpty(current) && File.Exists(current))
			return;

		bool enabledAfter;
		if (row is null)
		{
			row = new YtDlpConfigEntity
			{
				Id = ytDlpConfigId,
				ExecutablePath = bundled,
				Enabled = true
			};
			db.YtDlpConfig.Add(row);
			enabledAfter = row.Enabled;
		}
		else
		{
			row.ExecutablePath = bundled;
			enabledAfter = row.Enabled;
		}

		db.SaveChanges();
		logger.LogInformation(
			"yt-dlp executable path set to bundled default '{BundledPath}' (Enabled={Enabled}).",
			bundled,
			enabledAfter);
	}

	static void TryEnableSqliteWal(TubeArrDbContext db, ILogger logger)
	{
		if (db.Database.ProviderName is not string pn || !pn.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
			return;

		var cs = db.Database.GetConnectionString();
		if (string.IsNullOrWhiteSpace(cs) || SqliteConnectionPaths.IsSqliteMemoryDatabase(cs))
			return;

		try
		{
			db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Could not enable SQLite WAL journal mode.");
		}
	}
}