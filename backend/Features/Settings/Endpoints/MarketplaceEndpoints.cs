using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace TubeArr.Backend;

internal static class MarketplaceEndpoints
{
	internal static void Map(RouteGroupBuilder api)
	{
		api.MapGet("/marketplace/listBlockAll", () => Results.Json(Array.Empty<object>()));

		api.MapPost("/marketplace/listBlockAll", () => Results.NoContent());

		api.MapDelete("/marketplace/listBlockAll/{id:int}", () => Results.NoContent());

		api.MapGet("/marketplace/listFormat", () =>
		{
			var customFormats = new object[]
			{
				new
				{
					id = 0,
					name = "Standard",
					description = "Standard format specifications",
					tags = new[] { "format" },
					score = 0
				}
			};
			return Results.Json(customFormats);
		});

		api.MapPost("/marketplace/listFormat", () => Results.Created("/marketplace/listFormat/1", new { id = 1 }));

		api.MapGet("/marketplace/listImportList", () => Results.Json(Array.Empty<object>()));

		api.MapGet("/marketplace/listNotification", () => Results.Json(Array.Empty<object>()));

		api.MapGet("/marketplace/listDownloadClient", () => Results.Json(Array.Empty<object>()));

		api.MapGet("/marketplace/listIndexer", () => Results.Json(Array.Empty<object>()));

		api.MapGet("/marketplace/listIndexerFlag", () => Results.Json(Array.Empty<object>()));

		api.MapPost("/marketplace/search", () => Results.Json(Array.Empty<object>()));
	}
}
