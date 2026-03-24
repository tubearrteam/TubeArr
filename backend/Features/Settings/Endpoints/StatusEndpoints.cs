using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class SystemStatusEndpoints
{
	internal static void Map(RouteGroupBuilder api, string preloadedUrlBase)
	{
		api.MapGet("/system/status", async (TubeArrDbContext db) =>
		{
			var serverSettings = await ProgramStartupHelpers.GetOrCreateServerSettingsAsync(db);

			return Results.Json(new Dictionary<string, object?>
			{
				["version"] = "0.0.0-dev",
				["buildTime"] = "2026-01-01T00:00:00Z",
				["isDebug"] = true,
				["isProduction"] = false,
				["isAdmin"] = true,
				["appName"] = "TubeArr",
				["instanceName"] = serverSettings.InstanceName ?? "",
				["isWindows"] = OperatingSystem.IsWindows(),
				["mode"] = "console",
				["packageUpdateMechanism"] = "builtIn"
			});
		});

		api.MapGet("/system/routes", () => Results.Json(new Dictionary<string, object?>
		{
			["root"] = string.IsNullOrWhiteSpace(preloadedUrlBase) ? "/" : $"{preloadedUrlBase}/",
			["apiRoot"] = string.IsNullOrWhiteSpace(preloadedUrlBase) ? "/api/v1" : $"{preloadedUrlBase}/api/v1"
		}));

		api.MapGet("/system/task", () =>
		{
			var tasks = ScheduledTaskCatalog.GetScheduledTaskDtos();
			return Results.Json(tasks);
		});

		api.MapGet("/system/task/{id:int}", (int id) =>
		{
			var tasks = ScheduledTaskCatalog.GetScheduledTaskDtos();
			var task = tasks.FirstOrDefault(t => t.Id == id);
			return task is null ? Results.NotFound() : Results.Json(task);
		});
	}
}
