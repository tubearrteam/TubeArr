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

		return new LibraryImportFolderDto(
			folderName,
			folderFullPath,
			relativePath,
			null,
			null,
			false,
			null,
			lastReason ?? "Could not resolve channel from folder name or sample files.");
	}
}
