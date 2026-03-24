using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;
using System.IO;
using TubeArr.Backend.Realtime;

namespace TubeArr.Backend;

public static class WebApplicationExtensions
{
	public static void MapTubeArrHubs(this WebApplication app)
	{
		app.MapHub<MessagesHub>("/signalr/messages");
		app.MapHub<MessagesHub>("/api/v1/signalr/messages");
	}

	public static void ServeTubeArrUi(this WebApplication app, string contentRootPath)
	{
		// Serve built UI in release: frontend output is at repo/_output/UI (relative to backend ContentRoot)
		var uiPath = Path.GetFullPath(Path.Combine(contentRootPath, "..", "_output", "UI"));
		if (!Directory.Exists(uiPath))
			return;

		var fileProvider = new PhysicalFileProvider(uiPath);
		app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
		app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider, RequestPath = "" });

		app.MapFallback(async context =>
		{
			context.Response.ContentType = "text/html";
			await context.Response.SendFileAsync(Path.Combine(uiPath, "index.html"));
		});
	}
}

