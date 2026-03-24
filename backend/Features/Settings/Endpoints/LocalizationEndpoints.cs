using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace TubeArr.Backend;

internal static class LocalizationEndpoints
{
	internal static void Map(RouteGroupBuilder api, Lazy<IReadOnlyDictionary<string, string>> englishStringsLazy)
	{
		api.MapGet("/localization", () => Results.Json(new Dictionary<string, object?>
		{
			["strings"] = englishStringsLazy.Value
		}));

		api.MapGet("/localization/language", () => Results.Json(new Dictionary<string, object?>
		{
			["identifier"] = "en"
		}));
	}
}
