using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

public static partial class SystemMiscEndpoints
{
	private static readonly Lazy<string> _notificationSchemaJson = new(BuildNotificationSchemaJson);

	static partial void MapLocalizationAndNotificationEndpoints(RouteGroupBuilder api)
	{
		api.MapGet("/localization", async (IWebHostEnvironment env, TubeArrDbContext db, CancellationToken ct) =>
		{
			var ui = await db.UiConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
			var langId = ui?.UiLanguage ?? 0;
			var strings = ProgramStartupHelpers.BuildMergedUiStrings(env.ContentRootPath, langId);
			return Results.Json(new Dictionary<string, object?> { ["strings"] = strings });
		});

		api.MapGet("/localization/language", async (IWebHostEnvironment env, TubeArrDbContext db, CancellationToken ct) =>
		{
			var ui = await db.UiConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
			var langId = ui?.UiLanguage ?? 0;
			var languages = ProgramStartupHelpers.LoadAvailableLanguages(env.ContentRootPath);
			var lang = languages.FirstOrDefault(l => l.Id == langId && l.Enabled);
			var code = lang?.Code ?? "en";
			return Results.Json(new Dictionary<string, object?> { ["identifier"] = code });
		});

		api.MapGet("/autoTagging", () => Results.Json(Array.Empty<object>()));
	}
}
