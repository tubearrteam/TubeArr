using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class ImportExclusionEndpoints
{
	internal const string ChannelTargetType = "channel";

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
			else if (sortKeyNormalized == "createdatutc" || sortKeyNormalized == "created")
			{
				sorted = descending
					? records.OrderByDescending(x => x.CreatedAtUtc)
					: records.OrderBy(x => x.CreatedAtUtc);
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
			var youtubeChannelId = (request.YoutubeChannelId ?? string.Empty).Trim();
			if (string.IsNullOrWhiteSpace(youtubeChannelId))
				return Results.BadRequest(new { message = "youtubeChannelId is required" });

			var targetType = NormalizeTargetType(request.TargetType);
			if (targetType is null)
				return Results.BadRequest(new { message = "targetType must be channel" });

			var title = (request.Title ?? string.Empty).Trim();
			if (string.IsNullOrWhiteSpace(title))
				title = youtubeChannelId;

			var entity = await db.ImportExclusions.FirstOrDefaultAsync(x => x.YoutubeChannelId == youtubeChannelId)
				?? new ImportExclusionEntity { YoutubeChannelId = youtubeChannelId, CreatedAtUtc = DateTimeOffset.UtcNow };

			entity.TargetType = targetType;
			entity.Title = title;
			entity.Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

			if (entity.Id == 0)
				db.ImportExclusions.Add(entity);

			await db.SaveChangesAsync();
			return Results.Json(CreateImportExclusionDto(entity));
		});

		api.MapPut("/import-exclusions/{id:int}", async (int id, SaveImportExclusionRequest request, TubeArrDbContext db) =>
		{
			var entity = await db.ImportExclusions.FirstOrDefaultAsync(x => x.Id == id);
			if (entity is null)
				return Results.NotFound();

			if (!string.IsNullOrWhiteSpace(request.YoutubeChannelId))
			{
				var y = request.YoutubeChannelId.Trim();
				var taken = await db.ImportExclusions.AnyAsync(x => x.YoutubeChannelId == y && x.Id != id);
				if (taken)
					return Results.BadRequest(new { message = "youtubeChannelId already excluded" });
				entity.YoutubeChannelId = y;
			}

			if (request.TargetType is not null)
			{
				var tt = NormalizeTargetType(request.TargetType);
				if (tt is null)
					return Results.BadRequest(new { message = "targetType must be channel" });
				entity.TargetType = tt;
			}

			if (request.Title is not null)
			{
				var t = request.Title.Trim();
				entity.Title = string.IsNullOrWhiteSpace(t) ? entity.YoutubeChannelId : t;
			}

			if (request.Reason is not null)
				entity.Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

			await db.SaveChangesAsync();
			return Results.Json(CreateImportExclusionDto(entity));
		});

		api.MapDelete("/import-exclusions/{id:int}", async (int id, TubeArrDbContext db) =>
		{
			var entity = await db.ImportExclusions.FirstOrDefaultAsync(x => x.Id == id);
			if (entity is null)
				return Results.NotFound();

			db.ImportExclusions.Remove(entity);
			await db.SaveChangesAsync();
			return Results.Ok();
		});

		api.MapDelete("/import-exclusions/bulk", async ([FromBody] DeleteImportExclusionsRequest? request, TubeArrDbContext db) =>
		{
			if (request?.Ids is null)
				return Results.BadRequest(new { message = "ids is required" });

			var ids = request.Ids.Where(id => id > 0).ToList();

			if (ids.Count == 0)
				return Results.Json(new { removed = 0 });

			var items = await db.ImportExclusions.Where(x => ids.Contains(x.Id)).ToListAsync();
			if (items.Count == 0)
				return Results.Json(new { removed = 0 });

			db.ImportExclusions.RemoveRange(items);
			await db.SaveChangesAsync();
			return Results.Json(new { removed = items.Count });
		});
	}

	internal static ImportExclusionDto CreateImportExclusionDto(ImportExclusionEntity exclusion) =>
		new(
			exclusion.Id,
			exclusion.TargetType,
			exclusion.YoutubeChannelId,
			exclusion.Title,
			exclusion.Reason,
			exclusion.CreatedAtUtc);

	static string? NormalizeTargetType(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
			return ChannelTargetType;
		if (string.Equals(raw.Trim(), ChannelTargetType, StringComparison.OrdinalIgnoreCase))
			return ChannelTargetType;
		return null;
	}
}
