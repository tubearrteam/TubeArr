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