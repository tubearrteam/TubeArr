using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class CustomFilterEndpoints
{
	internal static void Map(RouteGroupBuilder api)
	{
		api.MapGet("/customFilter", async (TubeArrDbContext db) =>
		{
			var entities = await db.CustomFilters.OrderBy(x => x.Id).ToListAsync();
			return Results.Json(entities.Select(ToDto).ToArray());
		});

		api.MapGet("/customFilter/{id:int}", async (int id, TubeArrDbContext db) =>
		{
			var entity = await db.CustomFilters.FindAsync(id);
			return entity is null ? Results.NotFound() : Results.Json(ToDto(entity));
		});

		api.MapPost("/customFilter", async (CustomFilterSaveRequest request, TubeArrDbContext db) =>
		{
			var entity = new CustomFilterEntity
			{
				Type = request.Type,
				Label = request.Label,
				FiltersJson = JsonSerializer.Serialize(request.Filters ?? new List<PropertyFilterDto>())
			};
			db.CustomFilters.Add(entity);
			await db.SaveChangesAsync();
			return Results.Json(ToDto(entity));
		});

		api.MapPut("/customFilter/{id:int}", async (int id, CustomFilterSaveRequest request, TubeArrDbContext db) =>
		{
			var entity = await db.CustomFilters.FindAsync(id);
			if (entity is null)
				return Results.NotFound();
			entity.Type = request.Type;
			entity.Label = request.Label;
			entity.FiltersJson = JsonSerializer.Serialize(request.Filters ?? new List<PropertyFilterDto>());
			await db.SaveChangesAsync();
			return Results.Json(ToDto(entity));
		});

		api.MapDelete("/customFilter/{id:int}", async (int id, TubeArrDbContext db) =>
		{
			var entity = await db.CustomFilters.FindAsync(id);
			if (entity is null)
				return Results.NotFound();
			db.CustomFilters.Remove(entity);
			await db.SaveChangesAsync();
			return Results.Ok();
		});
	}

	private static CustomFilterDto ToDto(CustomFilterEntity entity)
	{
		var filters = JsonSerializer.Deserialize<List<PropertyFilterDto>>(entity.FiltersJson)
					  ?? new List<PropertyFilterDto>();
		return new CustomFilterDto(entity.Id, entity.Type, entity.Label, filters);
	}
}
