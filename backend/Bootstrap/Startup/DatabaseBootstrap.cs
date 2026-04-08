using System.Data;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
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

		TryRepairSqliteMetadataQueueAndHistory(db, logger);

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
	/// When the EF migrations history records <c>AddMetadataQueueAndMetadataHistory</c> (or later) but the tables were never created
	/// (restore, copy-paste DB file, or partial failure), command workers crash querying <c>MetadataQueue</c>.
	/// </summary>
	static void TryRepairSqliteMetadataQueueAndHistory(TubeArrDbContext db, ILogger logger)
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
				var historyExists = SqliteMasterTableExists(conn, "MetadataHistory");
				var queueExists = SqliteMasterTableExists(conn, "MetadataQueue");
				if (historyExists && queueExists)
					return;

				logger.LogWarning(
					"SQLite schema repair: MetadataQueue and/or MetadataHistory missing; creating tables to match migration AddMetadataQueueAndMetadataHistory.");

				if (!historyExists)
				{
					SqliteExec(conn,
						"""
						CREATE TABLE "MetadataHistory" (
							"Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
							"AcquisitionMethodsJson" TEXT NOT NULL,
							"ChannelId" INTEGER,
							"CommandId" INTEGER,
							"CommandQueueJobId" INTEGER,
							"EndedAtUtc" TEXT,
							"JobType" TEXT NOT NULL,
							"Message" TEXT,
							"Name" TEXT NOT NULL,
							"PayloadJson" TEXT NOT NULL,
							"QueuedAtUtc" TEXT NOT NULL,
							"ResultStatus" TEXT NOT NULL,
							"StartedAtUtc" TEXT);
						""");
					SqliteExec(conn, """CREATE INDEX "IX_MetadataHistory_ChannelId" ON "MetadataHistory" ("ChannelId");""");
					SqliteExec(conn, """CREATE INDEX "IX_MetadataHistory_CommandId" ON "MetadataHistory" ("CommandId");""");
					SqliteExec(conn, """CREATE INDEX "IX_MetadataHistory_CommandQueueJobId" ON "MetadataHistory" ("CommandQueueJobId");""");
					SqliteExec(conn, """CREATE INDEX "IX_MetadataHistory_EndedAtUtc" ON "MetadataHistory" ("EndedAtUtc");""");
				}

				if (!queueExists)
				{
					SqliteExec(conn,
						"""
						CREATE TABLE "MetadataQueue" (
							"Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
							"AcquisitionMethodsJson" TEXT NOT NULL,
							"ChannelId" INTEGER,
							"CommandId" INTEGER,
							"CommandQueueJobId" INTEGER NOT NULL,
							"EndedAtUtc" TEXT,
							"JobType" TEXT NOT NULL,
							"LastError" TEXT,
							"Name" TEXT NOT NULL,
							"PayloadJson" TEXT NOT NULL,
							"QueuedAtUtc" TEXT NOT NULL,
							"StartedAtUtc" TEXT,
							"Status" TEXT NOT NULL);
						""");
					SqliteExec(conn, """CREATE INDEX "IX_MetadataQueue_ChannelId" ON "MetadataQueue" ("ChannelId");""");
					SqliteExec(conn, """CREATE INDEX "IX_MetadataQueue_CommandId" ON "MetadataQueue" ("CommandId");""");
					SqliteExec(conn, """CREATE UNIQUE INDEX "IX_MetadataQueue_CommandQueueJobId" ON "MetadataQueue" ("CommandQueueJobId");""");
					SqliteExec(conn, """CREATE INDEX "IX_MetadataQueue_Status" ON "MetadataQueue" ("Status");""");
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
			logger.LogError(ex, "SQLite schema repair for MetadataQueue/MetadataHistory failed.");
			throw;
		}
	}

	static bool SqliteMasterTableExists(IDbConnection conn, string tableName)
	{
		using var cmd = conn.CreateCommand();
		cmd.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name=@name";
		var p = cmd.CreateParameter();
		p.ParameterName = "@name";
		p.Value = tableName;
		cmd.Parameters.Add(p);
		return Convert.ToInt64(cmd.ExecuteScalar() ?? 0L) > 0;
	}

	static void SqliteExec(IDbConnection conn, string sql)
	{
		using var cmd = conn.CreateCommand();
		cmd.CommandText = sql.Trim();
		cmd.ExecuteNonQuery();
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