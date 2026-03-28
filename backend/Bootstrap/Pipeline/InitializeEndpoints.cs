using Microsoft.AspNetCore.Builder;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

public static class InitializeEndpoints
{
	public static void MapInitializeEndpoints(this WebApplication app)
	{
		app.MapGet("/initialize.json", async (TubeArrDbContext db) =>
		{
			var serverSettings = await ProgramStartupHelpers.GetOrCreateServerSettingsAsync(db);
			return Results.Json(CreateInitializeResponse(serverSettings));
		});

		app.MapGet("/__URL_BASE__/initialize.json", async (TubeArrDbContext db) =>
		{
			var serverSettings = await ProgramStartupHelpers.GetOrCreateServerSettingsAsync(db);
			return Results.Json(CreateInitializeResponse(serverSettings));
		});
	}

	static IReadOnlyDictionary<string, object?> CreateInitializeResponse(ServerSettingsEntity serverSettings)
	{
		var urlBase = ProgramStartupHelpers.NormalizeUrlBase(serverSettings.UrlBase);
		var apiRoot = string.IsNullOrWhiteSpace(urlBase) ? "/api/v1" : $"{urlBase}/api/v1";

		return new Dictionary<string, object?>
		{
			["urlBase"] = urlBase,
			["apiRoot"] = apiRoot,
			["apiKey"] = serverSettings.ApiKey ?? "",
			["version"] = ApplicationVersion.GetDisplayVersion(),
			["buildTime"] = "2026-01-01T00:00:00Z",
			["isDebug"] = true,
			["isProduction"] = false,
			["isAdmin"] = true,
			["appName"] = "TubeArr",
			["instanceName"] = serverSettings.InstanceName ?? "",
			["analytics"] = serverSettings.AnalyticsEnabled,
			["theme"] = "dark"
		};
	}
}
