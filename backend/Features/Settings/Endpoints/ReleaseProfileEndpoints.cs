using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace TubeArr.Backend;

internal static class ReleaseProfileEndpoints
{
	internal static void Map(RouteGroupBuilder api)
	{
		api.MapGet("/releaseProfile", () => Results.Json(Array.Empty<object>()));

		api.MapPost("/releaseProfile", () =>
		{
			return Results.Created("/releaseProfile/1", new { id = 1, name = "New Release Profile" });
		});

		api.MapGet("/releaseProfile/{id:int}", (int id) =>
		{
			var releaseProfile = new
			{
				id = id,
				name = $"Release Profile {id}",
				enabled = true,
				indexers = new object[] { },
				description = "",
				preferred = new object[] { },
				ignored = new object[] { },
				tags = new object[] { }
			};
			return Results.Json(releaseProfile);
		});

		api.MapPut("/releaseProfile/{id:int}", () => Results.NoContent());

		api.MapDelete("/releaseProfile/{id:int}", () => Results.NoContent());
	}
}
