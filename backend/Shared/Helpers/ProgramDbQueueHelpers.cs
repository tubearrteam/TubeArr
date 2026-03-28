using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class ProgramDbQueueHelpers
{
	public static int MoveCompletedQueueItemsToHistoryBatched(
		TubeArrDbContext db,
		int batchSize = 250,
		ILogger? logger = null)
	{
		return MoveCompletedQueueItemsToHistoryBatchedAsync(db, batchSize, logger, CancellationToken.None)
			.GetAwaiter()
			.GetResult();
	}

	public static async Task<int> MoveCompletedQueueItemsToHistoryBatchedAsync(
		TubeArrDbContext db,
		int batchSize = 250,
		ILogger? logger = null,
		CancellationToken cancellationToken = default)
	{
		var totalMoved = 0;

		while (true)
		{
			var completedItems = await db.DownloadQueue
				.Where(q => q.Status == QueueJobStatuses.Completed)
				.OrderBy(q => q.Id)
				.Take(batchSize)
				.ToListAsync(cancellationToken);

			if (completedItems.Count == 0)
			{
				break;
			}

			var queueIds = completedItems.Select(x => x.Id).ToList();
			var downloadIds = queueIds.Select(x => x.ToString()).ToList();
			var videoIds = completedItems.Select(x => x.VideoId).Distinct().ToList();

			var videos = videoIds.Count == 0
				? new Dictionary<int, VideoEntity>()
				: await db.Videos
					.AsNoTracking()
					.Where(v => videoIds.Contains(v.Id))
					.ToDictionaryAsync(v => v.Id, cancellationToken);

			var primaryByVideoId = new Dictionary<int, int?>();
			foreach (var g in videos.Values.GroupBy(v => v.ChannelId))
			{
				var map = await ChannelDtoMapper.LoadPrimaryPlaylistIdByVideoIdsForChannelAsync(
					db,
					g.Key,
					g.Select(v => v.Id).ToList(),
					cancellationToken);
				foreach (var kv in map)
					primaryByVideoId[kv.Key] = kv.Value;
			}

			var existingDownloadIds = new HashSet<string>(
				await db.DownloadHistory
					.AsNoTracking()
					.Where(h => h.DownloadId != null && downloadIds.Contains(h.DownloadId))
					.Select(h => h.DownloadId!)
					.ToListAsync(cancellationToken),
				StringComparer.OrdinalIgnoreCase);

			var historyRows = new List<DownloadHistoryEntity>();
			foreach (var item in completedItems)
			{
				var downloadId = item.Id.ToString();
				if (existingDownloadIds.Contains(downloadId))
				{
					continue;
				}

				videos.TryGetValue(item.VideoId, out var video);
				historyRows.Add(new DownloadHistoryEntity
				{
					ChannelId = item.ChannelId,
					VideoId = item.VideoId,
					PlaylistId = primaryByVideoId.GetValueOrDefault(item.VideoId),
					EventType = 3,
					SourceTitle = video?.Title ?? $"Video {item.VideoId}",
					OutputPath = item.OutputPath,
					Message = item.LastError,
					DownloadId = downloadId,
					Date = (item.EndedAtUtc ?? item.QueuedAtUtc).UtcDateTime
				});
			}

			if (historyRows.Count > 0)
			{
				db.DownloadHistory.AddRange(historyRows);
			}

			db.DownloadQueue.RemoveRange(completedItems);
			await db.SaveChangesAsync(cancellationToken);

			totalMoved += completedItems.Count;
			logger?.LogInformation(
				"Moved {BatchCount} completed queue items to history in batch (running total: {TotalMoved}).",
				completedItems.Count,
				totalMoved);
		}

		return totalMoved;
	}
}
