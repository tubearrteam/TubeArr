using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

/// <summary>Shared rename collision checks for preview and execution paths.</summary>
internal static class VideoFileRenameCollision
{
	internal static string SafeFullPath(string p)
	{
		try
		{
			return Path.GetFullPath(p);
		}
		catch
		{
			return (p ?? "").Trim();
		}
	}

	/// <summary>
	/// Maps normalized full path to owning video file id (first row wins for duplicates).
	/// When <paramref name="channelRootFolderPath"/> is set, includes all channels on that root; otherwise only <paramref name="channelId"/>.
	/// </summary>
	internal static async Task<Dictionary<string, int>> LoadPathOwnerByFullPathForRenameScopeAsync(
		TubeArrDbContext db,
		int channelId,
		string? channelRootFolderPath,
		CancellationToken ct)
	{
		var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		List<(int Id, string Path)> rows;
		if (!string.IsNullOrWhiteSpace(channelRootFolderPath))
		{
			var root = channelRootFolderPath.Trim();
			var joined = await (
				from vf in db.VideoFiles.AsNoTracking()
				join ch in db.Channels.AsNoTracking() on vf.ChannelId equals ch.Id
				where vf.Path != null && vf.Path != ""
					&& ch.RootFolderPath != null
					&& ch.RootFolderPath == root
				select new { vf.Id, vf.Path }
			).ToListAsync(ct);
			rows = joined.ConvertAll(x => (x.Id, x.Path!));
		}
		else
		{
			var channelRows = await db.VideoFiles.AsNoTracking()
				.Where(vf => vf.ChannelId == channelId && vf.Path != null && vf.Path != "")
				.Select(vf => new { vf.Id, vf.Path })
				.ToListAsync(ct);
			rows = channelRows.ConvertAll(x => (x.Id, x.Path!));
		}

		foreach (var (id, path) in rows)
		{
			try
			{
				var fp = Path.GetFullPath(path);
				if (!map.ContainsKey(fp))
					map[fp] = id;
			}
			catch
			{
				// ignore invalid paths
			}
		}

		return map;
	}

	/// <summary>Call only when source and destination are already known to differ.</summary>
	internal static (bool OnDisk, bool DbOther, bool BatchDup) EvaluateBlocking(
		string destinationPath,
		string destFull,
		int videoFileId,
		Dictionary<string, int> pathOwnerByFullPath,
		HashSet<string> batchDestinationFullPaths)
	{
		pathOwnerByFullPath.TryGetValue(destFull, out var destOwnerId);
		var onDisk = File.Exists(destinationPath);
		var dbOther = destOwnerId != 0 && destOwnerId != videoFileId;
		var batchDup = batchDestinationFullPaths.Contains(destFull);
		return (onDisk, dbOther, batchDup);
	}
}
