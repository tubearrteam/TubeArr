using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TubeArr.Backend.Data;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class BackupRestoreTests
{
	[Fact]
	public void ApplyPendingRestoreIfPresent_replaces_main_database_file()
	{
		var dir = Path.Combine(Path.GetTempPath(), "tubearr-restore-" + Guid.NewGuid());
		Directory.CreateDirectory(dir);
		try
		{
			var mainPath = Path.Combine(dir, "app.db");
			WriteMarkerDb(mainPath, 41);

			var replacementPath = Path.Combine(dir, "other.db");
			WriteMarkerDb(replacementPath, 99);

			var pendingPath = SqliteConnectionPaths.GetPendingRestorePath(mainPath);
			File.Copy(replacementPath, pendingPath, overwrite: true);

			SqliteConnection.ClearAllPools();
			SqliteConnectionPaths.ApplyPendingRestoreIfPresent($"Data Source={mainPath}", dir, NullLogger.Instance);

			Assert.False(File.Exists(pendingPath));
			Assert.Equal(99, ReadMarker(mainPath));
		}
		finally
		{
			TryDeleteDir(dir);
		}
	}

	[Fact]
	public async Task StageRestoreFromZipStreamAsync_writes_pending_restore_file()
	{
		var dir = Path.Combine(Path.GetTempPath(), "tubearr-stage-" + Guid.NewGuid());
		Directory.CreateDirectory(dir);
		try
		{
			var mainPath = Path.Combine(dir, "app.db");
			WriteMarkerDb(mainPath, 1);

			var innerDb = Path.Combine(dir, "inner.db");
			WriteMarkerDb(innerDb, 77);
			SqliteConnection.ClearAllPools();

			var zipPath = Path.Combine(dir, "b.zip");
			using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
			{
				zip.CreateEntryFromFile(innerDb, Path.GetFileName(mainPath), CompressionLevel.Optimal);
			}

			var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:TubeArr"] = $"Data Source={mainPath}"
			}).Build();

			var env = new TestWebHostEnvironment(dir);
			var svc = new BackupRestoreService(config, env);

			await using var zipStream = File.OpenRead(zipPath);
			var (ok, err) = await svc.StageRestoreFromZipStreamAsync(zipStream);
			Assert.True(ok, err);
			Assert.Equal(1, ReadMarker(mainPath));

			var pending = SqliteConnectionPaths.GetPendingRestorePath(mainPath);
			Assert.True(File.Exists(pending));
			Assert.Equal(77, ReadMarker(pending));
		}
		finally
		{
			TryDeleteDir(dir);
		}
	}

	[Fact]
	public async Task CreateBackupAsync_writes_zip_with_database_entry()
	{
		var dir = Path.Combine(Path.GetTempPath(), "tubearr-backup-" + Guid.NewGuid());
		Directory.CreateDirectory(dir);
		try
		{
			var mainPath = Path.Combine(dir, "app.db");

			var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:TubeArr"] = $"Data Source={mainPath}"
			}).Build();

			var env = new TestWebHostEnvironment(dir);

			var services = new ServiceCollection();
			services.AddLogging();
			services.AddDbContext<TubeArrDbContext>(o => o.UseSqlite($"Data Source={mainPath}"));
			services.AddSingleton<IConfiguration>(config);
			services.AddSingleton<IWebHostEnvironment>(env);
			services.AddSingleton<BackupRestoreService>();
			using var provider = services.BuildServiceProvider();

			await using (await CreateDbContextAsync(mainPath))
			{
			}

			WriteMarkerDb(mainPath, 55);

			var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
			var svc = provider.GetRequiredService<BackupRestoreService>();
			var (success, message) = await svc.CreateBackupAsync(scopeFactory, "manual", "manual");
			Assert.True(success, message);

			var backupDir = Path.Combine(dir, "Backups");
			Assert.True(Directory.Exists(backupDir));
			var zips = Directory.GetFiles(backupDir, "*.zip");
			Assert.Single(zips);

			using var zip = ZipFile.OpenRead(zips[0]);
			var entry = zip.GetEntry(Path.GetFileName(mainPath));
			Assert.NotNull(entry);
		}
		finally
		{
			TryDeleteDir(dir);
		}
	}

	static async Task<TubeArrDbContext> CreateDbContextAsync(string dbPath)
	{
		var options = new DbContextOptionsBuilder<TubeArrDbContext>()
			.UseSqlite($"Data Source={dbPath}")
			.Options;
		var db = new TubeArrDbContext(options);
		await db.Database.EnsureDeletedAsync();
		await db.Database.EnsureCreatedAsync();
		var settings = await db.ServerSettings.FirstOrDefaultAsync(x => x.Id == 1);
		if (settings is null)
		{
			db.ServerSettings.Add(new ServerSettingsEntity { Id = 1 });
			await db.SaveChangesAsync();
		}

		return db;
	}

	static void WriteMarkerDb(string path, int marker)
	{
		using var conn = new SqliteConnection($"Data Source={path}");
		conn.Open();
		using var cmd = conn.CreateCommand();
		cmd.CommandText = "CREATE TABLE IF NOT EXISTS _m(v INTEGER NOT NULL); DELETE FROM _m; INSERT INTO _m VALUES (@v);";
		cmd.Parameters.AddWithValue("@v", marker);
		cmd.ExecuteNonQuery();
	}

	static int ReadMarker(string path)
	{
		using var conn = new SqliteConnection($"Data Source={path}");
		conn.Open();
		using var cmd = conn.CreateCommand();
		cmd.CommandText = "SELECT v FROM _m LIMIT 1;";
		return Convert.ToInt32(cmd.ExecuteScalar());
	}

	static void TryDeleteDir(string dir)
	{
		try
		{
			if (Directory.Exists(dir))
				Directory.Delete(dir, recursive: true);
		}
		catch
		{
			// ignore
		}
	}

	sealed class TestWebHostEnvironment : IWebHostEnvironment
	{
		public TestWebHostEnvironment(string contentRootPath)
		{
			ContentRootPath = contentRootPath;
			ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
			WebRootPath = contentRootPath;
			WebRootFileProvider = new PhysicalFileProvider(contentRootPath);
		}

		public string ApplicationName { get; set; } = "Test";
		public IFileProvider WebRootFileProvider { get; set; }
		public string WebRootPath { get; set; } = "";
		public string EnvironmentName { get; set; } = "Development";
		public IFileProvider ContentRootFileProvider { get; set; }
		public string ContentRootPath { get; set; } = "";
	}
}
