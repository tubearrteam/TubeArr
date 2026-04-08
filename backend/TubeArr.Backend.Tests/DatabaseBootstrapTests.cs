using System;
using System.IO;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using TubeArr.Backend.Data;

namespace TubeArr.Backend.Tests;

public sealed class DatabaseBootstrapTests
{
	[Fact]
	public void EnsureDatabaseInitialized_first_run_creates_schema_from_migrations()
	{
		var dbPath = CreateTempDbPath();
		try
		{
			using var services = CreateServices(dbPath);
			EnsureDatabaseInitialized(services);

			using var scope = services.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();

			Assert.True(TableExists(db, "Channels"));
			Assert.True(TableExists(db, "Videos"));
			Assert.True(TableExists(db, "QualityProfiles"));
			Assert.True(TableExists(db, "__EFMigrationsHistory"));
		}
		finally
		{
			TryDelete(dbPath);
		}
	}

	[Fact]
	public void EnsureDatabaseInitialized_is_idempotent_on_current_schema()
	{
		var dbPath = CreateTempDbPath();
		try
		{
			using var services = CreateServices(dbPath);
			EnsureDatabaseInitialized(services);
			EnsureDatabaseInitialized(services);

			using var scope = services.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
			Assert.True(TableExists(db, "Channels"));
			Assert.True(TableExists(db, "Videos"));
			Assert.True(TableExists(db, "__EFMigrationsHistory"));
		}
		finally
		{
			TryDelete(dbPath);
		}
	}

	private static ServiceProvider CreateServices(string dbPath)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
		services.AddDbContext<TubeArrDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
		return services.BuildServiceProvider();
	}

	private static void EnsureDatabaseInitialized(IServiceProvider services)
	{
		var bootstrapType = typeof(DatabaseBootstrapRunner).Assembly.GetType("TubeArr.Backend.DatabaseBootstrap");
		Assert.NotNull(bootstrapType);

		var method = bootstrapType!.GetMethod("EnsureDatabaseInitialized", BindingFlags.Public | BindingFlags.Static);
		Assert.NotNull(method);

		try
		{
			method!.Invoke(null, new object[] { services });
		}
		catch (TargetInvocationException ex) when (ex.InnerException is not null)
		{
			throw ex.InnerException;
		}
	}

	private static bool TableExists(TubeArrDbContext db, string tableName)
	{
		db.Database.OpenConnection();
		try
		{
			using var cmd = db.Database.GetDbConnection().CreateCommand();
			cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";

			var parameter = cmd.CreateParameter();
			parameter.ParameterName = "$name";
			parameter.Value = tableName;
			cmd.Parameters.Add(parameter);

			var value = cmd.ExecuteScalar();
			return value is not null && value != DBNull.Value;
		}
		finally
		{
			db.Database.CloseConnection();
		}
	}

	private static string CreateTempDbPath()
	{
		var root = Path.Combine(Path.GetTempPath(), "TubeArrTests");
		Directory.CreateDirectory(root);
		return Path.Combine(root, $"db-bootstrap-{Guid.NewGuid():N}.sqlite");
	}

	private static void TryDelete(string path)
	{
		try
		{
			if (File.Exists(path))
				File.Delete(path);
		}
		catch
		{
			// Best-effort cleanup for test temp files.
		}
	}
}
