using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

public static class InitializeEndpoints
{
	public static void MapInitializeEndpoints(this WebApplication app)
	{
		app.MapGet("/initialize.json", async (TubeArrDbContext db, IConfiguration configuration) =>
		{
			var serverSettings = await ProgramStartupHelpers.GetOrCreateServerSettingsAsync(db);
			return Results.Json(CreateInitializeResponse(serverSettings, includeApiKey: false, TubeArrFeatureFlagsReader.Read(configuration)));
		});

		app.MapGet("/__URL_BASE__/initialize.json", async (TubeArrDbContext db, IConfiguration configuration) =>
		{
			var serverSettings = await ProgramStartupHelpers.GetOrCreateServerSettingsAsync(db);
			return Results.Json(CreateInitializeResponse(serverSettings, includeApiKey: false, TubeArrFeatureFlagsReader.Read(configuration)));
		});
	}

	internal static IReadOnlyDictionary<string, object?> CreateInitializeResponse(
		ServerSettingsEntity serverSettings,
		bool includeApiKey,
		IReadOnlyDictionary<string, bool>? featureFlags = null)
	{
		var urlBase = ProgramStartupHelpers.NormalizeUrlBase(serverSettings.UrlBase);
		var apiRoot = string.IsNullOrWhiteSpace(urlBase) ? "/api/v1" : $"{urlBase}/api/v1";

		var payload = new Dictionary<string, object?>
		{
			["urlBase"] = urlBase,
			["apiRoot"] = apiRoot,
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
		if (featureFlags is { Count: > 0 })
			payload["featureFlags"] = featureFlags;
		if (includeApiKey)
			payload["apiKey"] = serverSettings.ApiKey ?? "";
		return payload;
	}
}
