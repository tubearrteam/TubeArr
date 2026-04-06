using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TubeArr.Backend.Data;
using TubeArr.Backend.Serialization;
using TubeArr.Backend.DownloadBackends;
using TubeArr.Backend.QualityProfile;
using TubeArr.Backend.Realtime;
using TubeArr.Shared.Infrastructure;

namespace TubeArr.Backend;

public static class ServiceCollectionExtensions
{
	public static void AddTubeArrServices(this IServiceCollection services, string connectionString)
	{
		connectionString = SqliteConnectionPaths.NormalizeConnectionStringForConcurrency(connectionString);
		services.AddDbContext<TubeArrDbContext>(options =>
		{
			options.UseSqlite(connectionString);
		});
		services.AddHostedService<DeferredDatabaseMigrationHostedService>();

		services.AddSignalR().AddJsonProtocol(o => TubeArrJsonSerializer.ApplyApiDefaults(o.PayloadSerializerOptions));
		services.AddSingleton<IRealtimeEventBroadcaster, SignalRRealtimeEventBroadcaster>();
		services.AddTransient<IYtDlpClient, YtDlpClient>();
		services.AddTransient<IBrowserCookieService, BrowserCookieService>();

		services.AddSingleton<YtDlpDownloadBackend>();
		services.AddSingleton(sp => new DownloadBackendRouter(new IDownloadBackend[]
		{
			sp.GetRequiredService<YtDlpDownloadBackend>()
		}));

		services.AddSingleton<InMemoryCommandState>();
		services.AddSingleton<CommandRecordFactory>();
		services.AddSingleton<ICommandExecutionQueue, InProcessCommandExecutionQueue>();
		services.AddSingleton<ICommandRecoveryJobRunner, CommandRecoveryJobRunner>();
		services.AddSingleton<IScheduledTaskRunRecorder, ScheduledTaskRunRecorder>();
		services.AddSingleton<CommandDispatcher>();
		services.AddSingleton<BackupRestoreService>();
		services.AddHostedService<CommandExecutionQueueHostedService>();
		services.AddHostedService<ScheduledTasksHostedService>();
		services.AddSingleton<TubeArrDbPersistQueue>();
		services.AddHostedService<TubeArrDbPersistQueueHostedService>();
		services.AddSingleton<DownloadQueueProcessTrigger>();
		services.AddHostedService<DownloadQueueProcessorHostedService>();

		services.AddHttpClient();

		services.AddHttpClient("YouTubeDataApi", client =>
		{
			client.BaseAddress = new Uri("https://www.googleapis.com/youtube/v3/");
		});

		services.AddHttpClient("YouTubePage", client =>
		{
			client.BaseAddress = new Uri("https://www.youtube.com/");
			client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; rv:109.0) Gecko/20100101 Firefox/115.0");
		});

		services.AddTransient<ChannelPageMetadataService>();
		services.AddTransient<ChannelSearchHtmlResolveService>();
		services.AddTransient<ChannelVideoDiscoveryService>();
		services.AddTransient<VideoWatchPageMetadataService>();
		services.AddTransient<YouTubeDataApiMetadataService>();
		services.AddTransient<ChannelMetadataAcquisitionService>();
		services.AddTransient<ChannelPlaylistDiscoveryService>();
		services.AddTransient<ChannelIngestionOrchestrator>();
		services.AddTransient<ChannelRssSyncService>();
		services.AddTransient<ChannelResolveService>();
		services.AddTransient<LibraryImportScanService>();

		services.AddHttpClient("GitHub", client =>
		{
			client.BaseAddress = new Uri("https://api.github.com/");
			client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "TubeArr");
			client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/vnd.github.v3+json");
		});

		services.ConfigureHttpJsonOptions(options => TubeArrJsonSerializer.ApplyApiDefaults(options.SerializerOptions));
	}
}

