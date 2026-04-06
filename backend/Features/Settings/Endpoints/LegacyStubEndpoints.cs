using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace TubeArr.Backend;

/// <summary>
/// Stub endpoints for Sonarr-derived frontend components that still fetch these
/// resources on boot. Returns empty arrays to prevent 404 errors.
/// </summary>
internal static class LegacyStubEndpoints
{
	internal static void Map(RouteGroupBuilder api)
	{
		api.MapGet("/indexer", () => Results.Json(Array.Empty<object>()));
		api.MapGet("/indexer/schema", () => Results.Json(Array.Empty<object>()));
		api.MapGet("/downloadClient", () => Results.Json(Array.Empty<object>()));
		api.MapGet("/downloadClient/schema", () => Results.Json(Array.Empty<object>()));
		api.MapGet("/importList", () => Results.Json(Array.Empty<object>()));
		api.MapGet("/importList/schema", () => Results.Json(Array.Empty<object>()));
		api.MapGet("/customFormat", () => Results.Json(Array.Empty<object>()));
		api.MapGet("/customFormat/schema", () => Results.Json(Array.Empty<object>()));
		api.MapGet("/releaseProfile", () => Results.Json(Array.Empty<object>()));
		api.MapGet("/marketplace", () => Results.Json(Array.Empty<object>()));
	}
}
