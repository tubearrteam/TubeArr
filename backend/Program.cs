using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using TubeArr.Backend;
using TubeArr.Backend.Data;
using TubeArr.Backend.QualityProfile;
using TubeArr.Backend.Realtime;

var builder = WebApplication.CreateBuilder(args);
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
app.InitializeDatabaseWithLogging();

var englishStringsLazy = new Lazy<IReadOnlyDictionary<string, string>>(() => ProgramStartupHelpers.LoadEnglishStrings(app.Environment.ContentRootPath));

app.MapInitializeEndpoints();

app.MapTubeArrApiEndpoints(
	preloadedUrlBase,
	englishStringsLazy);



app.MapTubeArrHubs();
app.ServeTubeArrUi(builder.Environment.ContentRootPath);

app.Logger.LogInformation("Startup configuration complete in {ElapsedMs} ms. Beginning request processing.", startupSw.ElapsedMilliseconds);

app.Run();
