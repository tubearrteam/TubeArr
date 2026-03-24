using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace TubeArr.Backend;

internal static class IndexerEndpoints
{
	internal static void Map(RouteGroupBuilder api)
	{
		api.MapGet("/indexer", () =>
		{
			var indexers = new object[]
			{
				new
				{
					id = 1,
					name = "YouTube",
					implementation = "YouTubeIndexer",
					configContract = "YouTubeIndexerSettings",
					settings = new { },
					enable = true
				}
			};
			return Results.Json(indexers);
		});

		api.MapPost("/indexer", () =>
		{
			return Results.Created("/indexer/1", new { id = 1, name = "New Indexer" });
		});

		api.MapGet("/indexer/{id:int}", (int id) =>
		{
			var indexer = new
			{
				id = id,
				name = $"Indexer {id}",
				implementation = "YouTubeIndexer",
				enable = true,
				settings = new { }
			};
			return Results.Json(indexer);
		});

		api.MapPut("/indexer/{id:int}", () => Results.NoContent());

		api.MapDelete("/indexer/{id:int}", () => Results.NoContent());

		api.MapGet("/indexer/schema", () =>
		{
			var indexerDefinitions = new object[]
			{
				new
				{
					implementation = "YouTubeIndexer",
					implementationName = "YouTube",
					infoLink = "https://www.youtube.com",
					supportsRss = false,
					supportsSearch = true,
					supportsRedirect = false,
					fields = new object[]
					{
						new
						{
							name = "apiKey",
							label = "API Key",
							type = "password",
							value = ""
						}
					}
				}
			};
			return Results.Json(indexerDefinitions);
		});

		api.MapGet("/indexerFlag", () =>
		{
			var flags = new object[]
			{
				new { id = 0, name = "G - General Audiences", flag = 0 },
				new { id = 1, name = "PG - Parental Guidance", flag = 1 },
				new { id = 2, name = "PG-13 - Parents Strongly Cautioned", flag = 2 },
				new { id = 3, name = "R - Restricted", flag = 3 },
				new { id = 4, name = "NC-17 - Adults Only", flag = 4 }
			};
			return Results.Json(flags);
		});
	}
}
