using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class TagEndpoints
{
	internal static void Map(RouteGroupBuilder api)
	{
		api.MapGet("/tag", async (TubeArrDbContext db) =>
		{
			var tags = await db.Tags.OrderBy(t => t.Id)
				.Select(t => new { id = t.Id, label = t.Label })
				.ToListAsync();
			return Results.Json(tags);
		});

		api.MapGet("/tag/detail", async (TubeArrDbContext db) =>
		{
			var tags = await db.Tags.OrderBy(t => t.Id).ToListAsync();
			var details = tags.Select(t => new Dictionary<string, object?>
			{
				["id"] = t.Id,
				["label"] = t.Label,
				["channelIds"] = Array.Empty<int>(),
				["delayProfileIds"] = Array.Empty<int>(),
				["notificationIds"] = Array.Empty<int>(),
				["restrictionIds"] = Array.Empty<int>(),
				["indexerIds"] = Array.Empty<int>(),
				["downloadClientIds"] = Array.Empty<int>(),
				["autoTagIds"] = Array.Empty<int>()
			}).ToArray();
			return Results.Json(details);
		});

		api.MapPost("/tag", async (TagCreateRequest request, TubeArrDbContext db) =>
		{
			var label = request.Label;
			if (string.IsNullOrWhiteSpace(label))
				return Results.BadRequest();

			var entity = new TagEntity { Label = label.Trim() };
			db.Tags.Add(entity);
			await db.SaveChangesAsync();
			return Results.Json(new { id = entity.Id, label = entity.Label });
		});

		api.MapDelete("/tag/{id:int}", async (int id, TubeArrDbContext db) =>
		{
			var entity = await db.Tags.FindAsync(id);
			if (entity is null)
				return Results.NotFound();
			db.Tags.Remove(entity);
			await db.SaveChangesAsync();
			return Results.Ok();
		});
	}
}
