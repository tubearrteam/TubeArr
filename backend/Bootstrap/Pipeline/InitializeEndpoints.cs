using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

public static class InitializeEndpoints
{
	public static void MapInitializeEndpoints(this WebApplication app)
	{
		app.MapGet("/initialize.json", async (HttpRequest req, TubeArrDbContext db, IConfiguration configuration, ApiSecuritySettingsCache apiSecurity, CancellationToken ct) =>
		{
			var serverSettings = await ProgramStartupHelpers.GetOrCreateServerSettingsAsync(db);
			var snap = await apiSecurity.GetAsync(db, ct);
			var includeKey = ShouldIncludeApiKeyInInitializeResponse(req, snap, serverSettings);
			return Results.Json(CreateInitializeResponse(serverSettings, includeKey, TubeArrFeatureFlagsReader.Read(configuration)));
		});

		app.MapGet("/__URL_BASE__/initialize.json", async (HttpRequest req, TubeArrDbContext db, IConfiguration configuration, ApiSecuritySettingsCache apiSecurity, CancellationToken ct) =>
		{
			var serverSettings = await ProgramStartupHelpers.GetOrCreateServerSettingsAsync(db);
			var snap = await apiSecurity.GetAsync(db, ct);
			var includeKey = ShouldIncludeApiKeyInInitializeResponse(req, snap, serverSettings);
			return Results.Json(CreateInitializeResponse(serverSettings, includeKey, TubeArrFeatureFlagsReader.Read(configuration)));
		});
	}

	internal static bool ShouldIncludeApiKeyInInitializeResponse(HttpRequest req, ApiSecuritySnapshot snap, ServerSettingsEntity serverSettings)
	{
		if (!ApiSecuritySettingsCache.IsApiKeyAuthEnforced(serverSettings))
			return true;
		if (snap.ExpectedKeySha256 is null)
			return false;
		var provided = req.Headers["X-Api-Key"].FirstOrDefault();
		if (string.IsNullOrWhiteSpace(provided))
			provided = req.Query["apikey"].FirstOrDefault();
		if (string.IsNullOrWhiteSpace(provided))
			return false;
		return ApiSecuritySettingsCache.FixedTimeApiKeyEquals(snap.ExpectedKeySha256, provided);
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
			["theme"] = "dark",
			["apiKeyRequired"] = ApiSecuritySettingsCache.IsApiKeyAuthEnforced(serverSettings)
		};
		payload["featureFlags"] = featureFlags ?? new Dictionary<string, bool>();
		if (includeApiKey)
			payload["apiKey"] = serverSettings.ApiKey ?? "";
		return payload;
	}
}
