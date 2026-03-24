using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class ProgramDbQueueHelpers
{
	public static int MoveCompletedQueueItemsToHistoryBatched(
		TubeArrDbContext db,
		int batchSize = 250,
		ILogger? logger = null)
	{
		const int Completed = 2;
		var totalMoved = 0;

		while (true)
		{
			var completedItems = db.DownloadQueue
				.Where(q => q.Status == Completed)
				.OrderBy(q => q.Id)
				.Take(batchSize)
				.ToList();

			if (completedItems.Count == 0)
			{
				break;
			}

			var queueIds = completedItems.Select(x => x.Id).ToList();
			var downloadIds = queueIds.Select(x => x.ToString()).ToList();
			var videoIds = completedItems.Select(x => x.VideoId).Distinct().ToList();

			var videos = videoIds.Count == 0
				? new Dictionary<int, VideoEntity>()
				: db.Videos
					.AsNoTracking()
					.Where(v => videoIds.Contains(v.Id))
					.ToDictionary(v => v.Id);

			var existingDownloadIds = new HashSet<string>(
				db.DownloadHistory
					.AsNoTracking()
					.Where(h => h.DownloadId != null && downloadIds.Contains(h.DownloadId))
					.Select(h => h.DownloadId!)
					.ToList(),
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
					PlaylistId = video?.PlaylistId,
					EventType = 3,
					SourceTitle = video?.Title ?? $"Video {item.VideoId}",
					OutputPath = item.OutputPath,
					Message = item.ErrorMessage,
					DownloadId = downloadId,
					Date = item.CompletedAt ?? item.QueuedAt
				});
			}

			if (historyRows.Count > 0)
			{
				db.DownloadHistory.AddRange(historyRows);
			}

			db.DownloadQueue.RemoveRange(completedItems);
			db.SaveChanges();

			totalMoved += completedItems.Count;
			logger?.LogInformation(
				"Moved {BatchCount} completed queue items to history in batch (running total: {TotalMoved}).",
				completedItems.Count,
				totalMoved);
		}

		return totalMoved;
	}
}

