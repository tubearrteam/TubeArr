using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;
using TubeArr.Backend.Realtime;

namespace TubeArr.Backend.Tests;

public sealed class ChannelIngestionOrchestratorTests
{
	const string YoutubeChannelId = "UC1234567890123456789012";

	[Fact]
	public async Task CreateOrUpdateAsync_queues_refresh_channel_for_new_channel()
	{
		using var connection = new SqliteConnection("Data Source=:memory:");
		await connection.OpenAsync();

		var services = new ServiceCollection();
		services.AddLogging();
		services.AddDbContext<TubeArrDbContext>(options => options.UseSqlite(connection));
		services.AddSingleton<IConfiguration>(_ => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build());
		services.AddSingleton<IWebHostEnvironment>(_ => new ChannelIngestionTestWebHostEnvironment(Path.GetTempPath()));
		services.AddSingleton<BackupRestoreService>();
		services.AddSingleton<InMemoryCommandState>();
		services.AddSingleton<CommandRecordFactory>();
		services.AddSingleton<ICommandExecutionQueue, InProcessCommandExecutionQueue>();
		services.AddSingleton<IScheduledTaskRunRecorder, ScheduledTaskRunRecorder>();
		services.AddSingleton<CommandDispatcher>();
		services.AddSingleton<IRealtimeEventBroadcaster, TestRealtimeEventBroadcaster>();

		using var provider = services.BuildServiceProvider();
		using (var setupScope = provider.CreateScope())
		{
			var setupDb = setupScope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
			await setupDb.Database.EnsureCreatedAsync();

			var orchestrator = new ChannelIngestionOrchestrator(
				new ChannelPageMetadataService(new NotFoundHttpClientFactory(), NullLogger<ChannelPageMetadataService>.Instance),
				new TestYtDlpClient(),
				setupScope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
				setupScope.ServiceProvider.GetRequiredService<CommandDispatcher>(),
				setupScope.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>(),
				NullLogger<ChannelIngestionOrchestrator>.Instance);

			var request = new CreateChannelRequest(
				YoutubeChannelId,
				"Queued Channel",
				"Queue metadata for this channel",
				Monitored: true,
				QualityProfileId: 7,
				RootFolderPath: @"C:\Media",
				ChannelType: "standard",
				PlaylistFolder: true,
				Path: @"C:\Media\Queued Channel",
				Tags: [11, 12],
				MonitorNewItems: 1,
				RoundRobinLatestVideoCount: 5);

			var (channel, wasNew, errorMessage) = await orchestrator.CreateOrUpdateAsync(request, setupDb);

			Assert.Null(errorMessage);
			Assert.True(wasNew);
			Assert.NotNull(channel);
		}

		using var verificationScope = provider.CreateScope();
		var verificationDb = verificationScope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
		var channelEntity = await verificationDb.Channels.SingleAsync();
		var queuedJob = await verificationDb.CommandQueueJobs.SingleAsync();

		Assert.Equal(CommandQueueJobTypes.RefreshChannel, queuedJob.JobType);
		Assert.Equal("RefreshChannelUploadsPopulation", queuedJob.Name);
		Assert.Equal("queued", queuedJob.Status);
		Assert.NotNull(queuedJob.CommandId);
		Assert.Empty(await verificationDb.Videos.ToListAsync());

		var records = verificationScope.ServiceProvider.GetRequiredService<CommandRecordFactory>();
		Assert.True(records.TryGetCommandById(queuedJob.CommandId!.Value, out var command));

		Assert.Equal("RefreshChannelUploadsPopulation", Assert.IsType<string>(command["name"]));
		Assert.Equal("queued", Assert.IsType<string>(command["status"]));
		Assert.Equal("auto", Assert.IsType<string>(command["trigger"]));

		var body = Assert.IsType<Dictionary<string, object?>>(command["body"]);
		Assert.Equal(channelEntity.Id, Assert.IsType<int>(body["channelId"]));
		var channelIds = Assert.IsType<int[]>(body["channelIds"]);
		Assert.Single(channelIds);
		Assert.Equal(channelEntity.Id, channelIds[0]);
	}

	sealed class TestRealtimeEventBroadcaster : IRealtimeEventBroadcaster
	{
		public Task BroadcastAsync(string name, object body, CancellationToken ct = default)
		{
			return Task.CompletedTask;
		}
	}

	sealed class TestYtDlpClient : IYtDlpClient
	{
		public Task<string?> GetExecutablePathAsync(TubeArrDbContext db, CancellationToken ct)
		{
			return Task.FromResult<string?>(null);
		}

		public Task<IReadOnlyList<YtDlpChannelResultMapper.ChannelResultMap>> SearchChannelsAsync(string executablePath, string term, int maxResults, CancellationToken ct)
		{
			throw new NotSupportedException();
		}

		public Task<(IReadOnlyList<YtDlpChannelResultMapper.ChannelResultMap> Results, string? ResolutionMethod)> ResolveExactChannelAsync(string executablePath, string input, CancellationToken ct, int timeoutMs, ILogger logger)
		{
			throw new NotSupportedException();
		}

		public Task<(string? Title, string? Description, string? ThumbnailUrl, string? ChannelUrl, string? Handle)?> EnrichChannelForCreateAsync(string executablePath, string youtubeChannelId, CancellationToken ct)
		{
			throw new NotSupportedException();
		}
	}

	sealed class NotFoundHttpClientFactory : IHttpClientFactory
	{
		public HttpClient CreateClient(string name)
		{
			return new HttpClient(new NotFoundHttpMessageHandler())
			{
				BaseAddress = new Uri("https://www.youtube.com/")
			};
		}
	}

	sealed class NotFoundHttpMessageHandler : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
		}
	}

	sealed class ChannelIngestionTestWebHostEnvironment : IWebHostEnvironment
	{
		public ChannelIngestionTestWebHostEnvironment(string contentRootPath)
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