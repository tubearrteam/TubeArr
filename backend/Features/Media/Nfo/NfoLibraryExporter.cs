using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Data;
using TubeArr.Backend.Media;
using TubeArr.Backend.Media.Nfo;

namespace TubeArr.Backend;

/// <summary>Writes tvshow/season/episode NFOs next to TubeArr download output for Plex/Kodi.</summary>
internal static class NfoLibraryExporter
{
	/// <summary>Custom (rule-based) playlists use <c>Season 10001</c>, <c>Season 10002</c>, … so they never collide with native curated playlists (Season 02–…).</summary>
	internal const int CustomPlaylistSeasonRangeStart = 10000;

	/// <summary>Folder name under the show root for a curated playlist when custom NFOs are enabled (<c>Season 01</c>, <c>Season 02</c>, …).</summary>
	internal static string FormatSeasonPlaylistFolderName(int seasonNumber) =>
		"Season " + seasonNumber.ToString("D2", CultureInfo.InvariantCulture);

	internal readonly record struct ExpectedNfoSet(
		string TvShowNfoPath,
		string TvShowXml,
		string? SeasonNfoPath,
		string? SeasonXml,
		string EpisodeNfoPath,
		string EpisodeXml);

	internal static async Task WriteForCompletedDownloadAsync(
		TubeArrDbContext db,
		ChannelEntity channel,
		VideoEntity video,
		PlaylistEntity? playlist,
		int? primaryPlaylistId,
		string mediaFilePath,
		NamingConfigEntity naming,
		List<RootFolderEntity> rootFolders,
		CancellationToken ct)
	{
		var set = await TryBuildExpectedNfoSetAsync(
			db,
			channel,
			video,
			playlist,
			primaryPlaylistId,
			mediaFilePath,
			naming,
			rootFolders,
			ct);
		if (set is null)
			return;

		await WriteExpectedNfoSetAsync(set.Value, rootFolders, ct);
	}

	internal static async Task<ExpectedNfoSet?> TryBuildExpectedNfoSetAsync(
		TubeArrDbContext db,
		ChannelEntity channel,
		VideoEntity video,
		PlaylistEntity? playlist,
		int? primaryPlaylistId,
		string mediaFilePath,
		NamingConfigEntity naming,
		List<RootFolderEntity> rootFolders,
		CancellationToken ct)
	{
		var showRoot = DownloadQueueProcessor.GetChannelShowRootPath(channel, video, naming, rootFolders);
		if (string.IsNullOrWhiteSpace(showRoot))
			return null;

		var minByChannel = await ChannelDtoMapper.LoadMinUploadUtcByChannelIdsAsync(db, [channel.Id], ct);
		int? channelYear = null;
		if (minByChannel.TryGetValue(channel.Id, out var minCh) && NfoXmlText.TryGetCalendarYear(minCh, out var yCh))
			channelYear = yCh;

		var plotChannel = NfoXmlText.NormalizeOptionalPlot(channel.Description);
		var tvShowXml = NfoWriter.BuildTvShowDocument(
			new TvShowNfoContent(Title: channel.Title ?? "", Year: channelYear, Plot: plotChannel));
		var tvShowPath = Path.Combine(showRoot, "tvshow.nfo");

		var (seasonNumber, customSeasonFolder) = await ResolveSeasonNumberForPlaylistFolderAsync(db, channel.Id, video, primaryPlaylistId, ct);

		string? seasonPath = null;
		string? seasonXml = null;
		if (channel.PlaylistFolder == true && (playlist is not null || customSeasonFolder is not null))
		{
			var seasonDir = Path.Combine(showRoot, FormatSeasonPlaylistFolderName(seasonNumber));
			int? playlistYear = null;
			if (playlist is not null)
			{
				var minByPl = await ChannelDtoMapper.LoadMinUploadUtcByPlaylistIdsAsync(db, [playlist.Id], ct);
				if (minByPl.TryGetValue(playlist.Id, out var minPl) && NfoXmlText.TryGetCalendarYear(minPl, out var yPl))
					playlistYear = yPl;
			}
			else if (customSeasonFolder is not null && NfoXmlText.TryGetCalendarYear(video.UploadDateUtc, out var yVid))
				playlistYear = yVid;

			var seasonTitle = customSeasonFolder is not null
				? (string.IsNullOrWhiteSpace(customSeasonFolder.Name)
					? "Season " + seasonNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)
					: customSeasonFolder.Name)
				: (string.IsNullOrWhiteSpace(playlist!.Title)
					? "Season " + seasonNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)
					: playlist.Title);

			seasonPath = Path.Combine(seasonDir, "season.nfo");
			seasonXml = NfoWriter.BuildSeasonDocument(
				new SeasonNfoContent(SeasonNumber: seasonNumber, Title: seasonTitle, Year: playlistYear));
		}

		var episodeNumber = await NfoPlaylistEpisodeResolver.ResolveEpisodeNumberAsync(db, primaryPlaylistId, video.Id, ct);

		var plotEpisode = NfoXmlText.NormalizeOptionalPlot(video.Description);
		string? aired = null;
		var airedRaw = NfoXmlText.FormatAiredDate(video.UploadDateUtc);
		if (!string.IsNullOrEmpty(airedRaw))
			aired = airedRaw;

		var mediaDir = Path.GetDirectoryName(mediaFilePath);
		if (string.IsNullOrEmpty(mediaDir))
			return null;
		var baseName = Path.GetFileNameWithoutExtension(mediaFilePath);
		var episodePath = Path.Combine(mediaDir, baseName + ".nfo");
		var episodeXml = NfoWriter.BuildEpisodeDocument(
			new EpisodeNfoContent(
				Title: video.Title ?? "",
				Season: seasonNumber,
				Episode: episodeNumber,
				Plot: plotEpisode,
				Aired: aired));

		return new ExpectedNfoSet(tvShowPath, tvShowXml, seasonPath, seasonXml, episodePath, episodeXml);
	}

	static async Task WriteExpectedNfoSetAsync(ExpectedNfoSet set, List<RootFolderEntity> rootFolders, CancellationToken ct)
	{
		var showRoot = Path.GetDirectoryName(set.TvShowNfoPath);
		if (string.IsNullOrEmpty(showRoot))
			return;

		Directory.CreateDirectory(showRoot);

		if (TubeArrManagedLibraryManifest.CanWriteManagedNfo(rootFolders, set.TvShowNfoPath))
		{
			await File.WriteAllTextAsync(set.TvShowNfoPath, set.TvShowXml, NfoXmlText.Utf8Encoding, ct);
			TubeArrManagedLibraryManifest.RegisterManagedAsset(rootFolders, set.TvShowNfoPath, TubeArrManagedLibraryManifest.KindNfo);
		}

		if (set.SeasonNfoPath is not null && set.SeasonXml is not null
			&& TubeArrManagedLibraryManifest.CanWriteManagedNfo(rootFolders, set.SeasonNfoPath))
		{
			var sd = Path.GetDirectoryName(set.SeasonNfoPath);
			if (!string.IsNullOrEmpty(sd))
				Directory.CreateDirectory(sd);
			await File.WriteAllTextAsync(set.SeasonNfoPath, set.SeasonXml, NfoXmlText.Utf8Encoding, ct);
			TubeArrManagedLibraryManifest.RegisterManagedAsset(rootFolders, set.SeasonNfoPath, TubeArrManagedLibraryManifest.KindNfo);
		}

		if (TubeArrManagedLibraryManifest.CanWriteManagedNfo(rootFolders, set.EpisodeNfoPath))
		{
			var epDir = Path.GetDirectoryName(set.EpisodeNfoPath);
			if (!string.IsNullOrEmpty(epDir))
				Directory.CreateDirectory(epDir);
			await File.WriteAllTextAsync(set.EpisodeNfoPath, set.EpisodeXml, NfoXmlText.Utf8Encoding, ct);
			TubeArrManagedLibraryManifest.RegisterManagedAsset(rootFolders, set.EpisodeNfoPath, TubeArrManagedLibraryManifest.KindNfo);
		}
	}

	/// <summary>
	/// Season folder index for Plex/Kodi layout: native YouTube playlists use 2,3,4,… (existing rules).
	/// Rule-based custom playlists use <see cref="CustomPlaylistSeasonRangeStart"/>+1, +2, … by stable row order so they never share folders with native lists.
	/// </summary>
	internal static async Task<(int SeasonNumber, ChannelCustomPlaylistEntity? CustomFolderSource)> ResolveSeasonNumberForPlaylistFolderAsync(
		TubeArrDbContext db,
		int channelId,
		VideoEntity video,
		int? primaryPlaylistId,
		CancellationToken ct)
	{
		var customOrdered = await db.ChannelCustomPlaylists.AsNoTracking()
			.Where(c => c.ChannelId == channelId)
			.OrderBy(c => c.Priority).ThenBy(c => c.Id)
			.ToListAsync(ct);

		if (customOrdered.Count > 0)
		{
			var playlistsInChannel = await db.Playlists.AsNoTracking()
				.Where(p => p.ChannelId == channelId)
				.ToListAsync(ct);
			var playlistById = playlistsInChannel.ToDictionary(p => p.Id);
			var pvIds = await db.PlaylistVideos.AsNoTracking()
				.Where(pv => pv.VideoId == video.Id)
				.Select(pv => pv.PlaylistId)
				.ToListAsync(ct);

			var ctx = ChannelCustomPlaylistVideoMatching.BuildVideoContext(video, primaryPlaylistId, pvIds, playlistById);

			for (var i = 0; i < customOrdered.Count; i++)
			{
				var c = customOrdered[i];
				if (!c.Enabled)
					continue;
				var rules = ChannelCustomPlaylistRulesHelper.ParseRules(c.RulesJson);
				var mt = ChannelCustomPlaylistRulesHelper.NormalizeMatchType(c.MatchType);
				if (ChannelCustomPlaylistEvaluator.Matches(rules, mt, ctx))
					return (CustomPlaylistSeasonRangeStart + 1 + i, c);
			}
		}

		var yt = await ResolveYoutubeCuratedSeasonNumberAsync(db, channelId, primaryPlaylistId, ct);
		return (yt, null);
	}

	internal static async Task<int> ResolveSeasonNumberAsync(
		TubeArrDbContext db,
		int channelId,
		int? primaryPlaylistId,
		CancellationToken ct) =>
		await ResolveYoutubeCuratedSeasonNumberAsync(db, channelId, primaryPlaylistId, ct);

	static async Task<int> ResolveYoutubeCuratedSeasonNumberAsync(
		TubeArrDbContext db,
		int channelId,
		int? primaryPlaylistId,
		CancellationToken ct)
	{
		if (!primaryPlaylistId.HasValue)
			return StableTvNumbering.ChannelOnlySeasonIndex;

		var playlist = await db.Playlists.FirstOrDefaultAsync(p => p.Id == primaryPlaylistId.Value && p.ChannelId == channelId, ct);
		if (playlist is null)
			return StableTvNumbering.ChannelOnlySeasonIndex;

		return await StableTvNumbering.EnsurePlaylistSeasonIndexAsync(db, playlist.Id, ct);
	}
}
