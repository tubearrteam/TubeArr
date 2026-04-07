using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Diagnostics;
using System.Text.Json;
using TubeArr.Backend;
using TubeArr.Backend.Data;
using TubeArr.Backend.QualityProfile;
using TubeArr.Backend.Realtime;

var builder = WebApplication.CreateBuilder(args);
TubeArrAppPaths.ContentRoot = builder.Environment.ContentRootPath;

var logDir = Path.Combine(builder.Environment.ContentRootPath, "logs");
Directory.CreateDirectory(logDir);
var logFilePath = Path.Combine(logDir, "tubearr-.log");
builder.Host.UseSerilog((context, services, configuration) =>
{
	configuration
		.ReadFrom.Configuration(context.Configuration)
		.WriteTo.Console()
		.WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14, shared: true);
});
var startupSw = Stopwatch.StartNew();

using var bootstrapLoggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
var startupLogger = bootstrapLoggerFactory.CreateLogger("Startup");

startupLogger.LogInformation("Preload started.");
var preloadSw = Stopwatch.StartNew();
var preload = builder.ConfigureTubeArrPreload();
startupLogger.LogInformation("Preload completed in {ElapsedMs} ms.", preloadSw.ElapsedMilliseconds);

startupLogger.LogInformation("Service registration started.");
var servicesSw = Stopwatch.StartNew();
builder.Services.AddTubeArrServices(preload.ConnectionString);
startupLogger.LogInformation("Service registration completed in {ElapsedMs} ms.", servicesSw.ElapsedMilliseconds);
var preloadedUrlBase = preload.PreloadedUrlBase;

startupLogger.LogInformation("Host build started.");
var buildSw = Stopwatch.StartNew();
var app = builder.Build();
app.Logger.LogInformation("Host build completed in {ElapsedMs} ms.", buildSw.ElapsedMilliseconds);
var plexHttpProbeLogging = app.Configuration.GetValue("Plex:HttpProbeLogging", false);
if (plexHttpProbeLogging)
{
	try
	{
		var probe = Path.Combine(logDir, "plex-http.log");
		File.AppendAllText(probe, $"{DateTimeOffset.Now:O} Host started contentRoot={app.Environment.ContentRootPath} logFile={logFilePath}\n");
	}
	catch
	{
		// ignore probe failures
	}
}

if (plexHttpProbeLogging)
{
	app.Use(async (context, next) =>
	{
		var p = context.Request.Path.Value ?? "";
		if (p.Contains("/tv", StringComparison.OrdinalIgnoreCase))
		{
			try
			{
				var probe = Path.Combine(logDir, "plex-http.log");
				await File.AppendAllTextAsync(probe,
					$"{DateTimeOffset.Now:O} {context.Request.Method} {p}{context.Request.QueryString}\n");
			}
			catch
			{
				// ignore
			}
		}

		await next();
	});
}

app.UseWebSockets();
app.InitializeDatabaseWithLogging();
app.UseTubeArrApiSecurity();

var englishStringsLazy = new Lazy<IReadOnlyDictionary<string, string>>(() => ProgramStartupHelpers.LoadEnglishStrings(app.Environment.ContentRootPath));

app.MapInitializeEndpoints();

app.MapTubeArrApiEndpoints(
	preloadedUrlBase,
	englishStringsLazy);

TubeArr.Backend.Plex.PlexEndpoints.Map(app);



app.MapTubeArrHubs();
app.ServeTubeArrUi(builder.Environment.ContentRootPath);

app.Logger.LogInformation("Startup configuration complete in {ElapsedMs} ms. Beginning request processing.", startupSw.ElapsedMilliseconds);

try
{
	app.Run();
}
finally
{
	Log.CloseAndFlush();
}
