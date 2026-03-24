using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace TubeArr.Backend;

internal static class QualityProfileEndpoints
{
	internal static void Map(RouteGroupBuilder api)
	{
		api.MapGet("/qualityProfile", () =>
		{
			var qualityProfile = new
			{
				id = 1,
				name = "Default",
				upgradeAllowed = true,
				cutoff = 3,
				minimumCustomFormatScore = 0,
				cutoffFormatScore = 0,
				formatItems = new object[]
				{
					new { format = new { id = 0, name = "Unknown", includeCustomFormatWhenRenaming = false }, allowed = true, score = 0 },
					new { format = new { id = 6, name = "WEBDL 1080p", includeCustomFormatWhenRenaming = false }, allowed = true, score = 0 },
					new { format = new { id = 7, name = "Bluray 1080p", includeCustomFormatWhenRenaming = false }, allowed = true, score = 0 },
					new { format = new { id = 4, name = "HDTV 720p", includeCustomFormatWhenRenaming = false }, allowed = true, score = 0 }
				}
			};
			return Results.Json(new[] { qualityProfile });
		});

		api.MapPost("/qualityProfile", () => Results.Created("/qualityProfile/1", new { id = 1 }));

		api.MapGet("/qualityProfile/{id}", (int id) =>
		{
			var qualityProfile = new
			{
				id = id,
				name = $"Profile {id}",
				upgradeAllowed = true,
				cutoff = 3,
				minimumCustomFormatScore = 0,
				cutoffFormatScore = 0,
				formatItems = new object[]
				{
					new { format = new { id = 0, name = "Unknown", includeCustomFormatWhenRenaming = false }, allowed = true, score = 0 }
				}
			};
			return Results.Json(qualityProfile);
		});

		api.MapPut("/qualityProfile/{id}", () => Results.NoContent());

		api.MapDelete("/qualityProfile/{id}", () => Results.NoContent());

		api.MapGet("/quality", () =>
		{
			var qualities = new object[]
			{
				new { id = 0, name = "Unknown", source = "Unknown", resolution = 0, modifier = "Unknown", megabytesPerMinute = 0.0 },
				new { id = 6, name = "WEBDL 1080p", source = "WebDL", resolution = 1080, modifier = "Unknown", megabytesPerMinute = 5.0 },
				new { id = 7, name = "Bluray 1080p", source = "Bluray", resolution = 1080, modifier = "Unknown", megabytesPerMinute = 20.0 },
				new { id = 4, name = "HDTV 720p", source = "HDTV", resolution = 720, modifier = "Unknown", megabytesPerMinute = 4.0 }
			};
			return Results.Json(qualities);
		});
	}
}
