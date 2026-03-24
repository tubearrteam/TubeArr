using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace TubeArr.Backend;

internal static class ImportListEndpoints
{
	internal static void Map(RouteGroupBuilder api)
	{
		api.MapGet("/importList", () => Results.Json(Array.Empty<object>()));

		api.MapPost("/importList", () =>
		{
			return Results.Created("/importList/1", new { id = 1, name = "New Import List" });
		});

		api.MapGet("/importList/{id:int}", (int id) =>
		{
			var importList = new
			{
				id = id,
				name = $"Import List {id}",
				implementation = "YoutubeTrendingListImporter",
				settings = new { }
			};
			return Results.Json(importList);
		});

		api.MapPut("/importList/{id:int}", () => Results.NoContent());

		api.MapDelete("/importList/{id:int}", () => Results.NoContent());

		api.MapGet("/importList/schema", () =>
		{
			var importListDefinitions = new object[]
			{
				new
				{
					implementation = "YoutubeTrendingListImporter",
					implementationName = "YouTube Trending",
					infoLink = "https://wiki.servarr.com/tubearr/settings#import-lists",
					supportsContentPaging = false,
					presets = new object[] { },
					fields = new object[]
					{
						new
						{
							name = "country",
							label = "Country Code",
							type = "select",
							value = "US",
							selectOptions = new object[]
							{
								new { name = "US", value = "US" },
								new { name = "GB", value = "GB" },
								new { name = "CA", value = "CA" }
							}
						}
					}
				},
				new
				{
					implementation = "YoutubePlayllistImporter",
					implementationName = "YouTube Playlist",
					infoLink = "https://wiki.servarr.com/tubearr/settings#import-lists",
					supportsContentPaging = false,
					presets = new object[] { },
					fields = new object[]
					{
						new
						{
							name = "playlistId",
							label = "Playlist ID",
							type = "textbox",
							value = ""
						}
					}
				}
			};
			return Results.Json(importListDefinitions);
		});

		api.MapGet("/importList/options", () =>
		{
			var options = new
			{
				id = 1,
				importListMinimumScore = 0,
				importListMinimumDays = 0,
				importListExcludeChannelIds = "",
				listSyncInterval = 12,
				listOrder = 1,
				enableAutoAddChannels = false,
				enableAutoAddChannelsByTag = false,
				autoAddChannelsByTag = ""
			};
			return Results.Json(options);
		});

		api.MapPut("/importList/options/{id:int}", () => Results.NoContent());
	}
}
