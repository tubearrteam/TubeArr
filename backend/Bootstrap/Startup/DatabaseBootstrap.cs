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

		var configuration = scope.ServiceProvider.GetService<IConfiguration>();
		if (configuration is not null)
			TryApplyBundledFfmpegPath(db, configuration, logger);

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
	/// </summary>
	static void TryApplyBundledFfmpegPath(TubeArrDbContext db, IConfiguration configuration, ILogger logger)
	{
		var bundled = configuration["TubeArr:BundledFfmpegPath"]?.Trim();
		if (string.IsNullOrEmpty(bundled) || !File.Exists(bundled))
			return;

		var row = db.FFmpegConfig.OrderBy(x => x.Id).FirstOrDefault();
		var current = (row?.ExecutablePath ?? "").Trim();
		if (!string.IsNullOrEmpty(current) && File.Exists(current))
			return;

		if (row is null)
			db.FFmpegConfig.Add(new FFmpegConfigEntity { Id = 1, ExecutablePath = bundled, Enabled = true });
		else
		{
			row.ExecutablePath = bundled;
			row.Enabled = true;
		}

		db.SaveChanges();
		logger.LogInformation("FFmpeg executable path applied from bundled default: {Path}", bundled);
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