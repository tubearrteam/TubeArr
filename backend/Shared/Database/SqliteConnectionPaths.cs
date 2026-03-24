using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.IO;

namespace TubeArr.Backend;

internal static class SqliteConnectionPaths
{
	/// <summary>
	/// SQLite busy timeout (seconds). When 0, concurrent access fails immediately with "database is locked".
	/// </summary>
	internal static string NormalizeConnectionStringForConcurrency(string connectionString)
	{
		try
		{
			var builder = new SqliteConnectionStringBuilder(connectionString);
			if (builder.DefaultTimeout == 0)
				builder.DefaultTimeout = 30;
			return builder.ConnectionString;
		}
		catch
		{
			return connectionString;
		}
	}

	internal static bool IsSqliteMemoryDatabase(string connectionString)
	{
		try
		{
			var builder = new SqliteConnectionStringBuilder(connectionString);
			var ds = builder.DataSource ?? "";
			if (ds.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
				return true;
			if (ds.Contains("mode=memory", StringComparison.OrdinalIgnoreCase))
				return true;
			return false;
		}
		catch
		{
			return false;
		}
	}

	internal static bool TryGetDatabaseFilePath(string connectionString, string contentRootPath, out string fullPath)
	{
		fullPath = "";
		try
		{
			var builder = new SqliteConnectionStringBuilder(connectionString);
			var ds = builder.DataSource;
			if (string.IsNullOrWhiteSpace(ds))
				return false;

			fullPath = Path.IsPathRooted(ds)
				? Path.GetFullPath(ds)
				: Path.GetFullPath(Path.Combine(contentRootPath, ds));
			return true;
		}
		catch
		{
			return false;
		}
	}

	internal static string GetPendingRestorePath(string mainDatabasePath) => mainDatabasePath + ".restorepending";

	internal static bool TryValidateSqliteFile(string dbPath)
	{
		if (!File.Exists(dbPath))
			return false;

		try
		{
			using var connection = new SqliteConnection($"Data Source={dbPath}");
			connection.Open();
			using var cmd = connection.CreateCommand();
			cmd.CommandText = "PRAGMA schema_version;";
			_ = cmd.ExecuteScalar();
			return true;
		}
		catch
		{
			return false;
		}
	}

	internal static void ApplyPendingRestoreIfPresent(string connectionString, string contentRootPath, ILogger? logger)
	{
		if (!TryGetDatabaseFilePath(connectionString, contentRootPath, out var mainPath))
			return;

		var pendingPath = GetPendingRestorePath(mainPath);
		if (!File.Exists(pendingPath))
			return;

		if (!TryValidateSqliteFile(pendingPath))
		{
			try
			{
				File.Delete(pendingPath);
			}
			catch
			{
				// ignore
			}

			logger?.LogWarning("Removed invalid pending database restore file at {Path}.", pendingPath);
			return;
		}

		try
		{
			SqliteConnection.ClearAllPools();

			if (File.Exists(mainPath))
				File.Delete(mainPath);

			File.Move(pendingPath, mainPath);
			logger?.LogInformation("Applied pending database restore to {Path}.", mainPath);
		}
		catch (Exception ex)
		{
			logger?.LogError(ex, "Failed to apply pending database restore from {Pending}.", pendingPath);
			try
			{
				if (File.Exists(pendingPath))
					File.Delete(pendingPath);
			}
			catch
			{
				// ignore
			}
		}
	}
}
