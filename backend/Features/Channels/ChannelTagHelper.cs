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

	/// <summary>Merge tag ids for one channel: <paramref name="applyMode"/> is <c>add</c>, <c>remove</c>, or <c>replace</c> (case-insensitive).</summary>
	internal static async Task MergeChannelTagsAsync(TubeArrDbContext db, int channelId, int[]? tagIds, string applyMode, CancellationToken ct)
	{
		var mode = (applyMode ?? "add").Trim();
		var ids = tagIds?.Distinct().ToArray() ?? Array.Empty<int>();
		var rows = await db.ChannelTags.Where(x => x.ChannelId == channelId).ToListAsync(ct);

		if (string.Equals(mode, "replace", StringComparison.OrdinalIgnoreCase))
		{
			db.ChannelTags.RemoveRange(rows);
			foreach (var t in ids.OrderBy(x => x))
				db.ChannelTags.Add(new ChannelTagEntity { ChannelId = channelId, TagId = t });
			return;
		}

		if (string.Equals(mode, "remove", StringComparison.OrdinalIgnoreCase))
		{
			foreach (var r in rows.Where(r => ids.Contains(r.TagId)))
				db.ChannelTags.Remove(r);
			return;
		}

		var have = rows.Select(r => r.TagId).ToHashSet();
		foreach (var t in ids.Where(t => !have.Contains(t)))
			db.ChannelTags.Add(new ChannelTagEntity { ChannelId = channelId, TagId = t });
	}
}
