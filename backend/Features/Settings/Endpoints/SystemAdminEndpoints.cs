using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;
using System.Linq;
using System.Threading.Tasks;

namespace TubeArr.Backend;

public static class SystemAdminEndpoints
{
	public static void Map(RouteGroupBuilder api)
	{
		api.MapPost("/system/restart", (IHostApplicationLifetime lifetime) =>
		{
			_ = Task.Run(async () =>
			{
				await Task.Delay(250);
				lifetime.StopApplication();
			});

			return Results.Ok();
		});

		api.MapPost("/system/shutdown", (IHostApplicationLifetime lifetime) =>
		{
			_ = Task.Run(async () =>
			{
				await Task.Delay(250);
				lifetime.StopApplication();
			});

			return Results.Ok();
		});

		api.MapPost("/channels/import", async (
			CreateChannelRequest[] requests,
			TubeArrDbContext db,
			HttpContext httpContext,
			ChannelIngestionOrchestrator ingestionOrchestrator) =>
		{
			if (requests is null || requests.Length == 0)
				return Results.Json(Array.Empty<ChannelDto>());

			var list = new List<ChannelDto>(requests.Length);
			foreach (var request in requests)
			{
				var (channel, _, errorMessage) = await ingestionOrchestrator.CreateOrUpdateAsync(request, db, httpContext.RequestAborted);
				if (!string.IsNullOrWhiteSpace(errorMessage))
					return Results.BadRequest(new { message = errorMessage });
				if (channel is null)
					return Results.BadRequest(new { message = "Unable to create channel." });

				var playlists = await db.Playlists.AsNoTracking().Where(p => p.ChannelId == channel.Id).ToListAsync(httpContext.RequestAborted);
				var customPlaylists = await db.ChannelCustomPlaylists.AsNoTracking()
					.Where(c => c.ChannelId == channel.Id)
					.OrderBy(c => c.Priority)
					.ThenBy(c => c.Id)
					.ToListAsync(httpContext.RequestAborted);
				var maxUploadByPlaylist = await ChannelDtoMapper.LoadMaxUploadUtcByPlaylistIdsAsync(db, playlists.Select(p => p.Id), httpContext.RequestAborted);
				var totalVideoCount = await db.Videos.AsNoTracking().CountAsync(x => x.ChannelId == channel.Id, httpContext.RequestAborted);
				var monitoredVideoCount = await db.Videos.AsNoTracking().CountAsync(x => x.ChannelId == channel.Id && x.Monitored, httpContext.RequestAborted);
				var videoFileStats = await ChannelVideoFileStatistics.GetByChannelIdAsync(db, channel.Id);
				var monitoredVideoFileCount = await ChannelVideoFileStatistics.GetMonitoredByChannelIdAsync(db, channel.Id);
				var maxUploadByChannel = await ChannelDtoMapper.LoadMaxUploadUtcByChannelIdsAsync(db, new[] { channel.Id }, httpContext.RequestAborted);
				var minActiveSinceByChannel = await ChannelDtoMapper.LoadMinActiveSinceUtcByChannelIdsAsync(db, new[] { channel.Id }, httpContext.RequestAborted);
				DateTimeOffset? lastUploadUtc = maxUploadByChannel.TryGetValue(channel.Id, out var lu) ? lu : null;
				DateTimeOffset? firstUploadUtc = minActiveSinceByChannel.TryGetValue(channel.Id, out var fu) ? fu : null;
				var tagIdsForDto = await ChannelTagHelper.LoadTagIdsForChannelAsync(db, channel.Id, httpContext.RequestAborted);
				list.Add(ChannelDtoMapper.CreateChannelDto(channel, playlists, customPlaylists, monitoredVideoCount, monitoredVideoFileCount, videoFileStats.SizeOnDisk, totalVideoCount, maxUploadByPlaylist, lastUploadUtc: lastUploadUtc, firstUploadUtc: firstUploadUtc, channelTagIds: tagIdsForDto));
			}

			return Results.Json(list.ToArray());
		});

		api.MapGet("/channels/folder-preview", async (string? youtubeChannelId, string? title, string? titleSlug, bool? playlistFolder, string? samplePlaylistTitle, TubeArrDbContext db) =>
		{
			var yt = (youtubeChannelId ?? "").Trim();
			if (string.IsNullOrWhiteSpace(yt))
				return Results.Json(new { folder = "", playlistFolder = false, exampleUploadsOnlyRelativePath = "", exampleCuratedPlaylistRelativePath = (string?)null });

			var naming = await db.NamingConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync() ?? new NamingConfigEntity { Id = 1 };
			var media = await db.MediaManagementConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync();
			var useCustomNfos = media?.UseCustomNfos != false;

			var t = (title ?? "").Trim();
			if (string.IsNullOrWhiteSpace(t))
				t = "Channel";

			var slug = (titleSlug ?? "").Trim();
			if (string.IsNullOrWhiteSpace(slug))
				slug = SlugHelper.Slugify(t);

			var previewChannel = new ChannelEntity
			{
				YoutubeChannelId = yt,
				Title = t,
				TitleSlug = slug
			};

			var dummyVideo = new VideoEntity { Title = "", YoutubeVideoId = "", UploadDateUtc = DateTimeOffset.UtcNow };
			var context = new VideoFileNaming.NamingContext(Channel: previewChannel, Video: dummyVideo, Playlist: null, PlaylistIndex: null, QualityFull: null, Resolution: null, Extension: null);
			var folder = VideoFileNaming.BuildFolderName(naming.ChannelFolderFormat, context, naming);

			if (string.IsNullOrWhiteSpace(folder))
				folder = string.IsNullOrWhiteSpace(previewChannel.TitleSlug) ? SlugHelper.Slugify(previewChannel.Title) : previewChannel.TitleSlug;

			var usePlaylistFolder = playlistFolder == true;
			string? exampleCurated = null;
			if (usePlaylistFolder)
			{
				var plTitle = string.IsNullOrWhiteSpace(samplePlaylistTitle) ? "Sample Playlist" : samplePlaylistTitle.Trim();
				var samplePl = new PlaylistEntity { Title = plTitle, YoutubePlaylistId = "PLxxxxxxxxxxxx", ChannelId = 0 };
				var plCtx = new VideoFileNaming.NamingContext(Channel: previewChannel, Video: dummyVideo, Playlist: samplePl, PlaylistIndex: null, QualityFull: null, Resolution: null, Extension: null);
				var plFolder = useCustomNfos
					? NfoLibraryExporter.FormatSeasonPlaylistFolderName(2)
					: VideoFileNaming.BuildFolderName(naming.PlaylistFolderFormat, plCtx, naming);
				if (!string.IsNullOrWhiteSpace(plFolder))
				{
					exampleCurated = string.Join('/',
						new[] { folder.TrimEnd('/', '\\'), plFolder, "2024-01-01 - Example Title [dQw4w9WgXcQ].mp4" }
							.Where(s => !string.IsNullOrWhiteSpace(s)));
				}
			}

			var uploadsFolder = usePlaylistFolder && useCustomNfos
				? NfoLibraryExporter.FormatSeasonPlaylistFolderName(1)
				: null;
			var exampleUploads = string.Join('/',
				new[] { folder.TrimEnd('/', '\\'), uploadsFolder, "2024-01-01 - Example Title [dQw4w9WgXcQ].mp4" }
					.Where(s => !string.IsNullOrWhiteSpace(s)));

			return Results.Json(new
			{
				folder,
				playlistFolder = usePlaylistFolder,
				exampleUploadsOnlyRelativePath = exampleUploads,
				exampleCuratedPlaylistRelativePath = exampleCurated
			});
		});

		api.MapGet("/channels/{id:int}/folder", async (int id, bool? playlistFolder, string? samplePlaylistTitle, TubeArrDbContext db) =>
		{
			var channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
			if (channel is null)
				return Results.NotFound();

			var naming = await db.NamingConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync() ?? new NamingConfigEntity { Id = 1 };
			var media = await db.MediaManagementConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync();
			var useCustomNfos = media?.UseCustomNfos != false;
			var dummyVideo = new VideoEntity { Title = "", YoutubeVideoId = "", UploadDateUtc = DateTimeOffset.UtcNow };
			var context = new VideoFileNaming.NamingContext(Channel: channel, Video: dummyVideo, Playlist: null, PlaylistIndex: null, QualityFull: null, Resolution: null, Extension: null);

			var folder = VideoFileNaming.BuildFolderName(naming.ChannelFolderFormat, context, naming);
			if (string.IsNullOrWhiteSpace(folder))
				folder = string.IsNullOrWhiteSpace(channel.TitleSlug) ? SlugHelper.Slugify(channel.Title) : channel.TitleSlug;

			var usePlaylistFolder = playlistFolder == true;
			string? exampleCurated = null;
			if (usePlaylistFolder)
			{
				var plTitle = string.IsNullOrWhiteSpace(samplePlaylistTitle) ? "Sample Playlist" : samplePlaylistTitle.Trim();
				var samplePl = new PlaylistEntity { Title = plTitle, YoutubePlaylistId = "PLxxxxxxxxxxxx", ChannelId = channel.Id };
				var plCtx = new VideoFileNaming.NamingContext(Channel: channel, Video: dummyVideo, Playlist: samplePl, PlaylistIndex: null, QualityFull: null, Resolution: null, Extension: null);
				var plFolder = useCustomNfos
					? NfoLibraryExporter.FormatSeasonPlaylistFolderName(2)
					: VideoFileNaming.BuildFolderName(naming.PlaylistFolderFormat, plCtx, naming);
				if (!string.IsNullOrWhiteSpace(plFolder))
				{
					exampleCurated = string.Join('/',
						new[] { folder.TrimEnd('/', '\\'), plFolder, "2024-01-01 - Example Title [dQw4w9WgXcQ].mp4" }
							.Where(s => !string.IsNullOrWhiteSpace(s)));
				}
			}

			var uploadsFolder = usePlaylistFolder && useCustomNfos
				? NfoLibraryExporter.FormatSeasonPlaylistFolderName(1)
				: null;
			var exampleUploads = string.Join('/',
				new[] { folder.TrimEnd('/', '\\'), uploadsFolder, "2024-01-01 - Example Title [dQw4w9WgXcQ].mp4" }
					.Where(s => !string.IsNullOrWhiteSpace(s)));

			return Results.Json(new
			{
				folder,
				playlistFolder = usePlaylistFolder,
				exampleUploadsOnlyRelativePath = exampleUploads,
				exampleCuratedPlaylistRelativePath = exampleCurated
			});
		});
	}
}

