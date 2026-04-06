using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class ChannelTagHelper
{
	internal static async Task ReplaceChannelTagsAsync(TubeArrDbContext db, int channelId, int[]? tagIds, CancellationToken ct)
	{
		var old = await db.ChannelTags.Where(x => x.ChannelId == channelId).ToListAsync(ct);
		db.ChannelTags.RemoveRange(old);
		if (tagIds is not { Length: > 0 })
			return;

		foreach (var id in tagIds.Distinct().OrderBy(x => x))
			db.ChannelTags.Add(new ChannelTagEntity { ChannelId = channelId, TagId = id });
	}

	internal static async Task<int[]> LoadTagIdsForChannelAsync(TubeArrDbContext db, int channelId, CancellationToken ct) =>
		await db.ChannelTags.AsNoTracking()
			.Where(x => x.ChannelId == channelId)
			.OrderBy(x => x.TagId)
			.Select(x => x.TagId)
			.ToArrayAsync(ct);
}
