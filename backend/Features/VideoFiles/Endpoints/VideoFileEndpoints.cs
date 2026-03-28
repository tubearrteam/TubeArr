using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;
using TubeArr.Backend.Realtime;
using System.Text.RegularExpressions;

namespace TubeArr.Backend;

public static class VideoFileEndpoints
{
	public static void Map(RouteGroupBuilder api)
	{
		api.MapGet("/videoFile", async (int? channelId, int[]? videoFileIds, TubeArrDbContext db, CancellationToken ct) =>
		{
			if ((!channelId.HasValue || channelId.Value <= 0) && (videoFileIds is not { Length: > 0 }))
				return Results.Json(Array.Empty<object>());

			if (channelId.HasValue && channelId.Value > 0)
			{
				try
				{
					var channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == channelId.Value);
					var naming = await db.NamingConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync() ?? new NamingConfigEntity { Id = 1 };
					var rootFolders = await db.RootFolders.AsNoTracking().ToListAsync();
					if (channel is not null && rootFolders.Count > 0)
					{
						var channelVideos = await db.Videos.AsNoTracking()
							.Where(v => v.ChannelId == channelId.Value)
							.Select(v => new { v.Id, v.ChannelId, v.PlaylistId, v.YoutubeVideoId })
							.ToListAsync();
						var videoByYoutubeId = channelVideos
							.Where(v => !string.IsNullOrWhiteSpace(v.YoutubeVideoId))
							.ToDictionary(v => v.YoutubeVideoId, StringComparer.OrdinalIgnoreCase);

						var scanRoots = new List<string>();
						if (!string.IsNullOrWhiteSpace(channel.Path))
						{
							foreach (var root in rootFolders)
							{
								var rootPath = (root.Path ?? "").Trim();
								if (string.IsNullOrWhiteSpace(rootPath))
									continue;

								var path = Path.IsPathRooted(channel.Path)
									? channel.Path
									: Path.Combine(rootPath, channel.Path);

								if (!string.IsNullOrWhiteSpace(path))
									scanRoots.Add(path);
							}
						}
						else
						{
							var dummyVideo = new VideoEntity { Title = "", YoutubeVideoId = "", UploadDateUtc = DateTimeOffset.UtcNow };
							var context = new VideoFileNaming.NamingContext(Channel: channel, Video: dummyVideo, Playlist: null, PlaylistIndex: null, QualityFull: null, Resolution: null, Extension: null);
							var channelFolderName = VideoFileNaming.BuildFolderName(naming.ChannelFolderFormat, context, naming);
							if (!string.IsNullOrWhiteSpace(channelFolderName))
							{
								foreach (var root in rootFolders)
								{
									var rootPath = (root.Path ?? "").Trim();
									if (!string.IsNullOrWhiteSpace(rootPath))
										scanRoots.Add(Path.Combine(rootPath, channelFolderName));
								}
							}
						}

						var mediaExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
						{
							".mp4", ".mkv", ".webm", ".avi", ".mov", ".m4v", ".flv", ".wmv", ".mpg", ".mpeg",
							".m4a", ".mp3", ".aac", ".opus", ".ogg", ".wav", ".flac"
						};

						var foundByVideoId = new Dictionary<int, (string Path, string RelativePath, long Size, int ChannelId, int? PlaylistId)>();
						foreach (var root in scanRoots.Distinct(StringComparer.OrdinalIgnoreCase))
						{
							if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
								continue;

							try
							{
								foreach (var filePath in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
								{
									var ext = Path.GetExtension(filePath);
									if (!mediaExts.Contains(ext))
										continue;

									var name = Path.GetFileName(filePath);
									if (string.IsNullOrWhiteSpace(name))
										continue;

									var match = Regex.Match(name, @"\[(?<id>[^\]]+)\]", RegexOptions.IgnoreCase);
									if (!match.Success)
										continue;

									var youtubeVideoId = match.Groups["id"].Value.Trim();
									if (string.IsNullOrWhiteSpace(youtubeVideoId))
										continue;

									if (!videoByYoutubeId.TryGetValue(youtubeVideoId, out var video))
										continue;

									var fi = new FileInfo(filePath);
									var relativePath = Path.GetRelativePath(root, filePath);
									foundByVideoId[video.Id] = (filePath, relativePath, fi.Exists ? fi.Length : 0, video.ChannelId, video.PlaylistId);
								}
							}
							catch
							{
								// best-effort scan
							}
						}

						var existingRows = await db.VideoFiles.Where(vf => vf.ChannelId == channelId.Value).ToListAsync();
						foreach (var row in existingRows)
						{
							if (!foundByVideoId.TryGetValue(row.VideoId, out var found))
							{
								db.VideoFiles.Remove(row);
								continue;
							}

							row.Path = found.Path;
							row.RelativePath = found.RelativePath;
							row.Size = found.Size;
							row.ChannelId = found.ChannelId;
							row.PlaylistId = found.PlaylistId;
							row.DateAdded = DateTimeOffset.UtcNow;
							foundByVideoId.Remove(row.VideoId);
						}

						foreach (var kv in foundByVideoId)
						{
							var found = kv.Value;
							db.VideoFiles.Add(new VideoFileEntity
							{
								VideoId = kv.Key,
								ChannelId = found.ChannelId,
								PlaylistId = found.PlaylistId,
								Path = found.Path,
								RelativePath = found.RelativePath,
								Size = found.Size,
								DateAdded = DateTimeOffset.UtcNow
							});
						}

						await db.SaveChangesAsync();
					}
				}
				catch
				{
					// keep endpoint resilient: return persisted rows even if sync scan fails
				}
			}

			try
			{
				var query = db.VideoFiles.AsNoTracking().AsQueryable();
				if (channelId.HasValue && channelId.Value > 0)
					query = query.Where(vf => vf.ChannelId == channelId.Value);

				if (videoFileIds is { Length: > 0 })
					query = query.Where(vf => videoFileIds.Contains(vf.Id));

				// SQLite provider cannot translate DateTimeOffset ORDER BY, so sort client-side.
				var rows = (await query.ToListAsync())
					.OrderByDescending(vf => vf.DateAdded)
					.ToList();

				if (rows.Count == 0)
					return Results.Json(Array.Empty<object>());

				var staleIds = rows
					.Where(vf => string.IsNullOrWhiteSpace(vf.Path) || !File.Exists(vf.Path))
					.Select(vf => vf.Id)
					.ToList();

				if (staleIds.Count > 0)
				{
					await db.VideoFiles.Where(vf => staleIds.Contains(vf.Id)).ExecuteDeleteAsync();
					rows = rows.Where(vf => !staleIds.Contains(vf.Id)).ToList();
				}

				var channelIds = rows.Select(vf => vf.ChannelId).Distinct().ToList();
				var playlistRows = await db.Playlists.AsNoTracking()
					.Where(p => channelIds.Contains(p.ChannelId))
					.OrderBy(p => p.ChannelId)
					.ThenBy(p => p.Id)
					.ToListAsync(ct);

				var maxUploadByPlaylist = await ChannelDtoMapper.LoadMaxUploadUtcByPlaylistIdsAsync(db, playlistRows.Select(p => p.Id), ct);
				var playlistNumberByPlaylistId = new Dictionary<int, int>();
				foreach (var group in playlistRows.GroupBy(p => p.ChannelId))
				{
					var ordered = ChannelDtoMapper.OrderPlaylistsByLatestUpload(group.ToList(), maxUploadByPlaylist);
					var number = 2;
					foreach (var playlist in ordered)
						playlistNumberByPlaylistId[playlist.Id] = number++;
				}

				var results = rows.Select(vf => new VideoFileDto(
					Id: vf.Id,
					ChannelId: vf.ChannelId,
					PlaylistNumber: vf.PlaylistId.HasValue && playlistNumberByPlaylistId.TryGetValue(vf.PlaylistId.Value, out var pn) ? pn : 1,
					Path: vf.Path,
					RelativePath: vf.RelativePath,
					Size: vf.Size,
					DateAdded: vf.DateAdded,
					ReleaseGroup: "",
					Languages: Array.Empty<object>(),
					Quality: DefaultQuality(),
					CustomFormats: Array.Empty<object>(),
					CustomFormatScore: 0,
					IndexerFlags: 0,
					ReleaseType: "",
					MediaInfo: null,
					QualityCutoffNotMet: false
				));

				return Results.Json(results);
			}
			catch
			{
				return Results.Json(Array.Empty<object>());
			}
		});

		api.MapGet("/videoFile/{id:int}", async (int id, TubeArrDbContext db, CancellationToken ct) =>
		{
			var file = await db.VideoFiles.AsNoTracking().FirstOrDefaultAsync(vf => vf.Id == id, ct);
			if (file is null)
				return Results.NotFound();

			if (string.IsNullOrWhiteSpace(file.Path) || !File.Exists(file.Path))
			{
				await db.VideoFiles.Where(vf => vf.Id == id).ExecuteDeleteAsync(ct);
				return Results.NotFound();
			}

			var playlistNumber = 1;
			if (file.PlaylistId.HasValue)
			{
				var orderedPlaylistIds = await ChannelDtoMapper.LoadOrderedPlaylistIdsForChannelAsync(db, file.ChannelId, ct);
				var idx = orderedPlaylistIds.IndexOf(file.PlaylistId.Value);
				if (idx >= 0)
					playlistNumber = idx + 2;
			}

			return Results.Json(new VideoFileDto(
				Id: file.Id,
				ChannelId: file.ChannelId,
				PlaylistNumber: playlistNumber,
				Path: file.Path,
				RelativePath: file.RelativePath,
				Size: file.Size,
				DateAdded: file.DateAdded,
				ReleaseGroup: "",
				Languages: Array.Empty<object>(),
				Quality: DefaultQuality(),
				CustomFormats: Array.Empty<object>(),
				CustomFormatScore: 0,
				IndexerFlags: 0,
				ReleaseType: "",
				MediaInfo: null,
				QualityCutoffNotMet: false
			));
		});

		api.MapDelete("/videoFile/{id:int}", async (int id, TubeArrDbContext db, IRealtimeEventBroadcaster realtime) =>
		{
			var file = await db.VideoFiles.FirstOrDefaultAsync(vf => vf.Id == id);
			if (file is null)
				return Results.Ok();

			var video = await db.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == file.VideoId);
			try
			{
				if (!string.IsNullOrWhiteSpace(file.Path) && File.Exists(file.Path))
					File.Delete(file.Path);
			}
			catch
			{
				// best-effort delete on disk
			}

			db.DownloadHistory.Add(new DownloadHistoryEntity
			{
				ChannelId = file.ChannelId,
				VideoId = file.VideoId,
				PlaylistId = file.PlaylistId,
				EventType = 5, // deleted
				SourceTitle = video?.Title ?? $"Video {file.VideoId}",
				OutputPath = file.Path,
				Message = "Deleted manually from video files.",
				DownloadId = null,
				Date = DateTime.UtcNow
			});

			db.VideoFiles.Remove(file);
			await db.SaveChangesAsync();
			await RealtimeBroadcastHelper.BroadcastLiveQueueAndHistoryAsync(realtime);
			return Results.Ok();
		});

		api.MapDelete("/videoFile/bulk", async ([FromBody] DeleteVideoFilesRequest? request, TubeArrDbContext db, IRealtimeEventBroadcaster realtime) =>
		{
			var ids = request?.VideoFileIds?
				.Where(id => id > 0)
				.Distinct()
				.ToList() ?? new List<int>();

			if (ids.Count == 0)
				return Results.Ok();

			var files = await db.VideoFiles.Where(vf => ids.Contains(vf.Id)).ToListAsync();
			var deletedVideoIds = files.Select(f => f.VideoId).Distinct().ToList();
			var videosById = deletedVideoIds.Count == 0
				? new Dictionary<int, VideoEntity>()
				: await db.Videos.AsNoTracking()
					.Where(v => deletedVideoIds.Contains(v.Id))
					.ToDictionaryAsync(v => v.Id);

			foreach (var file in files)
			{
				try
				{
					if (!string.IsNullOrWhiteSpace(file.Path) && File.Exists(file.Path))
						File.Delete(file.Path);
				}
				catch
				{
					// best-effort delete on disk
				}

				videosById.TryGetValue(file.VideoId, out var video);
				db.DownloadHistory.Add(new DownloadHistoryEntity
				{
					ChannelId = file.ChannelId,
					VideoId = file.VideoId,
					PlaylistId = file.PlaylistId,
					EventType = 5, // deleted
					SourceTitle = video?.Title ?? $"Video {file.VideoId}",
					OutputPath = file.Path,
					Message = "Deleted manually from video files (bulk).",
					DownloadId = null,
					Date = DateTime.UtcNow
				});
			}

			db.VideoFiles.RemoveRange(files);
			await db.SaveChangesAsync();
			await RealtimeBroadcastHelper.BroadcastLiveQueueAndHistoryAsync(realtime);
			return Results.Ok();
		});

		api.MapPut("/videoFile/bulk", async (VideoFileBulkSelectionRequest[]? request, TubeArrDbContext db) =>
		{
			var ids = request?
				.Select(item => item.Id)
				.Where(id => id > 0)
				.Distinct()
				.ToList() ?? new List<int>();

			if (ids.Count == 0)
				return Results.Json(Array.Empty<object>());

			var files = await db.VideoFiles.AsNoTracking()
				.Where(vf => ids.Contains(vf.Id))
				.ToListAsync();

			return Results.Json(files.Select(vf => new VideoFileBulkUpdateDto(
				Id: vf.Id,
				QualityCutoffNotMet: false,
				CustomFormats: Array.Empty<object>(),
				CustomFormatScore: 0
			)));
		});
	}

	private static VideoFileQualityWrapper DefaultQuality() =>
		new(new VideoFileQualityDetails(0, "Unknown", "unknown", 0), new VideoFileRevision(1, 0, false));
}

