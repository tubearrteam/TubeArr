using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class SystemStatusHelpers
{
	public static async Task<Dictionary<string, object?>> BuildSystemStatusAsync(
		TubeArrDbContext db,
		IWebHostEnvironment webHost,
		IHostEnvironment hostEnv,
		ServerSettingsEntity serverSettings,
		string preloadedUrlBase)
	{
		var connectionString = db.Database.GetConnectionString()
			?? throw new InvalidOperationException("Database connection string is not available.");

		var appDataDir = ResolveDatabaseDirectory(connectionString, webHost.ContentRootPath);
		var startupPath = Path.GetDirectoryName(Environment.ProcessPath)
			?? webHost.ContentRootPath;

		var sqliteVersion = await QuerySqliteScalarAsync(db, "SELECT sqlite_version();") ?? "";
		var migrationCount = await QueryMigrationCountAsync(db);

		var version = ReadInformationalVersion();
		var buildTime = ReadAssemblyBuildTimeUtc();

		var startTimeUtc = TimeZoneInfo.ConvertTimeToUtc(Process.GetCurrentProcess().StartTime);

		return new Dictionary<string, object?>
		{
			["version"] = version,
			["buildTime"] = buildTime,
			["isDebug"] = hostEnv.IsDevelopment(),
			["isProduction"] = hostEnv.IsProduction(),
			["isAdmin"] = true,
			["appName"] = "TubeArr",
			["instanceName"] = serverSettings.InstanceName ?? "",
			["authentication"] = "apiKey",
			["branch"] = "",
			["databaseType"] = "sqlite",
			["databaseVersion"] = sqliteVersion,
			["sqliteVersion"] = sqliteVersion,
			["migrationVersion"] = migrationCount,
			["appData"] = appDataDir,
			["startupPath"] = startupPath,
			["mode"] = hostEnv.EnvironmentName,
			["startTime"] = startTimeUtc.ToString("O"),
			["urlBase"] = ProgramStartupHelpers.NormalizeUrlBase(serverSettings.UrlBase),
			["isWindows"] = OperatingSystem.IsWindows(),
			["isLinux"] = OperatingSystem.IsLinux(),
			["isOsx"] = OperatingSystem.IsMacOS(),
			["isDocker"] = IsDocker(),
			["isNetCore"] = true,
			["isUserInteractive"] = Environment.UserInteractive,
			["osName"] = RuntimeInformation.OSDescription,
			["osVersion"] = Environment.OSVersion.VersionString,
			["runtimeName"] = ".NET",
			["runtimeVersion"] = Environment.Version.ToString(),
			["packageVersion"] = "",
			["packageAuthor"] = "",
			["packageUpdateMechanism"] = "builtIn",
			["packageUpdateMechanismMessage"] = ""
		};
	}

	public static List<Dictionary<string, object?>> BuildDiskSpace()
	{
		var rows = new List<Dictionary<string, object?>>();
		var drives = DriveInfo.GetDrives()
			.Where(d => d.IsReady)
			.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase);

		foreach (var drive in drives)
		{
			try
			{
				rows.Add(new Dictionary<string, object?>
				{
					["path"] = drive.Name,
					["label"] = drive.VolumeLabel ?? "",
					["freeSpace"] = drive.AvailableFreeSpace,
					["totalSpace"] = drive.TotalSize
				});
			}
			catch
			{
				// Skip drives that report readiness but fail on space queries.
			}
		}

		return rows;
	}

	static string ResolveDatabaseDirectory(string connectionString, string contentRoot)
	{
		try
		{
			var builder = new SqliteConnectionStringBuilder(connectionString);
			var dataSource = builder.DataSource;
			if (string.IsNullOrWhiteSpace(dataSource))
			{
				return Path.GetFullPath(contentRoot);
			}

			var fullPath = Path.IsPathRooted(dataSource)
				? dataSource
				: Path.GetFullPath(Path.Combine(contentRoot, dataSource));

			var dir = Path.GetDirectoryName(fullPath);
			return string.IsNullOrEmpty(dir) ? Path.GetFullPath(contentRoot) : dir;
		}
		catch
		{
			return Path.GetFullPath(contentRoot);
		}
	}

	static async Task<string?> QuerySqliteScalarAsync(TubeArrDbContext db, string sql)
	{
		var conn = db.Database.GetDbConnection();
		var opened = false;
		if (conn.State != System.Data.ConnectionState.Open)
		{
			await conn.OpenAsync();
			opened = true;
		}

		try
		{
			await using var cmd = conn.CreateCommand();
			cmd.CommandText = sql;
			var result = await cmd.ExecuteScalarAsync();
			return result?.ToString();
		}
		finally
		{
			if (opened)
			{
				await conn.CloseAsync();
			}
		}
	}

	static async Task<int> QueryMigrationCountAsync(TubeArrDbContext db)
	{
		try
		{
			var conn = db.Database.GetDbConnection();
			var opened = false;
			if (conn.State != System.Data.ConnectionState.Open)
			{
				await conn.OpenAsync();
				opened = true;
			}

			try
			{
				await using var cmd = conn.CreateCommand();
				cmd.CommandText = "SELECT COUNT(*) FROM __EFMigrationsHistory;";
				var result = await cmd.ExecuteScalarAsync();
				return Convert.ToInt32(result);
			}
			finally
			{
				if (opened)
				{
					await conn.CloseAsync();
				}
			}
		}
		catch
		{
			return 0;
		}
	}

	static string ReadInformationalVersion()
	{
		var asm = Assembly.GetExecutingAssembly();
		var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
		if (!string.IsNullOrWhiteSpace(info))
		{
			var plus = info.IndexOf('+', StringComparison.Ordinal);
			return plus > 0 ? info[..plus] : info;
		}

		var v = asm.GetName().Version;
		if (v is null || (v.Major == 0 && v.Minor == 0 && v.Build == 0 && v.Revision == 0))
		{
			return "0.0.0-dev";
		}

		return v.ToString();
	}

	static string ReadAssemblyBuildTimeUtc()
	{
		try
		{
			var path = Assembly.GetExecutingAssembly().Location;
			if (string.IsNullOrEmpty(path))
			{
				return "";
			}

			return File.GetLastWriteTimeUtc(path).ToString("O");
		}
		catch
		{
			return "";
		}
	}

	static bool IsDocker()
	{
		if (string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		try
		{
			return OperatingSystem.IsLinux() && File.Exists("/.dockerenv");
		}
		catch
		{
			return false;
		}
	}
}
