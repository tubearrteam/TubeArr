using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

/// <summary>Optionally syncs NFOs from DB, then refreshes TubeArr-managed thumbnail sidecars (same rules as a completed download).</summary>
internal static class LibraryNfoAndArtworkRepairRunner
{
	internal static async Task<(int MediaFilesChecked, int NfoFilesWritten, int ArtworkPassFiles, string Message)> RunAsync(
		TubeArrDbContext db,
		IHttpClientFactory httpClientFactory,
		ILogger logger,
		CancellationToken ct,
		Func<string, Task>? reportProgress = null)
	{
		var media = await db.MediaManagementConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		var plex = await db.PlexProviderConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		var plexEnabled = plex?.Enabled == true;
		var useCustomNfos = media?.UseCustomNfos != false;
		var exportThumbs = LibraryThumbnailExportPolicy.ShouldExport(
			media?.DownloadLibraryThumbnails == true,
			plexEnabled);

		if (!useCustomNfos && !exportThumbs)
		{
			return (0, 0, 0,
				"Nothing to do: enable custom NFOs and/or the Plex metadata provider or “download library thumbnails” in Media Management.");
		}

		var checkedCount = 0;
		var nfoWrites = 0;
		var nfoMsg = "NFOs: skipped.";
		if (useCustomNfos)
		{
			if (reportProgress is not null)
				await reportProgress("Syncing library NFOs…");
			(checkedCount, nfoWrites, nfoMsg) = await NfoLibrarySyncRunner.RunAsync(db, logger, ct);
		}

		if (!exportThumbs)
			return (checkedCount, nfoWrites, 0, $"{nfoMsg} Thumbnails: skipped (Plex provider off and library thumbnail download off).");

		var naming = await db.NamingConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		if (naming is null)
			return (checkedCount, nfoWrites, 0, $"{nfoMsg} Thumbnails: skipped (naming configuration is missing).");

		var roots = await db.RootFolders.AsNoTracking().ToListAsync(ct);
		var videoFiles = await db.VideoFiles.AsNoTracking()
			.Where(vf => vf.Path != null && vf.Path != "")
			.ToListAsync(ct);

		if (reportProgress is not null)
			await reportProgress($"Downloading new thumbnails… {videoFiles.Count} media file(s) queued…");

		var artworkPass = 0;
		foreach (var vf in videoFiles)
		{
			if (!File.Exists(vf.Path))
				continue;

			artworkPass++;

			var video = await db.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vf.VideoId, ct);
			if (video is null)
				continue;

			var channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == vf.ChannelId, ct);
			if (channel is null)
				continue;

			PlaylistEntity? playlist = null;
			if (vf.PlaylistId is { } plId)
				playlist = await db.Playlists.AsNoTracking().FirstOrDefaultAsync(p => p.Id == plId, ct);

			try
			{
				if (reportProgress is not null)
				{
					var chTitle = string.IsNullOrWhiteSpace(channel.Title) ? $"Channel {channel.Id}" : channel.Title.Trim();
					var plTitle = playlist is null
						? "Uploads"
						: (string.IsNullOrWhiteSpace(playlist.Title) ? $"Playlist {playlist.Id}" : playlist.Title.Trim());
					var vidTitle = string.IsNullOrWhiteSpace(video.Title)
						? (string.IsNullOrWhiteSpace(video.YoutubeVideoId) ? $"Video {video.Id}" : video.YoutubeVideoId.Trim())
						: video.Title.Trim();
					var vidId = string.IsNullOrWhiteSpace(video.YoutubeVideoId) ? "" : $" [{video.YoutubeVideoId.Trim()}]";
					await reportProgress($"Thumbnail: {chTitle} / {plTitle} / {vidTitle}{vidId} ({artworkPass}/{videoFiles.Count})");
				}

				logger.LogInformation(
					"Thumbnail repair: channelId={ChannelId} channel={ChannelTitle} playlistId={PlaylistId} playlist={PlaylistTitle} videoId={VideoId} youtubeVideoId={YoutubeVideoId} path={Path}",
					channel.Id,
					channel.Title,
					playlist?.Id,
					playlist?.Title,
					video.Id,
					video.YoutubeVideoId,
					vf.Path);

				await PlexLibraryArtworkExporter.WriteForCompletedDownloadAsync(
					db,
					channel,
					video,
					playlist,
					vf.PlaylistId,
					vf.Path!,
					naming,
					roots,
					httpClientFactory,
					logger,
					ct);
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Library thumbnail repair: export failed videoFile id={Id}", vf.Id);
			}
		}

		var msg =
			$"{nfoMsg} Thumbnails: checked {artworkPass} on-disk media file(s); existing JPEG sidecars were not re-downloaded.";
		return (checkedCount, nfoWrites, artworkPass, msg);
	}
}
