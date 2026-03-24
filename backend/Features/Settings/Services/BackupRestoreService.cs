using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

public sealed class BackupRestoreService
{
	readonly IConfiguration _configuration;
	readonly IWebHostEnvironment _environment;

	public BackupRestoreService(IConfiguration configuration, IWebHostEnvironment environment)
	{
		_configuration = configuration;
		_environment = environment;
	}

	string ConnectionString =>
		_configuration.GetConnectionString("TubeArr")
		?? $"Data Source={Path.Combine(_environment.ContentRootPath, "TubeArr.db")}";

	public string ResolveBackupDirectory(ServerSettingsEntity settings)
	{
		var configured = (settings.BackupFolder ?? "").Trim();
		if (!string.IsNullOrEmpty(configured))
		{
			var full = Path.IsPathRooted(configured)
				? Path.GetFullPath(configured)
				: Path.GetFullPath(Path.Combine(_environment.ContentRootPath, configured));

			Directory.CreateDirectory(full);
			return full;
		}

		var fallback = Path.Combine(_environment.ContentRootPath, "Backups");
		Directory.CreateDirectory(fallback);
		return Path.GetFullPath(fallback);
	}

	static string ManifestPath(string backupDir) => Path.Combine(backupDir, "backups.json");

	sealed class BackupManifest
	{
		[JsonPropertyName("entries")]
		public List<BackupManifestEntry> Entries { get; set; } = new();
	}

	sealed class BackupManifestEntry
	{
		[JsonPropertyName("id")]
		public int Id { get; set; }

		[JsonPropertyName("fileName")]
		public string FileName { get; set; } = "";

		[JsonPropertyName("createdUtc")]
		public DateTimeOffset CreatedUtc { get; set; }

		[JsonPropertyName("type")]
		public string Type { get; set; } = "manual";
	}

	BackupManifest LoadManifest(string backupDir)
	{
		var path = ManifestPath(backupDir);
		if (!File.Exists(path))
			return new BackupManifest();

		try
		{
			var json = File.ReadAllText(path);
			return JsonSerializer.Deserialize<BackupManifest>(json) ?? new BackupManifest();
		}
		catch
		{
			return new BackupManifest();
		}
	}

	void SaveManifest(string backupDir, BackupManifest manifest)
	{
		var path = ManifestPath(backupDir);
		var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
		File.WriteAllText(path, json);
	}

	void ReconcileManifestWithDisk(string backupDir, BackupManifest manifest)
	{
		var valid = new List<BackupManifestEntry>();
		foreach (var e in manifest.Entries)
		{
			var fp = Path.Combine(backupDir, e.FileName);
			if (File.Exists(fp))
				valid.Add(e);
		}

		var knownNames = new HashSet<string>(valid.Select(x => x.FileName), StringComparer.OrdinalIgnoreCase);
		var nextId = valid.Count == 0 ? 1 : valid.Max(x => x.Id) + 1;
		foreach (var zipPath in Directory.EnumerateFiles(backupDir, "*.zip", SearchOption.TopDirectoryOnly))
		{
			var name = Path.GetFileName(zipPath);
			if (knownNames.Contains(name))
				continue;

			valid.Add(new BackupManifestEntry
			{
				Id = nextId,
				FileName = name,
				CreatedUtc = File.GetCreationTimeUtc(zipPath),
				Type = "manual"
			});
			knownNames.Add(name);
			nextId++;
		}

		valid.Sort((a, b) => b.CreatedUtc.CompareTo(a.CreatedUtc));
		manifest.Entries = valid;
	}

	int AllocateNextId(BackupManifest manifest)
	{
		return manifest.Entries.Count == 0 ? 1 : manifest.Entries.Max(x => x.Id) + 1;
	}

	public async Task<(bool Success, string Message)> CreateBackupAsync(
		IServiceScopeFactory scopeFactory,
		string trigger,
		string backupType,
		CancellationToken cancellationToken = default)
	{
		string backupDir;
		BackupManifest manifest;
		int id;
		string zipFileName;
		string zipPath;
		string dbFileName;
		string tempCopy;
		int retentionDays;

		using (var scope = scopeFactory.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
			var settings = await ProgramStartupHelpers.GetOrCreateServerSettingsAsync(db);
			backupDir = ResolveBackupDirectory(settings);
			manifest = LoadManifest(backupDir);
			ReconcileManifestWithDisk(backupDir, manifest);

			id = AllocateNextId(manifest);
			var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
			zipFileName = $"tubearr_backup_{stamp}_{id}.zip";
			zipPath = Path.Combine(backupDir, zipFileName);
			retentionDays = settings.BackupRetention;

			if (!SqliteConnectionPaths.TryGetDatabaseFilePath(ConnectionString, _environment.ContentRootPath, out var mainDbPath))
				return (false, "Could not resolve SQLite database path from connection string.");

			dbFileName = Path.GetFileName(mainDbPath);
			tempCopy = Path.Combine(Path.GetTempPath(), $"tubearr-backup-{Guid.NewGuid():N}.db");

			await db.Database.CloseConnectionAsync();
		}

		try
		{
			await Task.Run(() =>
			{
				SqliteConnection.ClearAllPools();

				using var source = new SqliteConnection(ConnectionString);
				source.Open();
				using (var checkpoint = source.CreateCommand())
				{
					checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
					checkpoint.ExecuteNonQuery();
				}

				if (File.Exists(tempCopy))
					File.Delete(tempCopy);

				using (var dest = new SqliteConnection($"Data Source={tempCopy}"))
				{
					dest.Open();
					source.BackupDatabase(dest);
				}

				SqliteConnection.ClearAllPools();

				using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
					zip.CreateEntryFromFile(tempCopy, dbFileName, CompressionLevel.Optimal);
			}, cancellationToken);

			var info = new FileInfo(zipPath);
			manifest.Entries.Add(new BackupManifestEntry
			{
				Id = id,
				FileName = zipFileName,
				CreatedUtc = DateTimeOffset.UtcNow,
				Type = string.IsNullOrWhiteSpace(backupType) ? "manual" : backupType
			});

			ApplyRetention(backupDir, manifest, retentionDays);
			SaveManifest(backupDir, manifest);

			return (true, $"Backup created: {zipFileName} ({info.Length} bytes).");
		}
		catch (Exception ex)
		{
			try
			{
				if (File.Exists(zipPath))
					File.Delete(zipPath);
			}
			catch
			{
				// ignore
			}

			return (false, "Backup failed: " + (ex.Message ?? "Unknown error"));
		}
		finally
		{
			try
			{
				if (File.Exists(tempCopy))
					File.Delete(tempCopy);
			}
			catch
			{
				// ignore
			}
		}
	}

	void ApplyRetention(string backupDir, BackupManifest manifest, int retentionDays)
	{
		if (retentionDays <= 0)
			return;

		var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);
		var toRemove = manifest.Entries.Where(e => e.CreatedUtc < cutoff).ToList();
		foreach (var e in toRemove)
		{
			try
			{
				var fp = Path.Combine(backupDir, e.FileName);
				if (File.Exists(fp))
					File.Delete(fp);
			}
			catch
			{
				// ignore
			}

			manifest.Entries.RemoveAll(x => x.Id == e.Id);
		}
	}

	public async Task<IReadOnlyList<BackupListItemDto>> ListBackupsAsync(TubeArrDbContext db, CancellationToken cancellationToken = default)
	{
		var settings = await ProgramStartupHelpers.GetOrCreateServerSettingsAsync(db);
		var backupDir = ResolveBackupDirectory(settings);
		var manifest = LoadManifest(backupDir);
		ReconcileManifestWithDisk(backupDir, manifest);
		SaveManifest(backupDir, manifest);

		var apiKey = settings.ApiKey ?? "";

		var items = new List<BackupListItemDto>();
		foreach (var e in manifest.Entries.OrderByDescending(x => x.CreatedUtc))
		{
			var fp = Path.Combine(backupDir, e.FileName);
			if (!File.Exists(fp))
				continue;

			var info = new FileInfo(fp);
			var path = $"/api/v1/system/backup/download/{e.Id}?apikey={Uri.EscapeDataString(apiKey)}";
			items.Add(new BackupListItemDto(
				e.Id,
				e.Type,
				e.FileName,
				path,
				info.Length,
				e.CreatedUtc.ToString("O")));
		}

		return items;
	}

	public async Task<string?> TryGetBackupZipPathAsync(TubeArrDbContext db, int id, CancellationToken cancellationToken = default)
	{
		var settings = await ProgramStartupHelpers.GetOrCreateServerSettingsAsync(db);
		var backupDir = ResolveBackupDirectory(settings);
		var manifest = LoadManifest(backupDir);
		ReconcileManifestWithDisk(backupDir, manifest);

		var entry = manifest.Entries.FirstOrDefault(x => x.Id == id);
		if (entry is null)
			return null;

		var fp = Path.Combine(backupDir, entry.FileName);
		return File.Exists(fp) ? fp : null;
	}

	public async Task<(bool Ok, string? Error)> DeleteBackupAsync(TubeArrDbContext db, int id, CancellationToken cancellationToken = default)
	{
		var settings = await ProgramStartupHelpers.GetOrCreateServerSettingsAsync(db);
		var backupDir = ResolveBackupDirectory(settings);
		var manifest = LoadManifest(backupDir);
		ReconcileManifestWithDisk(backupDir, manifest);

		var entry = manifest.Entries.FirstOrDefault(x => x.Id == id);
		if (entry is null)
			return (false, "Backup not found.");

		try
		{
			var fp = Path.Combine(backupDir, entry.FileName);
			if (File.Exists(fp))
				File.Delete(fp);

			manifest.Entries.RemoveAll(x => x.Id == id);
			SaveManifest(backupDir, manifest);
			return (true, null);
		}
		catch (Exception ex)
		{
			return (false, ex.Message ?? "Delete failed.");
		}
	}

	public async Task<(bool Ok, string? Error)> StageRestoreFromBackupIdAsync(TubeArrDbContext db, int id, CancellationToken cancellationToken = default)
	{
		var zipPath = await TryGetBackupZipPathAsync(db, id, cancellationToken);
		if (zipPath is null)
			return (false, "Backup not found.");

		await using var stream = File.OpenRead(zipPath);
		return await StageRestoreFromZipStreamAsync(stream, cancellationToken);
	}

	public async Task<(bool Ok, string? Error)> StageRestoreFromZipStreamAsync(Stream zipStream, CancellationToken cancellationToken = default)
	{
		if (!SqliteConnectionPaths.TryGetDatabaseFilePath(ConnectionString, _environment.ContentRootPath, out var mainDbPath))
			return (false, "Could not resolve SQLite database path.");

		var pendingPath = SqliteConnectionPaths.GetPendingRestorePath(mainDbPath);
		var tempDir = Path.Combine(Path.GetTempPath(), $"tubearr-restore-{Guid.NewGuid():N}");

		try
		{
			Directory.CreateDirectory(tempDir);
			var tempZip = Path.Combine(tempDir, "upload.zip");
			await using (var fs = File.Create(tempZip))
			{
				await zipStream.CopyToAsync(fs, cancellationToken);
			}

			string? extractedDb = null;
			await Task.Run(() =>
			{
				using var archive = ZipFile.OpenRead(tempZip);
				var dbEntries = archive.Entries
					.Where(e => !string.IsNullOrEmpty(e.Name) &&
						e.Name.EndsWith(".db", StringComparison.OrdinalIgnoreCase) &&
						!e.Name.EndsWith("-shm.db", StringComparison.OrdinalIgnoreCase) &&
						!e.Name.EndsWith("-wal.db", StringComparison.OrdinalIgnoreCase))
					.ToList();

				if (dbEntries.Count != 1)
					throw new InvalidOperationException("Backup zip must contain exactly one .db file.");

				var entry = dbEntries[0];
				extractedDb = Path.Combine(tempDir, "extracted.db");
				entry.ExtractToFile(extractedDb, overwrite: true);
			}, cancellationToken);

			if (extractedDb is null || !File.Exists(extractedDb))
				return (false, "Could not extract database from backup.");

			if (!SqliteConnectionPaths.TryValidateSqliteFile(extractedDb))
				return (false, "Extracted file is not a valid SQLite database.");

			SqliteConnection.ClearAllPools();

			if (File.Exists(pendingPath))
				File.Delete(pendingPath);

			File.Copy(extractedDb, pendingPath, overwrite: true);
			return (true, null);
		}
		catch (Exception ex)
		{
			return (false, ex.Message ?? "Restore failed.");
		}
		finally
		{
			try
			{
				if (Directory.Exists(tempDir))
					Directory.Delete(tempDir, recursive: true);
			}
			catch
			{
				// ignore
			}
		}
	}
}

public sealed record BackupListItemDto(
	int Id,
	string Type,
	string Name,
	string Path,
	long Size,
	string Time);
