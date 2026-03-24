using Microsoft.AspNetCore.Builder;

namespace TubeArr.Backend;

public static partial class SystemMiscEndpoints
{
	private static readonly Lazy<string> _notificationSchemaJson = new(BuildNotificationSchemaJson);

	static partial void MapLocalizationAndNotificationEndpoints(
		RouteGroupBuilder api,
		Lazy<IReadOnlyDictionary<string, string>> englishStringsLazy)
	{
		api.MapGet("/localization", () => Results.Json(new Dictionary<string, object?>
		{
			["strings"] = englishStringsLazy.Value
		}));

		api.MapGet("/localization/language", () => Results.Json(new Dictionary<string, object?>
		{
			["identifier"] = "en"
		}));

		api.MapGet("/notification", () => Results.Json(Array.Empty<object>()));

		api.MapGet("/notification/schema", () => Results.Content(_notificationSchemaJson.Value, "application/json"));

		api.MapGet("/autoTagging", () => Results.Json(Array.Empty<object>()));
	}
}
