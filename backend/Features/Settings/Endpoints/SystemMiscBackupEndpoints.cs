using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

public static partial class SystemMiscEndpoints
{
	static partial void MapBackupEndpoints(RouteGroupBuilder api)
	{
		api.MapGet("/system/backup", async (TubeArrDbContext db, BackupRestoreService backup) =>
		{
			var items = await backup.ListBackupsAsync(db);
			return Results.Json(items);
		});

		api.MapGet("/system/backup/download/{id:int}", async (
			int id,
			HttpRequest request,
			TubeArrDbContext db,
			BackupRestoreService backup) =>
		{
			var settings = await ProgramStartupHelpers.GetOrCreateServerSettingsAsync(db);
			var expected = settings.ApiKey ?? "";
			var provided = request.Query["apikey"].FirstOrDefault();
			if (string.IsNullOrEmpty(provided))
				provided = request.Headers["X-Api-Key"].FirstOrDefault();

			if (!string.Equals(expected, provided, StringComparison.Ordinal))
				return Results.Unauthorized();

			var path = await backup.TryGetBackupZipPathAsync(db, id);
			if (path is null || !File.Exists(path))
				return Results.NotFound();

			return Results.File(path, "application/zip", Path.GetFileName(path));
		});

		api.MapDelete("/system/backup/{id:int}", async (int id, TubeArrDbContext db, BackupRestoreService backup) =>
		{
			var result = await backup.DeleteBackupAsync(db, id);
			if (!result.Ok)
				return Results.Json(new { message = result.Error ?? "Delete failed." }, statusCode: StatusCodes.Status404NotFound);

			return Results.Ok();
		});

		api.MapPost("/system/backup/restore/{id:int}", async (int id, TubeArrDbContext db, BackupRestoreService backup) =>
		{
			var result = await backup.StageRestoreFromBackupIdAsync(db, id);
			if (!result.Ok)
				return Results.BadRequest(new { message = result.Error ?? "Restore failed." });

			return Results.Ok();
		});

		api.MapPost("/system/backup/restore/upload", async (HttpRequest request, BackupRestoreService backup) =>
		{
			if (!request.HasFormContentType)
				return Results.BadRequest(new { message = "Expected multipart form data." });

			var form = await request.ReadFormAsync();
			var file = form.Files["restore"];
			if (file is null || file.Length == 0)
				return Results.BadRequest(new { message = "No file uploaded." });

			await using var stream = file.OpenReadStream();
			var result = await backup.StageRestoreFromZipStreamAsync(stream);
			if (!result.Ok)
				return Results.BadRequest(new { message = result.Error ?? "Restore failed." });

			return Results.Ok();
		});
	}
}
