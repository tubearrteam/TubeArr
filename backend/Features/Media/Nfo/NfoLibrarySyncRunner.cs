using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.Data;
using TubeArr.Backend.Media.Nfo;

namespace TubeArr.Backend;

/// <summary>Ensures tvshow/season/episode NFO files match database state for tracked <see cref="VideoFileEntity"/> rows (no network).</summary>
internal static class NfoLibrarySyncRunner
{
	internal static async Task<(int MediaFilesChecked, int NfoFilesWritten, string Message)> RunAsync(
		TubeArrDbContext db,
		ILogger logger,
		CancellationToken ct)
	{
		var media = await db.MediaManagementConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		if (media?.UseCustomNfos == false)
			return (0, 0, "Custom NFOs are disabled in Media Management settings.");

		var naming = await db.NamingConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		if (naming is null)
			return (0, 0, "Naming configuration is missing.");

		var roots = await db.RootFolders.AsNoTracking().ToListAsync(ct);
		var videoFiles = await db.VideoFiles.AsNoTracking()
			.Where(vf => vf.Path != null && vf.Path != "")
			.ToListAsync(ct);

		var nfoWrites = 0;
		var checkedCount = 0;

		foreach (var vf in videoFiles)
		{
			if (!File.Exists(vf.Path))
				continue;

			checkedCount++;

			var video = await db.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vf.VideoId, ct);
			if (video is null)
				continue;

			var channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == vf.ChannelId, ct);
			if (channel is null)
				continue;

			var primaryPid = await ChannelDtoMapper.GetPrimaryPlaylistIdForVideoAsync(db, channel.Id, video.Id, ct);
			PlaylistEntity? playlist = null;
			if (primaryPid is { } plId)
				playlist = await db.Playlists.AsNoTracking().FirstOrDefaultAsync(p => p.Id == plId, ct);

			logger.LogInformation(
				"NFO sync: channelId={ChannelId} channel={ChannelTitle} playlistId={PlaylistId} playlist={PlaylistTitle} videoId={VideoId} youtubeVideoId={YoutubeVideoId} path={Path}",
				channel.Id,
				channel.Title,
				playlist?.Id,
				playlist?.Title,
				video.Id,
				video.YoutubeVideoId,
				vf.Path);

			NfoLibraryExporter.ExpectedNfoSet? expected;
			try
			{
				expected = await NfoLibraryExporter.TryBuildExpectedNfoSetAsync(
					db,
					channel,
					video,
					playlist,
					primaryPid,
					vf.Path,
					naming,
					roots,
					ct);
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Library NFO sync: could not compute expected NFOs for videoFile id={Id}", vf.Id);
				continue;
			}

			if (expected is not { } e)
			{
				logger.LogInformation(
					"NFO sync: skipped (could not resolve expected set) channelId={ChannelId} playlistId={PlaylistId} videoId={VideoId}",
					channel.Id,
					playlist?.Id,
					video.Id);
				continue;
			}

			var showRoot = Path.GetDirectoryName(e.TvShowNfoPath);
			if (string.IsNullOrEmpty(showRoot))
				continue;

			var before = nfoWrites;
			nfoWrites += await EnsureNfoFileAsync(roots, e.TvShowNfoPath, e.TvShowXml, ct);
			if (e.SeasonNfoPath is not null && e.SeasonXml is not null)
				nfoWrites += await EnsureNfoFileAsync(roots, e.SeasonNfoPath, e.SeasonXml, ct);
			nfoWrites += await EnsureNfoFileAsync(roots, e.EpisodeNfoPath, e.EpisodeXml, ct);

			var wrote = nfoWrites - before;
			logger.LogInformation(
				"NFO sync: wroteOrUpdated={NfoWrites} tvshow={Tvshow} season={Season} episode={Episode}",
				wrote,
				e.TvShowNfoPath,
				e.SeasonNfoPath,
				e.EpisodeNfoPath);
		}

		var msg =
			$"Checked {checkedCount} media file(s) on disk; wrote or updated {nfoWrites} NFO file(s).";
		return (checkedCount, nfoWrites, msg);
	}

	static async Task<int> EnsureNfoFileAsync(List<RootFolderEntity> rootFolders, string path, string expectedXml, CancellationToken ct)
	{
		if (FileContentMatches(path, expectedXml))
			return 0;

		if (!TubeArrManagedLibraryManifest.CanWriteManagedNfo(rootFolders, path))
			return 0;

		var dir = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(dir))
			Directory.CreateDirectory(dir);
		await File.WriteAllTextAsync(path, expectedXml, NfoXmlText.Utf8Encoding, ct);
		TubeArrManagedLibraryManifest.RegisterManagedAsset(rootFolders, path, TubeArrManagedLibraryManifest.KindNfo);
		return 1;
	}

	static bool FileContentMatches(string path, string expected)
	{
		try
		{
			if (!File.Exists(path))
				return false;
			var actual = File.ReadAllText(path, NfoXmlText.Utf8Encoding);
			return string.Equals(NormalizeNewlines(actual), NormalizeNewlines(expected), StringComparison.Ordinal);
		}
		catch
		{
			return false;
		}
	}

	static string NormalizeNewlines(string s) =>
		s.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
}
