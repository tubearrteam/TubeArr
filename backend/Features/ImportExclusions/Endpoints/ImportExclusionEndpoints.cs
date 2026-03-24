using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class ImportExclusionEndpoints
{
	internal static void Map(RouteGroupBuilder api)
	{
		api.MapGet("/import-exclusions/paged", async (
			int page,
			int pageSize,
			string? sortKey,
			string? sortDirection,
			TubeArrDbContext db) =>
		{
			page = page <= 0 ? 1 : page;
			pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 200);
			sortKey ??= "title";
			sortDirection ??= "ascending";

			var all = await db.ImportExclusions.AsNoTracking().ToListAsync();
			var records = all.Select(CreateImportExclusionDto).ToList();

			var descending = sortDirection.Equals("descending", StringComparison.OrdinalIgnoreCase);
			var sortKeyNormalized = sortKey.Trim().ToLowerInvariant();

			IEnumerable<ImportExclusionDto> sorted;
			if (sortKeyNormalized == "youtubechannelid")
			{
				sorted = descending
					? records.OrderByDescending(x => x.YoutubeChannelId)
					: records.OrderBy(x => x.YoutubeChannelId);
			}
			else
			{
				sorted = descending
					? records.OrderByDescending(x => x.Title)
					: records.OrderBy(x => x.Title);
			}

			var totalRecords = all.Count;
			var paged = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToArray();

			return Results.Json(new
			{
				page,
				pageSize,
				sortKey,
				sortDirection,
				totalRecords,
				records = paged
			});
		});

		api.MapPost("/import-exclusions", async (SaveImportExclusionRequest request, TubeArrDbContext db) =>
		{
			var title = (request.Title ?? string.Empty).Trim();
			if (string.IsNullOrWhiteSpace(title))
			{
				return Results.BadRequest(new { message = "title is required" });
			}

			var youtubeChannelId = string.IsNullOrWhiteSpace(request.YoutubeChannelId) ? null : request.YoutubeChannelId.Trim();

			ImportExclusionEntity entity;
			if (!string.IsNullOrWhiteSpace(youtubeChannelId))
			{
				entity = await db.ImportExclusions.FirstOrDefaultAsync(x => x.YoutubeChannelId == youtubeChannelId)
					?? new ImportExclusionEntity { YoutubeChannelId = youtubeChannelId };
			}
			else
			{
				entity = new ImportExclusionEntity();
			}

			entity.Title = title.Trim();
			entity.YoutubeChannelId = youtubeChannelId;

			if (entity.Id == 0)
			{
				db.ImportExclusions.Add(entity);
			}

			await db.SaveChangesAsync();
			return Results.Json(CreateImportExclusionDto(entity));
		});

		api.MapPut("/import-exclusions/{id:int}", async (int id, SaveImportExclusionRequest request, TubeArrDbContext db) =>
		{
			var entity = await db.ImportExclusions.FirstOrDefaultAsync(x => x.Id == id);
			if (entity is null)
			{
				return Results.NotFound();
			}

			if (!string.IsNullOrWhiteSpace(request.Title))
			{
				entity.Title = request.Title.Trim();
			}

			if (request.YoutubeChannelId is not null)
			{
				entity.YoutubeChannelId = string.IsNullOrWhiteSpace(request.YoutubeChannelId) ? null : request.YoutubeChannelId.Trim();
			}

			await db.SaveChangesAsync();
			return Results.Json(CreateImportExclusionDto(entity));
		});

		api.MapDelete("/import-exclusions/{id:int}", async (int id, TubeArrDbContext db) =>
		{
			var entity = await db.ImportExclusions.FirstOrDefaultAsync(x => x.Id == id);
			if (entity is null)
			{
				return Results.NotFound();
			}

			db.ImportExclusions.Remove(entity);
			await db.SaveChangesAsync();
			return Results.Ok();
		});

		api.MapDelete("/import-exclusions/bulk", async ([FromBody] DeleteImportExclusionsRequest? request, TubeArrDbContext db) =>
		{
			if (request?.Ids is null)
			{
				return Results.BadRequest(new { message = "ids is required" });
			}

			var ids = request.Ids.Where(id => id > 0).ToList();

			if (ids.Count == 0)
			{
				return Results.Json(new { removed = 0 });
			}

			var items = await db.ImportExclusions.Where(x => ids.Contains(x.Id)).ToListAsync();
			if (items.Count == 0)
			{
				return Results.Json(new { removed = 0 });
			}

			db.ImportExclusions.RemoveRange(items);
			await db.SaveChangesAsync();
			return Results.Json(new { removed = items.Count });
		});
	}

	internal static ImportExclusionDto CreateImportExclusionDto(ImportExclusionEntity exclusion) =>
		new ImportExclusionDto(exclusion.Id, exclusion.YoutubeChannelId, exclusion.Title);
}
