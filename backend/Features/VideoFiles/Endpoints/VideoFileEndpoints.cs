using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;
using TubeArr.Backend.Media.Nfo;
using TubeArr.Backend.Realtime;
using System.Text.RegularExpressions;

namespace TubeArr.Backend;

public static class VideoFileEndpoints
{
	public static void Map(RouteGroupBuilder api)
	{
		api.MapGet("/rename", async (int channelId, int? playlistNumber, TubeArrDbContext db, CancellationToken ct) =>
		{
			if (channelId <= 0)
				return Results.Json(Array.Empty<object>());

			var channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == channelId, ct);
			if (channel is null)
				return Results.Json(Array.Empty<object>());

			var naming = await db.NamingConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct) ?? new NamingConfigEntity { Id = 1 };
			if (!naming.RenameVideos)
				return Results.Json(Array.Empty<object>());

			var media = await db.MediaManagementConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
			var useCustomNfos = media?.UseCustomNfos != false;

			var roots = await db.RootFolders.AsNoTracking().ToListAsync(ct);
			var showRoot = DownloadQueueProcessor.GetChannelShowRootPath(channel, new VideoEntity { Title = "", YoutubeVideoId = "", UploadDateUtc = DateTimeOffset.UtcNow }, naming, roots);
			if (string.IsNullOrWhiteSpace(showRoot))
				return Results.Json(Array.Empty<object>());

			// Resolve playlistId for a UI playlistNumber (2+ = curated playlist ordering).
			int? curatedPlaylistId = null;
			if (playlistNumber is > 1)
			{
				var orderedIds = await ChannelDtoMapper.LoadOrderedPlaylistIdsForChannelAsync(db, channelId, ct);
				var idx = playlistNumber.Value - 2;
				if (idx >= 0 && idx < orderedIds.Count)
					curatedPlaylistId = orderedIds[idx];
			}

			var fileQuery = db.VideoFiles.AsNoTracking()
				.Where(vf => vf.ChannelId == channelId && vf.Path != null && vf.Path != "");
			if (curatedPlaylistId.HasValue)
			{
				// In playlist preview, only rename media currently associated with that curated playlist.
				fileQuery = fileQuery.Where(vf => vf.PlaylistId == curatedPlaylistId.Value);
			}

			var videoFiles = await fileQuery.ToListAsync(ct);
			if (videoFiles.Count == 0)
				return Results.Json(Array.Empty<object>());

			var videoIds = videoFiles.Select(vf => vf.VideoId).Distinct().ToList();
			var videos = await db.Videos.AsNoTracking()
				.Where(v => videoIds.Contains(v.Id))
				.ToDictionaryAsync(v => v.Id, ct);

			var playlists = await db.Playlists.AsNoTracking()
				.Where(p => p.ChannelId == channelId)
				.ToListAsync(ct);
			var playlistById = playlists.ToDictionary(p => p.Id);

			// Primary playlist for file organization is per-video (multi-match strategy aware).
			var primaryPlaylistByVideoId = await ChannelDtoMapper.LoadPrimaryPlaylistIdByVideoIdsForChannelAsync(db, channelId, videoIds, ct);

			string ResolveVideoPattern()
			{
				var ctRaw = (channel.ChannelType ?? "").Trim().ToLowerInvariant();
				return ctRaw switch
				{
					"daily" => naming.DailyVideoFormat,
					"episodic" => naming.EpisodicVideoFormat,
					"streaming" => naming.StreamingVideoFormat,
					_ => naming.StandardVideoFormat
				};
			}

			var videoPattern = ResolveVideoPattern();
			var patternForTokens = videoPattern ?? string.Empty;
			var needsPlaylistNumber = patternForTokens.Contains("{Playlist Number", StringComparison.OrdinalIgnoreCase);
			var needsPlaylistIndex = patternForTokens.Contains("{Playlist Index", StringComparison.OrdinalIgnoreCase);

			static string NormalizeRel(string rel) => rel.Replace('\\', '/');

			var candidates = new List<(VideoFileEntity Vf, VideoEntity Video, int? PrimaryPlaylistId, PlaylistEntity? Playlist, int? PlaylistNumber, int? SeasonNumber, string OutputDir, string Ext, string ExistingRel)>();
			foreach (var vf in videoFiles)
			{
				if (string.IsNullOrWhiteSpace(vf.Path) || !File.Exists(vf.Path))
					continue;
				if (!videos.TryGetValue(vf.VideoId, out var video))
					continue;

				var primaryPlaylistId = primaryPlaylistByVideoId.GetValueOrDefault(video.Id);
				PlaylistEntity? playlist = null;
				if (primaryPlaylistId.HasValue && playlistById.TryGetValue(primaryPlaylistId.Value, out var pl))
					playlist = pl;

				int? playlistNumberToken = null;
				int? seasonNumber = null;
				if (needsPlaylistNumber || (channel.PlaylistFolder == true && useCustomNfos))
				{
					var (sn, _) = await NfoLibraryExporter.ResolveSeasonNumberForPlaylistFolderAsync(db, channelId, video, primaryPlaylistId, ct);
					if (needsPlaylistNumber)
						playlistNumberToken = sn;
					if (channel.PlaylistFolder == true && useCustomNfos)
						seasonNumber = sn;
				}

				var outputDir = DownloadQueueProcessor.GetOutputDirectory(
					channel,
					video,
					playlist,
					naming,
					roots,
					useCustomNfos,
					seasonNumber);
				if (string.IsNullOrWhiteSpace(outputDir))
					continue;

				var ext = Path.GetExtension(vf.Path);

				var existingRel = !string.IsNullOrWhiteSpace(vf.RelativePath)
					? vf.RelativePath!
					: NormalizeRel(Path.GetRelativePath(showRoot, vf.Path));
				existingRel = NormalizeRel(existingRel);

				candidates.Add((vf, video, primaryPlaylistId, playlist, playlistNumberToken, seasonNumber, outputDir, ext, existingRel));
			}

			var resolvePlaylistIndex = needsPlaylistIndex || channel.PlaylistFolder == true;

			Dictionary<int, int>? customIndexByVideoId = null;
			if (resolvePlaylistIndex)
			{
				var customGroups = candidates
					.Where(x => x.PlaylistNumber is >= (NfoLibraryExporter.CustomPlaylistSeasonRangeStart + 1))
					.GroupBy(x => x.PlaylistNumber!.Value)
					.ToList();

				if (customGroups.Count > 0)
				{
					customIndexByVideoId = new Dictionary<int, int>();
					foreach (var g in customGroups)
					{
						var ordered = g
							.OrderBy(x => x.Video.UploadDateUtc)
							.ThenBy(x => x.Video.Id)
							.ToList();
						for (var i = 0; i < ordered.Count; i++)
						{
							customIndexByVideoId[ordered[i].Video.Id] = i + 1;
						}
					}
				}
			}

			var destOwnerByFullPath = await VideoFileRenameCollision.LoadPathOwnerByFullPathForRenameScopeAsync(
				db,
				channelId,
				channel.RootFolderPath,
				ct);

			var reservedRenameTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			var items = new List<object>();
			foreach (var c in candidates)
			{
				int? playlistIndexToken = null;
				if (resolvePlaylistIndex)
				{
					if (customIndexByVideoId is not null && customIndexByVideoId.TryGetValue(c.Video.Id, out var customIndex))
					{
						playlistIndexToken = customIndex;
					}
					else
					{
						var n = await NfoPlaylistEpisodeResolver.ResolveEpisodeNumberAsync(db, c.PrimaryPlaylistId, c.Video.Id, ct);
						playlistIndexToken = n;
					}
				}

				var context = new VideoFileNaming.NamingContext(
					Channel: channel,
					Playlist: c.Playlist,
					Video: c.Video,
					PlaylistIndex: playlistIndexToken,
					QualityFull: null,
					Resolution: null,
					Extension: c.Ext,
					PlaylistNumber: c.PlaylistNumber);
				var newFileName = VideoFileNaming.BuildFileName(videoPattern ?? string.Empty, context, naming);
				if (string.IsNullOrWhiteSpace(newFileName))
					continue;

				var targetFull = Path.Combine(c.OutputDir, newFileName + c.Ext);
				var newRel = NormalizeRel(Path.GetRelativePath(showRoot, targetFull));

				var srcFull = VideoFileRenameCollision.SafeFullPath(c.Vf.Path!);
				var destFull = VideoFileRenameCollision.SafeFullPath(targetFull);
				var sameInPlace = string.Equals(srcFull, destFull,
					OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
				string[]? collisionReasons = null;
				bool collision;
				if (sameInPlace)
				{
					collision = false;
				}
				else
				{
					var (onDisk, dbOther, batchDup) = VideoFileRenameCollision.EvaluateBlocking(
						targetFull,
						destFull,
						c.Vf.Id,
						destOwnerByFullPath,
						reservedRenameTargets);
					collision = onDisk || dbOther || batchDup;
					if (collision)
					{
						var reasons = new List<string>(3);
						if (onDisk)
							reasons.Add("onDisk");
						if (dbOther)
							reasons.Add("dbPathOwnedByOther");
						if (batchDup)
							reasons.Add("batchDuplicate");
						collisionReasons = reasons.ToArray();
					}
				}

				reservedRenameTargets.Add(destFull);

				items.Add(new
				{
					videoFileId = c.Vf.Id,
					existingPath = c.ExistingRel,
					newPath = newRel,
					collision,
					collisionReasons,
					safeToApply = !collision
				});
			}

			return Results.Json(items.ToArray());
		});

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
							.Select(v => new { v.Id, v.ChannelId, v.YoutubeVideoId })
							.ToListAsync();
						var primaryPlaylistByVideoId = await ChannelDtoMapper.LoadPrimaryPlaylistIdByVideoIdsForChannelAsync(
							db,
							channelId.Value,
							channelVideos.Select(v => v.Id).ToList(),
							CancellationToken.None);
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

						var existingRows = await db.VideoFiles.Where(vf => vf.ChannelId == channelId.Value).ToListAsync();
						var existingByFullPath = existingRows
							.Where(r => !string.IsNullOrWhiteSpace(r.Path))
							.ToDictionary(r => Path.GetFullPath(r.Path), r => r, StringComparer.OrdinalIgnoreCase);

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

									var fullPath = Path.GetFullPath(filePath);
									if (existingByFullPath.TryGetValue(fullPath, out var existing))
									{
										var fi0 = new FileInfo(fullPath);
										var relative0 = Path.GetRelativePath(root, fullPath);
										foundByVideoId[existing.VideoId] = (fullPath, relative0, fi0.Exists ? fi0.Length : 0, existing.ChannelId, existing.PlaylistId);
										continue;
									}

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

									var fi = new FileInfo(fullPath);
									var relativePath = Path.GetRelativePath(root, fullPath);
									foundByVideoId[video.Id] = (fullPath, relativePath, fi.Exists ? fi.Length : 0, video.ChannelId, primaryPlaylistByVideoId.GetValueOrDefault(video.Id));
								}
							}
							catch
							{
								// best-effort scan
							}
						}

						foreach (var row in existingRows)
						{
							if (!foundByVideoId.TryGetValue(row.VideoId, out var found))
							{
								// Keep existing mappings; do not drop rows just because the current scan
								// can't re-identify the file by filename tokens.
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
				var customPlRows = channelIds.Count == 0
					? new List<ChannelCustomPlaylistEntity>()
					: await db.ChannelCustomPlaylists.AsNoTracking()
						.Where(c => channelIds.Contains(c.ChannelId))
						.ToListAsync(ct);
				var customByChannelId = customPlRows
					.GroupBy(c => c.ChannelId)
					.ToDictionary(g => g.Key, g => (IReadOnlyList<ChannelCustomPlaylistEntity>)g.OrderBy(x => x.Priority).ThenBy(x => x.Id).ToList());
				var mergedYoutubePlaylistIdToNumber = new Dictionary<int, int>();
				foreach (var group in playlistRows.GroupBy(p => p.ChannelId))
				{
					var ordered = ChannelDtoMapper.OrderPlaylistsByLatestUpload(group.ToList(), maxUploadByPlaylist);
					var customFor = customByChannelId.TryGetValue(group.Key, out var cp) ? cp : Array.Empty<ChannelCustomPlaylistEntity>();
					var (ytMap, _) = ChannelDtoMapper.BuildMergedCuratedPlaylistNumberMaps(ordered, customFor);
					foreach (var kv in ytMap)
						mergedYoutubePlaylistIdToNumber[kv.Key] = kv.Value;
				}

				var dq = DefaultQuality();
				var results = rows.Select(vf => VideoFileDtoMapper.ToDto(
					vf,
					vf.PlaylistId.HasValue && mergedYoutubePlaylistIdToNumber.TryGetValue(vf.PlaylistId.Value, out var pn) ? pn : 1,
					dq));

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

			return Results.Json(VideoFileDtoMapper.ToDto(file, playlistNumber, DefaultQuality()));
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

