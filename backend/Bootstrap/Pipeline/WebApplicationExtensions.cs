using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using TubeArr.Backend.Data;
using TubeArr.Backend.Plex;
using TubeArr.Backend.Realtime;

namespace TubeArr.Backend;

public static class WebApplicationExtensions
{
	public static void MapTubeArrHubs(this WebApplication app)
	{
		app.MapHub<MessagesHub>("/signalr/messages");
		app.MapHub<MessagesHub>("/api/v1/signalr/messages");
	}

	/// <summary>
	/// Serves the SPA from <c>_output/UI</c>. Static files and the HTML fallback must not run for <c>/tv</c> (Plex Custom Metadata Provider JSON)
	/// or <c>/api</c> — otherwise a path collision or the SPA <c>index.html</c> is returned as 200 HTML and Plex fails with JSON parse errors at 1:1.
	/// </summary>
	public static void ServeTubeArrUi(this WebApplication app, string contentRootPath)
	{
		// Serve built UI in release: frontend output is at repo/_output/UI (relative to backend ContentRoot)
		var uiPath = Path.GetFullPath(Path.Combine(contentRootPath, "..", "_output", "UI"));
		if (!Directory.Exists(uiPath))
			return;

		var fileProvider = new PhysicalFileProvider(uiPath);

		app.UseWhen(
			ctx => !IsReservedNonUiPath(ctx.Request.Path),
			branch =>
			{
				branch.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider, RequestPath = "" });
			});

		app.MapFallback(async context =>
		{
			if (IsReservedNonUiPath(context.Request.Path))
			{
				// Unmatched /tv/* should never return SPA HTML (Plex expects JSON or HTTP error without HTML body).
				if (context.Request.Path.StartsWithSegments("/tv", StringComparison.OrdinalIgnoreCase))
				{
					context.Response.StatusCode = StatusCodes.Status404NotFound;
					context.Response.ContentType = "application/json; charset=utf-8";
					await context.Response.WriteAsJsonAsync(
						new
						{
							MediaContainer = new
							{
								offset = 0,
								totalSize = 0,
								identifier = PlexConstants.ProviderIdentifier,
								size = 0,
								Metadata = Array.Empty<object>()
							}
						},
						new JsonSerializerOptions { PropertyNamingPolicy = null });
					return;
				}

				context.Response.StatusCode = StatusCodes.Status404NotFound;
				return;
			}

			context.Response.ContentType = "text/html";
			var html = await RenderBootstrappedIndexAsync(context, uiPath);
			await context.Response.WriteAsync(html);
		});
	}

	static async Task<string> RenderBootstrappedIndexAsync(HttpContext context, string uiPath)
	{
		var indexPath = Path.Combine(uiPath, "index.html");
		var html = await File.ReadAllTextAsync(indexPath);
		var db = context.RequestServices.GetRequiredService<TubeArrDbContext>();
		var serverSettings = await ProgramStartupHelpers.GetOrCreateServerSettingsAsync(db);
		var bootstrapJson = JsonSerializer.Serialize(InitializeEndpoints.CreateInitializeResponse(serverSettings, includeApiKey: true));
		var bootstrapScript = $"<script>window.TubeArr={bootstrapJson};</script>";
		var marker = "</head>";
		var markerIndex = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
		if (markerIndex >= 0)
			return html.Insert(markerIndex, bootstrapScript);
		return bootstrapScript + html;
	}

	static bool IsReservedNonUiPath(PathString path)
	{
		if (!path.HasValue)
			return false;
		return path.StartsWithSegments("/tv", StringComparison.OrdinalIgnoreCase)
			|| path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
			|| path.StartsWithSegments("/signalr", StringComparison.OrdinalIgnoreCase)
			|| path.StartsWithSegments("/initialize", StringComparison.OrdinalIgnoreCase)
			|| path.StartsWithSegments("/__URL_BASE__", StringComparison.OrdinalIgnoreCase);
	}
}

