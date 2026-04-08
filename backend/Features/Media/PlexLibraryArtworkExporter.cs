using System.Globalization;
using System.Net;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

/// <summary>Writes Plex/Kodi-style local JPEG artwork next to TubeArr library output (poster, fanart, season poster, episode thumb). Managed paths are recorded in the library root <c>.tubearr</c> manifest (<see cref="TubeArrManagedLibraryManifest"/>).</summary>
internal static class PlexLibraryArtworkExporter
{
	/// <summary>i.ytimg.com video thumbnail filenames: spec order first, then common YouTube names for the same tier.</summary>
	static readonly string[] YoutubeVideoThumbFileNames =
	[
		"maxresdefault.jpg",
		"standard.jpg",
		"sddefault.jpg",
		"high.jpg",
		"hqdefault.jpg",
		"medium.jpg",
		"mqdefault.jpg",
		"default.jpg"
	];

	internal static async Task WriteForCompletedDownloadAsync(
		TubeArrDbContext db,
		ChannelEntity channel,
		VideoEntity video,
		PlaylistEntity? playlist,
		int? primaryPlaylistId,
		string mediaFilePath,
		NamingConfigEntity naming,
		List<RootFolderEntity> rootFolders,
		IHttpClientFactory httpClientFactory,
		ILogger? logger,
		CancellationToken ct)
	{
		var (seasonNumber, _) = await NfoLibraryExporter.ResolveSeasonNumberForPlaylistFolderAsync(db, channel.Id, video, primaryPlaylistId, ct);
		await WriteForCompletedDownloadWithSeasonAsync(
			channel,
			video,
			playlist,
			seasonNumber,
			mediaFilePath,
			naming,
			rootFolders,
			httpClientFactory,
			logger,
			ct);
	}

	/// <summary>Uses a playlist-folder season already resolved on the caller's DB context so artwork I/O can run parallel to other work without a second EF writer.</summary>
	internal static async Task WriteForCompletedDownloadWithSeasonAsync(
		ChannelEntity channel,
		VideoEntity video,
		PlaylistEntity? playlist,
		int playlistFolderSeasonNumber,
		string mediaFilePath,
		NamingConfigEntity naming,
		List<RootFolderEntity> rootFolders,
		IHttpClientFactory httpClientFactory,
		ILogger? logger,
		CancellationToken ct)
	{
		var showRoot = DownloadQueueProcessor.GetChannelShowRootPath(channel, video, naming, rootFolders);
		if (string.IsNullOrWhiteSpace(showRoot))
			return;

		using var http = httpClientFactory.CreateClient();
		http.Timeout = TimeSpan.FromSeconds(60);

		var seasonNumber = playlistFolderSeasonNumber;

		try
		{
			await WriteChannelArtworkAsync(http, showRoot, rootFolders, channel, logger, ct);
		}
		catch (Exception ex)
		{
			logger?.LogWarning(ex, "Channel artwork export failed channelId={ChannelId}", channel.Id);
		}

		if (channel.PlaylistFolder == true && playlist is not null && !string.IsNullOrWhiteSpace(playlist.ThumbnailUrl))
		{
			var seasonDir = Path.Combine(showRoot, NfoLibraryExporter.FormatSeasonPlaylistFolderName(seasonNumber));
			Directory.CreateDirectory(seasonDir);
			var seasonFile = $"season{seasonNumber.ToString("00", CultureInfo.InvariantCulture)}-poster.jpg";
			var seasonPath = Path.Combine(seasonDir, seasonFile);
			try
			{
				await WriteManagedJpegFromUrlAsync(
					http,
					playlist.ThumbnailUrl,
					seasonPath,
					ArtworkKind.SeasonPoster,
					showRoot,
					rootFolders,
					logger,
					ct);
			}
			catch (Exception ex)
			{
				logger?.LogWarning(ex,
					"Season poster export failed channelId={ChannelId} playlistId={PlaylistId} season={Season}",
					channel.Id, playlist.Id, seasonNumber);
			}
		}

		try
		{
			await WriteEpisodeThumbAsync(http, mediaFilePath, showRoot, rootFolders, video, logger, ct);
		}
		catch (Exception ex)
		{
			logger?.LogWarning(ex, "Episode thumb export failed videoId={VideoId} youtubeVideoId={YoutubeVideoId}", video.Id, video.YoutubeVideoId);
		}
	}

	static async Task WriteChannelArtworkAsync(
		HttpClient http,
		string showRoot,
		List<RootFolderEntity> rootFolders,
		ChannelEntity channel,
		ILogger? logger,
		CancellationToken ct)
	{
		Directory.CreateDirectory(showRoot);

		if (string.IsNullOrWhiteSpace(channel.ThumbnailUrl))
		{
			logger?.LogWarning("Channel poster skipped: no ThumbnailUrl channelId={ChannelId}", channel.Id);
		}
		else
		{
			var posterPath = Path.Combine(showRoot, "poster.jpg");
			await WriteManagedJpegFromUrlAsync(
				http,
				channel.ThumbnailUrl,
				posterPath,
				ArtworkKind.Poster,
				showRoot,
				rootFolders,
				logger,
				ct);
		}

		if (!string.IsNullOrWhiteSpace(channel.BannerUrl))
		{
			var fanartPath = Path.Combine(showRoot, "fanart.jpg");
			await WriteManagedJpegFromUrlAsync(
				http,
				channel.BannerUrl!,
				fanartPath,
				ArtworkKind.Fanart,
				showRoot,
				rootFolders,
				logger,
				ct);
		}
	}

	static async Task WriteEpisodeThumbAsync(
		HttpClient http,
		string mediaFilePath,
		string showRoot,
		List<RootFolderEntity> rootFolders,
		VideoEntity video,
		ILogger? logger,
		CancellationToken ct)
	{
		var vid = (video.YoutubeVideoId ?? "").Trim();
		if (string.IsNullOrEmpty(vid))
		{
			logger?.LogWarning("Episode thumb skipped: empty YoutubeVideoId videoId={VideoId}", video.Id);
			return;
		}

		var dir = Path.GetDirectoryName(mediaFilePath);
		if (string.IsNullOrEmpty(dir))
			return;

		Directory.CreateDirectory(dir);
		var baseName = Path.GetFileNameWithoutExtension(mediaFilePath);
		var thumbPath = Path.Combine(dir, baseName + "-thumb.jpg");

		byte[]? raw = null;
		string? usedUrl = null;
		foreach (var name in YoutubeVideoThumbFileNames)
		{
			var url = $"https://i.ytimg.com/vi/{vid}/{name}";
			raw = await TryDownloadBytesAsync(http, url, ct);
			if (raw is not null)
			{
				usedUrl = url;
				break;
			}
		}

		if (raw is null)
		{
			logger?.LogWarning("Episode thumb download failed for all qualities videoId={VideoId} youtubeVideoId={YoutubeVideoId}", video.Id, vid);
			return;
		}

		await TryWriteManagedJpegFromBytesAsync(
			thumbPath,
			ArtworkKind.EpisodeThumb,
			showRoot,
			rootFolders,
			logger,
			ct,
			raw,
			usedUrl);
	}

	static async Task WriteManagedJpegFromUrlAsync(
		HttpClient http,
		string sourceUrl,
		string destinationPath,
		ArtworkKind kind,
		string showRoot,
		List<RootFolderEntity> rootFolders,
		ILogger? logger,
		CancellationToken ct)
	{
		var raw = await TryDownloadBytesAsync(http, sourceUrl, ct);
		if (raw is null)
		{
			logger?.LogWarning("Artwork download failed kind={Kind} url={Url}", kind, TruncateUrl(sourceUrl));
			return;
		}

		await TryWriteManagedJpegFromBytesAsync(destinationPath, kind, showRoot, rootFolders, logger, ct, raw, sourceUrl);
	}

	static async Task TryWriteManagedJpegFromBytesAsync(
		string destinationPath,
		ArtworkKind kind,
		string showRoot,
		List<RootFolderEntity> rootFolders,
		ILogger? logger,
		CancellationToken ct,
		byte[] imageBytes,
		string? sourceDescription)
	{
		if (!CanWriteTubeArrManagedFile(destinationPath, showRoot, rootFolders))
			return;

		var tempPath = destinationPath + ".tmp";

		try
		{
			await using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan))
			{
				await EncodeAsTubeArrJpegAsync(imageBytes, kind, fs, ct);
			}

			await using (var verify = File.OpenRead(tempPath))
			{
				await Image.LoadAsync(verify, ct);
			}

			File.Move(tempPath, destinationPath, overwrite: true);
			TubeArrManagedLibraryManifest.RegisterManagedAsset(rootFolders, destinationPath, TubeArrManagedLibraryManifest.KindArtwork);
		}
		catch (Exception ex)
		{
			logger?.LogWarning(ex,
				"Artwork encode/write failed kind={Kind} dest={Dest} source={Source}",
				kind,
				destinationPath,
				sourceDescription ?? TruncateUrl(destinationPath));
			try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* best-effort */ }
		}
	}

	/// <summary>Allows creating missing artwork only. Existing files are never replaced (repair / re-download skips).</summary>
	internal static bool CanWriteTubeArrManagedFile(string destinationPath, string showRoot, List<RootFolderEntity> rootFolders)
	{
		var lib = TubeArrManagedLibraryManifest.TryResolveLibraryRootForPath(destinationPath, rootFolders)
			?? TubeArrManagedLibraryManifest.TryResolveLibraryRootForPath(showRoot, rootFolders);
		if (string.IsNullOrWhiteSpace(lib))
			return false;
		return TubeArrManagedLibraryManifest.TryGetManagedRelativePath(lib, destinationPath) is not null
			&& !File.Exists(destinationPath);
	}

	static async Task EncodeAsTubeArrJpegAsync(byte[] raw, ArtworkKind kind, Stream destination, CancellationToken ct)
	{
		using var image = await Image.LoadAsync(new MemoryStream(raw, writable: false), ct);
		image.Metadata.ExifProfile = null;
		image.Metadata.IccProfile = null;
		image.Metadata.XmpProfile = null;

		var (maxW, maxH) = kind switch
		{
			ArtworkKind.Poster => (int.MaxValue, 1500),
			ArtworkKind.Fanart => (1920, 1080),
			ArtworkKind.SeasonPoster => (int.MaxValue, 1500),
			ArtworkKind.EpisodeThumb => (1280, 720),
			_ => (int.MaxValue, int.MaxValue)
		};

		if (maxW < int.MaxValue || maxH < int.MaxValue)
		{
			var w = image.Width;
			var h = image.Height;
			if (w > maxW || h > maxH)
			{
				var ratio = Math.Min((double)maxW / w, (double)maxH / h);
				if (ratio < 1.0)
				{
					var nw = Math.Max(1, (int)Math.Round(w * ratio));
					var nh = Math.Max(1, (int)Math.Round(h * ratio));
					image.Mutate(x => x.Resize(nw, nh));
				}
			}
		}

		var encoder = new JpegEncoder { Quality = 88 };
		await image.SaveAsJpegAsync(destination, encoder, ct);
	}

	static async Task<byte[]?> TryDownloadBytesAsync(HttpClient http, string url, CancellationToken ct)
	{
		try
		{
			using var req = new HttpRequestMessage(HttpMethod.Get, url);
			req.Headers.UserAgent.ParseAdd("TubeArr/1.0");
			using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
			if (resp.StatusCode != HttpStatusCode.OK)
				return null;
			var len = resp.Content.Headers.ContentLength;
			if (len.HasValue && (len.Value <= 0 || len.Value > 40 * 1024 * 1024))
				return null;
			return await resp.Content.ReadAsByteArrayAsync(ct);
		}
		catch
		{
			return null;
		}
	}

	static string TruncateUrl(string? url, int max = 160)
	{
		if (string.IsNullOrEmpty(url))
			return "";
		var u = url.Trim();
		return u.Length <= max ? u : u.Substring(0, max) + "…";
	}

	enum ArtworkKind
	{
		Poster,
		Fanart,
		SeasonPoster,
		EpisodeThumb
	}
}
