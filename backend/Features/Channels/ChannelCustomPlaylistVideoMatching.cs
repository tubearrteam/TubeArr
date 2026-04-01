using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class ChannelCustomPlaylistVideoMatching
{
	internal static CustomPlaylistVideoContext BuildVideoContext(
		VideoEntity video,
		int? primaryPlaylistId,
		IReadOnlyCollection<int> allPlaylistIdsForVideo,
		IReadOnlyDictionary<int, PlaylistEntity> playlistById)
	{
		var primaryPl = primaryPlaylistId.HasValue && playlistById.TryGetValue(primaryPlaylistId.Value, out var pp) ? pp : null;
		var allIds = new List<string>();
		var allNames = new List<string>();
		foreach (var pid in allPlaylistIdsForVideo)
		{
			if (!playlistById.TryGetValue(pid, out var pl))
				continue;
			allIds.Add(pl.YoutubePlaylistId ?? "");
			if (!string.IsNullOrWhiteSpace(pl.Title))
				allNames.Add(pl.Title);
		}

		return new CustomPlaylistVideoContext(
			Title: video.Title ?? "",
			Description: video.Description,
			PrimarySourcePlaylistYoutubeId: primaryPl?.YoutubePlaylistId,
			PrimarySourcePlaylistName: primaryPl?.Title,
			AllSourcePlaylistYoutubeIds: allIds,
			AllSourcePlaylistNames: allNames,
			PublishedAtUtc: video.UploadDateUtc,
			DurationSeconds: video.Runtime);
	}

	internal static int[] ComputeCustomPlaylistNumbers(
		VideoEntity video,
		int? primaryPlaylistId,
		IReadOnlyCollection<int> allPlaylistIdsForVideo,
		IReadOnlyDictionary<int, PlaylistEntity> playlistById,
		IReadOnlyList<ChannelCustomPlaylistEntity> customOrdered,
		IReadOnlyDictionary<int, int> customPlaylistIdToPlaylistNumber)
	{
		var ctx = BuildVideoContext(video, primaryPlaylistId, allPlaylistIdsForVideo, playlistById);

		var nums = new List<int>();
		foreach (var c in customOrdered)
		{
			if (!c.Enabled)
				continue;
			if (!customPlaylistIdToPlaylistNumber.TryGetValue(c.Id, out var plNum))
				continue;
			var rules = ChannelCustomPlaylistRulesHelper.ParseRules(c.RulesJson);
			var mt = ChannelCustomPlaylistRulesHelper.NormalizeMatchType(c.MatchType);
			if (ChannelCustomPlaylistEvaluator.Matches(rules, mt, ctx))
				nums.Add(plNum);
		}

		nums.Sort();
		return nums.ToArray();
	}
}
