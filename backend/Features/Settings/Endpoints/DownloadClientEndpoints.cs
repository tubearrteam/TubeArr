using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace TubeArr.Backend;

internal static class DownloadClientEndpoints
{
	internal static void Map(RouteGroupBuilder api)
	{
		api.MapGet("/downloadClient", () => Results.Json(Array.Empty<object>()));

		api.MapPost("/downloadClient", () =>
		{
			return Results.Created("/downloadClient/1", new { id = 1, name = "New Download Client" });
		});

		api.MapGet("/downloadClient/{id:int}", (int id) =>
		{
			var downloadClient = new
			{
				id = id,
				name = $"Download Client {id}",
				implementation = "UsenetDownloadClient",
				enable = true,
				settings = new { }
			};
			return Results.Json(downloadClient);
		});

		api.MapPut("/downloadClient/{id:int}", () => Results.NoContent());

		api.MapDelete("/downloadClient/{id:int}", () => Results.NoContent());

		api.MapGet("/downloadClient/schema", () =>
		{
			var downloadClientDefinitions = new object[]
			{
				new
				{
					implementation = "UsenetDownloadClient",
					implementationName = "Usenet",
					infoLink = "https://wiki.servarr.com/tubearr/settings#download-clients",
					fields = new object[]
					{
						new
						{
							name = "host",
							label = "Host",
							type = "textbox",
							value = ""
						},
						new
						{
							name = "port",
							label = "Port",
							type = "number",
							value = 6789
						}
					}
				},
				new
				{
					implementation = "TorrentDownloadClient",
					implementationName = "Torrent",
					infoLink = "https://wiki.servarr.com/tubearr/settings#download-clients",
					fields = new object[]
					{
						new
						{
							name = "host",
							label = "Host",
							type = "textbox",
							value = ""
						}
					}
				}
			};
			return Results.Json(downloadClientDefinitions);
		});

		api.MapGet("/downloadClient/action/{action}", (string action) =>
		{
			return Results.Json(Array.Empty<object>());
		});
	}
}
