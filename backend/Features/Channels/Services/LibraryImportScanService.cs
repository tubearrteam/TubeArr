using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

public sealed class LibraryImportScanService
{
	readonly ChannelResolveService _channelResolve;
	readonly ILogger<LibraryImportScanService> _logger;

	public LibraryImportScanService(ChannelResolveService channelResolve, ILogger<LibraryImportScanService> logger)
	{
		_channelResolve = channelResolve;
		_logger = logger;
	}

	/// <summary>Single root folder row plus unmapped immediate child folders and best-effort channel resolve preview.</summary>
	public async Task<RootFolderDetailDto?> BuildRootFolderDetailAsync(int rootFolderId, TubeArrDbContext db, CancellationToken ct, int perResolveTimeoutMs = 120_000)
	{
		var entity = await db.RootFolders.AsNoTracking().FirstOrDefaultAsync(r => r.Id == rootFolderId, ct);
		if (entity is null)
			return null;

		var naming = await db.NamingConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct) ?? new NamingConfigEntity { Id = 1 };
		var channels = await db.Channels.AsNoTracking().OrderBy(c => c.Id).ToListAsync(ct);
		var configuredRootCount = await db.RootFolders.AsNoTracking().CountAsync(ct);
		var rootPath = (entity.Path ?? "").Trim();
		var (accessible, freeSpace) = RootFolderPathProbe.GetStats(entity.Path);

		var unmapped = Array.Empty<LibraryImportFolderDto>();
		if (accessible && !string.IsNullOrWhiteSpace(rootPath))
		{
			try
			{
				var fullRoot = Path.GetFullPath(rootPath);
				if (Directory.Exists(fullRoot))
				{
					var mapped = LibraryImportFolderCandidateExtractor.BuildMappedNormalizedPaths(entity, channels, naming, configuredRootCount);
					var list = new List<LibraryImportFolderDto>();
					foreach (var sub in Directory.EnumerateDirectories(fullRoot))
					{
						string name;
						try
						{
							name = Path.GetFileName(sub.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
						}
						catch
						{
							continue;
						}

						if (string.IsNullOrWhiteSpace(name))
							continue;

						string fullSub;
						try
						{
							fullSub = Path.GetFullPath(sub);
						}
						catch
						{
							continue;
						}

						if (mapped.Contains(fullSub))
							continue;

						string rel;
						try
						{
							rel = Path.GetRelativePath(fullRoot, fullSub);
						}
						catch
						{
							rel = name;
						}

						var dto = await TryResolveFolderAsync(fullSub, name, rel, db, ct, perResolveTimeoutMs);
						list.Add(dto);
					}

					unmapped = list.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Library import scan failed for rootFolder id={RootFolderId} path={Path}", rootFolderId, rootPath);
			}
		}

		return new RootFolderDetailDto(entity.Id, entity.Path ?? "", accessible, freeSpace, unmapped);
	}

	async Task<LibraryImportFolderDto> TryResolveFolderAsync(
		string folderFullPath,
		string folderName,
		string relativePath,
		TubeArrDbContext db,
		CancellationToken ct,
		int perResolveTimeoutMs)
	{
		var candidates = LibraryImportFolderCandidateExtractor.CollectCandidates(folderFullPath, folderName);
		string? lastReason = null;

		foreach (var candidate in candidates)
		{
			try
			{
				var (result, status) = await _channelResolve.ResolveAsync(candidate, db, _logger, ct, perResolveTimeoutMs);
				if (status is >= 400 && status != 503 && status != 504)
					lastReason = result.FailureReason ?? $"HTTP {status}";

				if (result.Success && result.Items is { Length: > 0 })
				{
					return new LibraryImportFolderDto(
						folderName,
						folderFullPath,
						relativePath,
						candidate,
						result.ResolutionMethod,
						true,
						result.Items[0],
						null);
				}

				if (!string.IsNullOrWhiteSpace(result.FailureReason))
					lastReason = result.FailureReason;
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				_logger.LogDebug(ex, "Library import resolve candidate failed folder={Folder} candidate={Candidate}", folderName, candidate);
				lastReason = ex.Message;
			}
		}

		var suggestions = await BuildVideoMappingSuggestionsAsync(folderFullPath, db, ct);
		return new LibraryImportFolderDto(
			folderName,
			folderFullPath,
			relativePath,
			null,
			null,
			false,
			null,
			lastReason ?? "Could not resolve channel from folder name or sample files.",
			suggestions.Count > 0 ? suggestions : null);
	}

	static async Task<IReadOnlyList<VideoFileMappingSuggestionDto>> BuildVideoMappingSuggestionsAsync(
		string folderFullPath,
		TubeArrDbContext db,
		CancellationToken ct)
	{
		var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var file in EnumerateMediaFilesUnderFolder(folderFullPath, maxFiles: 120, maxDepth: 4))
			UnmappedVideoFileMappingRunner.CollectCandidateYoutubeVideoIds(file, ids);

		if (ids.Count == 0)
			return Array.Empty<VideoFileMappingSuggestionDto>();

		var idList = ids.ToList();
		var rows = await db.Videos.AsNoTracking()
			.Where(v => v.YoutubeVideoId != null && idList.Contains(v.YoutubeVideoId))
			.Join(
				db.Channels.AsNoTracking(),
				v => v.ChannelId,
				c => c.Id,
				(v, c) => new VideoFileMappingSuggestionDto(
					v.Id,
					v.YoutubeVideoId ?? "",
					v.Title ?? "",
					c.Id,
					c.Title ?? ""))
			.OrderBy(x => x.ChannelTitle)
			.ThenBy(x => x.VideoTitle)
			.Take(25)
			.ToListAsync(ct);

		return rows;
	}

	static IEnumerable<string> EnumerateMediaFilesUnderFolder(string root, int maxFiles, int maxDepth)
	{
		if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
			yield break;

		static bool IsMedia(string path)
		{
			var ext = Path.GetExtension(path);
			return ext.Equals(".mp4", StringComparison.OrdinalIgnoreCase)
			       || ext.Equals(".mkv", StringComparison.OrdinalIgnoreCase)
			       || ext.Equals(".webm", StringComparison.OrdinalIgnoreCase)
			       || ext.Equals(".m4v", StringComparison.OrdinalIgnoreCase)
			       || ext.Equals(".mov", StringComparison.OrdinalIgnoreCase);
		}

		var count = 0;
		var q = new Queue<(string Path, int Depth)>();
		q.Enqueue((Path.GetFullPath(root), 0));
		while (q.Count > 0 && count < maxFiles)
		{
			var (dir, depth) = q.Dequeue();
			IEnumerable<string> files;
			try
			{
				files = Directory.EnumerateFiles(dir);
			}
			catch
			{
				continue;
			}

			foreach (var f in files)
			{
				if (!IsMedia(f))
					continue;
				yield return f;
				count++;
				if (count >= maxFiles)
					yield break;
			}

			if (count >= maxFiles || depth >= maxDepth)
				continue;

			try
			{
				foreach (var sub in Directory.EnumerateDirectories(dir))
					q.Enqueue((sub, depth + 1));
			}
			catch
			{
				// ignore
			}
		}
	}
}
