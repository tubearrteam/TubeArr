using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Data;
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

		api.MapPost("/channels/import", () => Results.Json(Array.Empty<object>()));

		api.MapGet("/channels/folder-preview", async (string? youtubeChannelId, string? title, string? titleSlug, TubeArrDbContext db) =>
		{
			var yt = (youtubeChannelId ?? "").Trim();
			if (string.IsNullOrWhiteSpace(yt))
				return Results.Json(new { folder = "" });

			var naming = await db.NamingConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync() ?? new NamingConfigEntity { Id = 1 };

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

			return Results.Json(new { folder });
		});

		api.MapGet("/channels/{id:int}/folder", async (int id, TubeArrDbContext db) =>
		{
			var channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
			if (channel is null)
				return Results.NotFound();

			var naming = await db.NamingConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync() ?? new NamingConfigEntity { Id = 1 };
			var dummyVideo = new VideoEntity { Title = "", YoutubeVideoId = "", UploadDateUtc = DateTimeOffset.UtcNow };
			var context = new VideoFileNaming.NamingContext(Channel: channel, Video: dummyVideo, Playlist: null, PlaylistIndex: null, QualityFull: null, Resolution: null, Extension: null);

			var folder = VideoFileNaming.BuildFolderName(naming.ChannelFolderFormat, context, naming);
			if (string.IsNullOrWhiteSpace(folder))
				folder = string.IsNullOrWhiteSpace(channel.TitleSlug) ? SlugHelper.Slugify(channel.Title) : channel.TitleSlug;

			return Results.Json(new { folder });
		});
	}
}

