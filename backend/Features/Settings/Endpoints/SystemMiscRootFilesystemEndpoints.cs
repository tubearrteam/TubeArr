using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;
using TubeArr.Backend.Serialization;

namespace TubeArr.Backend;

public static partial class SystemMiscEndpoints
{
	static partial void MapRootFolderAndFilesystemEndpoints(RouteGroupBuilder api)
	{
		api.MapGet("/rootFolder/{id:int}", async (int id, TubeArrDbContext db, LibraryImportScanService scanner, CancellationToken ct) =>
		{
			var detail = await scanner.BuildRootFolderDetailAsync(id, db, ct);
			return detail is null ? Results.NotFound() : Results.Json(detail);
		});

		api.MapGet("/rootFolder/{id:int}/scan-stream", async (int id, HttpContext http, TubeArrDbContext db, LibraryImportScanService scanner, CancellationToken ct) =>
		{
			if (!await db.RootFolders.AsNoTracking().AnyAsync(r => r.Id == id, ct))
			{
				http.Response.StatusCode = StatusCodes.Status404NotFound;
				return;
			}

			var jsonOptions = new JsonSerializerOptions();
			TubeArrJsonSerializer.ApplyApiDefaults(jsonOptions);

			http.Response.Headers.CacheControl = "no-cache, no-transform";
			http.Response.Headers.Append("X-Accel-Buffering", "no");
			http.Response.ContentType = "text/event-stream; charset=utf-8";

			http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

			try
			{
				await scanner.BuildRootFolderDetailWithProgressAsync(
					id,
					db,
					async (evt, c) =>
					{
						var line = JsonSerializer.Serialize(evt, jsonOptions);
						await http.Response.WriteAsync("data: " + line + "\n\n", c);
						await http.Response.Body.FlushAsync(c);
					},
					ct);
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				/* client disconnected */
			}
		});

		api.MapGet("/rootfolder", async (TubeArrDbContext db) =>
		{
			var rows = await db.RootFolders
				.OrderBy(x => x.Path)
				.Select(x => new { x.Id, x.Path })
				.ToListAsync();
			var list = rows.ConvertAll(x =>
			{
				var (accessible, freeSpace) = RootFolderPathProbe.GetStats(x.Path);
				return new
				{
					id = x.Id,
					path = x.Path,
					accessible,
					freeSpace,
					unmappedFolders = Array.Empty<object>()
				};
			});
			return Results.Json(list);
		});

		api.MapPost("/rootFolder", async (RootFolderCreateRequest request, TubeArrDbContext db) =>
		{
			var path = request.Path?.Trim();
			if (string.IsNullOrEmpty(path))
				return ApiErrorResults.BadRequest(TubeArrErrorCodes.InvalidInput, "path is required");
			var entity = new RootFolderEntity { Path = path };
			db.RootFolders.Add(entity);
			await db.SaveChangesAsync();
			var (accessible, freeSpace) = RootFolderPathProbe.GetStats(entity.Path);
			return Results.Json(new { id = entity.Id, path = entity.Path, accessible, freeSpace, unmappedFolders = Array.Empty<object>() });
		});

		api.MapDelete("/rootFolder/{id:int}", async (int id, TubeArrDbContext db) =>
		{
			var entity = await db.RootFolders.FindAsync(id);
			if (entity == null)
				return Results.NotFound();
			db.RootFolders.Remove(entity);
			await db.SaveChangesAsync();
			return Results.NoContent();
		});

		api.MapGet("/filesystem", (string? path, HttpContext ctx, bool includeFiles = false) =>
		{
			try
			{
				var basePath = string.IsNullOrWhiteSpace(path) ? "" : path.Trim();
				string fullPath;
				if (string.IsNullOrEmpty(basePath))
				{
					if (OperatingSystem.IsWindows())
						fullPath = "";
					else
						fullPath = Path.GetFullPath("/");
				}
				else
				{
					fullPath = Path.GetFullPath(basePath);
					if (!Directory.Exists(fullPath))
						return Results.Json(new { path = basePath, directories = Array.Empty<object>(), files = Array.Empty<object>(), parent = (string?)null });
				}

				var directories = new List<object>();
				var files = new List<object>();
				string? parent = null;
				if (string.IsNullOrEmpty(fullPath) && OperatingSystem.IsWindows())
				{
					foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
						directories.Add(new { name = drive.Name.TrimEnd('\\', '/'), path = drive.Name, size = 0L, lastModified = "", type = "folder" });
				}
				else
				{
					try
					{
						parent = Path.GetDirectoryName(fullPath);
						if (string.IsNullOrEmpty(parent) && OperatingSystem.IsWindows() && fullPath.Length >= 2 && fullPath[1] == ':')
							parent = "";
						foreach (var dir in Directory.EnumerateDirectories(fullPath))
						{
							try
							{
								var name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
								directories.Add(new { name, path = dir, size = 0L, lastModified = Directory.GetLastWriteTimeUtc(dir).ToString("O"), type = "folder" });
							}
							catch
							{
								// skip inaccessible
							}
						}
						if (includeFiles)
						{
							foreach (var file in Directory.EnumerateFiles(fullPath))
							{
								try
								{
									var fi = new FileInfo(file);
									files.Add(new { name = fi.Name, path = file, size = fi.Length, lastModified = fi.LastWriteTimeUtc.ToString("O"), type = "file" });
								}
								catch
								{
									// skip inaccessible
								}
							}
						}
					}
					catch (UnauthorizedAccessException)
					{
					}
					catch (DirectoryNotFoundException)
					{
					}
				}

				return Results.Json(new { path = basePath ?? "", directories, files, parent });
			}
			catch (Exception ex)
			{
				return Results.Json(new { path = path ?? "", directories = Array.Empty<object>(), files = Array.Empty<object>(), parent = (string?)null, error = ex.Message }, statusCode: 500);
			}
		});
	}
}
