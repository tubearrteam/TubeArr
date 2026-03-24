using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace TubeArr.Backend;

internal static class CustomFormatsEndpoints
{
	internal static void Map(RouteGroupBuilder api)
	{
		api.MapGet("/customFormat", () =>
		{
			var customFormats = new object[]
			{
				new
				{
					id = 1,
					name = "H.264",
					description = "Videos encoded with H.264 codec",
					specifications = new object[]
					{
						new
						{
							name = "Codec",
							implementation = "ReleaseTitleRegex",
							negate = false,
							required = false,
							fields = new object[]
							{
								new { name = "Value", value = "H\\.?264" }
							}
						}
					},
					tags = new object[] { }
				}
			};
			return Results.Json(customFormats);
		});

		api.MapPost("/customFormat", () =>
		{
			return Results.Created("/customFormat/1", new { id = 1, name = "New Format" });
		});

		api.MapGet("/customFormat/{id:int}", (int id) =>
		{
			var customFormat = new
			{
				id = id,
				name = $"Format {id}",
				description = $"Custom format {id}",
				specifications = new object[] { },
				tags = new object[] { }
			};
			return Results.Json(customFormat);
		});

		api.MapPut("/customFormat/{id:int}", () => Results.NoContent());

		api.MapDelete("/customFormat/{id:int}", () => Results.NoContent());

		api.MapGet("/customFormat/schema", () =>
		{
			var specifications = new object[]
			{
				new
				{
					name = "Release Title Regex",
					implementation = "ReleaseTitleRegex",
					implementationName = "Release Title Regex",
					info = "Match release title with a regex pattern",
					negate = new { order = 0 },
					required = new { order = 1 },
					fields = new[]
					{
						new { name = "Value", label = "Value", helpText = "Regex pattern to match" }
					}
				},
				new
				{
					name = "Release Name",
					implementation = "ReleaseName",
					implementationName = "Release Name",
					info = "Match release name",
					negate = new { order = 0 },
					required = new { order = 1 },
					fields = new[]
					{
						new { name = "Value", label = "Value", helpText = "Release name pattern" }
					}
				}
			};
			return Results.Json(specifications);
		});
	}
}
