using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TubeArr.Backend.Data;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class DownloadQueueAndStubIntegrityTests
{
	[Fact]
	public void AddTubeArrServices_registers_DownloadQueueProcessorHostedService()
	{
		var services = new ServiceCollection();
		var dbPath = Path.Combine(Path.GetTempPath(), "TubeArrTests", $"reg-{Guid.NewGuid():N}.sqlite");
		Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
		try
		{
			services.AddTubeArrServices($"Data Source={dbPath}");
			Assert.Contains(
				services,
				d => d.ServiceType == typeof(IHostedService) &&
				     d.ImplementationType == typeof(DownloadQueueProcessorHostedService));
		}
		finally
		{
			try
			{
				if (File.Exists(dbPath))
					File.Delete(dbPath);
			}
			catch
			{
				// best-effort
			}
		}
	}

	[Fact]
	public async Task QualityProfile_route_returns_OK_and_JSON_array()
	{
		var dbPath = CreateTempDbPath();
		try
		{
			var builder = WebApplication.CreateBuilder(new string[0]);
			builder.WebHost.UseTestServer();
			builder.Services.AddTubeArrServices($"Data Source={dbPath}");

			await using var app = builder.Build();
			app.InitializeDatabaseWithLogging();
			app.MapInitializeEndpoints();
			MapApiEndpointsViaReflection(app);

			await app.StartAsync();
			var client = app.GetTestClient();

			var response = await client.GetAsync("/api/v1/qualityprofile");
			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var body = await response.Content.ReadAsStringAsync();
			Assert.StartsWith("[", body.TrimStart());
		}
		finally
		{
			TryDelete(dbPath);
		}
	}

	[Fact]
	public async Task BulkQueueDelete_removes_terminal_rows_and_keeps_running_rows()
	{
		var dbPath = CreateTempDbPath();
		try
		{
			var builder = WebApplication.CreateBuilder(new string[0]);
			builder.WebHost.UseTestServer();
			builder.Services.AddTubeArrServices($"Data Source={dbPath}");

			await using var app = builder.Build();
			app.InitializeDatabaseWithLogging();
			app.MapInitializeEndpoints();
			MapApiEndpointsViaReflection(app);

			using (var scope = app.Services.CreateScope())
			{
				var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
				db.Channels.Add(new ChannelEntity
				{
					Title = "Channel",
					YoutubeChannelId = "chan-1",
					Monitored = true
				});
				db.SaveChanges();
				db.Videos.AddRange(
					new VideoEntity { ChannelId = 1, YoutubeVideoId = "vid-1", Title = "Video 1" },
					new VideoEntity { ChannelId = 1, YoutubeVideoId = "vid-2", Title = "Video 2" },
					new VideoEntity { ChannelId = 1, YoutubeVideoId = "vid-3", Title = "Video 3" });
				db.SaveChanges();
				db.DownloadQueue.AddRange(
					new DownloadQueueEntity { ChannelId = 1, VideoId = 1, Status = QueueJobStatuses.Queued },
					new DownloadQueueEntity { ChannelId = 1, VideoId = 2, Status = QueueJobStatuses.Running },
					new DownloadQueueEntity { ChannelId = 1, VideoId = 3, Status = QueueJobStatuses.Failed });
				db.SaveChanges();
			}

			await app.StartAsync();
			var client = app.GetTestClient();

			var response = await client.DeleteAsync("/api/v1/queue");
			Assert.Equal(HttpStatusCode.OK, response.StatusCode);

			using var verifyScope = app.Services.CreateScope();
			var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
			var remainingStatuses = await verifyDb.DownloadQueue.AsNoTracking().Select(x => x.Status).ToListAsync();
			Assert.Single(remainingStatuses);
			Assert.Equal(QueueJobStatuses.Running, remainingStatuses[0]);
		}
		finally
		{
			TryDelete(dbPath);
		}
	}

	static void MapApiEndpointsViaReflection(WebApplication app)
	{
		var englishStringsLazy = new System.Lazy<System.Collections.Generic.IReadOnlyDictionary<string, string>>(
			() => new System.Collections.Generic.Dictionary<string, string>());

		var composerType = typeof(InitializeEndpoints).Assembly.GetType("TubeArr.Backend.ApiEndpointComposer");
		Assert.NotNull(composerType);

		var mapMethod = composerType!.GetMethod("MapTubeArrApiEndpoints", BindingFlags.Static | BindingFlags.NonPublic);
		Assert.NotNull(mapMethod);

		mapMethod!.Invoke(null, [app, "", englishStringsLazy]);
	}

	static string CreateTempDbPath()
	{
		var root = Path.Combine(Path.GetTempPath(), "TubeArrTests");
		Directory.CreateDirectory(root);
		return Path.Combine(root, $"qp-smoke-{Guid.NewGuid():N}.sqlite");
	}

	static void TryDelete(string path)
	{
		try
		{
			if (File.Exists(path))
				File.Delete(path);
		}
		catch
		{
			// best-effort
		}
	}
}
