using System.IO;
using System.Xml.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TubeArr.Backend.Data;
using TubeArr.Backend.Media.Nfo;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class NfoLibraryExportIntegrationTests
{
	[Fact]
	public async Task Export_writes_tvshow_season_and_episode_nfos_for_playlist_layout()
	{
		var tempRoot = Path.Combine(Path.GetTempPath(), "tubearr-nfo-int-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempRoot);

		try
		{
			using var connection = new SqliteConnection("Data Source=:memory:");
			await connection.OpenAsync();

			var options = new DbContextOptionsBuilder<TubeArrDbContext>()
				.UseSqlite(connection)
				.Options;

			await using (var db = new TubeArrDbContext(options))
			{
				await db.Database.MigrateAsync();
				db.RootFolders.Add(new RootFolderEntity { Id = 1, Path = tempRoot });
				db.NamingConfig.Add(new NamingConfigEntity { Id = 1 });
				db.Channels.Add(new ChannelEntity
				{
					Id = 1,
					YoutubeChannelId = "UCxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
					Title = "Integration Channel",
					Description = "Channel about tests",
					TitleSlug = "integration-channel",
					PlaylistFolder = true,
					Path = null
				});
				db.Playlists.Add(new PlaylistEntity
				{
					Id = 1,
					ChannelId = 1,
					YoutubePlaylistId = "PLxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
					Title = "Curated List",
					Monitored = true
				});

				var u0 = new DateTimeOffset(2024, 1, 10, 12, 0, 0, TimeSpan.Zero);
				var u1 = new DateTimeOffset(2024, 3, 2, 12, 0, 0, TimeSpan.Zero);
				var u2 = new DateTimeOffset(2024, 6, 18, 12, 0, 0, TimeSpan.Zero);

				db.Videos.Add(new VideoEntity
				{
					Id = 1,
					ChannelId = 1,
					YoutubeVideoId = "aaaaaaaaaaa",
					Title = "First",
					Description = "d1",
					UploadDateUtc = u0,
					Monitored = true
				});
				db.Videos.Add(new VideoEntity
				{
					Id = 2,
					ChannelId = 1,
					YoutubeVideoId = "bbbbbbbbbbb",
					Title = "Second",
					Description = null,
					UploadDateUtc = u1,
					Monitored = true
				});
				db.Videos.Add(new VideoEntity
				{
					Id = 3,
					ChannelId = 1,
					YoutubeVideoId = "ccccccccccc",
					Title = "Third",
					Description = "plot & <ok>",
					UploadDateUtc = u2,
					Monitored = true
				});

				db.PlaylistVideos.Add(new PlaylistVideoEntity { PlaylistId = 1, VideoId = 1, Position = 0 });
				db.PlaylistVideos.Add(new PlaylistVideoEntity { PlaylistId = 1, VideoId = 2, Position = 1 });
				db.PlaylistVideos.Add(new PlaylistVideoEntity { PlaylistId = 1, VideoId = 3, Position = 2 });

				await db.SaveChangesAsync();
			}

			await using (var db = new TubeArrDbContext(options))
			{
				var channel = await db.Channels.AsNoTracking().FirstAsync();
				var video = await db.Videos.AsNoTracking().FirstAsync(v => v.Id == 3);
				var playlist = await db.Playlists.AsNoTracking().FirstAsync();
				var naming = await db.NamingConfig.AsNoTracking().FirstAsync();
				var roots = await db.RootFolders.AsNoTracking().ToListAsync();

				var showRoot = DownloadQueueProcessor.GetChannelShowRootPath(channel, video, naming, roots);
				Assert.NotNull(showRoot);
				var seasonDir = Path.Combine(showRoot!, "Season 02");
				var mediaPath = Path.Combine(seasonDir, "Third.mkv");
				Directory.CreateDirectory(seasonDir);
				await File.WriteAllTextAsync(mediaPath, "");

				await NfoLibraryExporter.WriteForCompletedDownloadAsync(
					db,
					channel,
					video,
					playlist,
					primaryPlaylistId: 1,
					mediaPath,
					naming,
					roots,
					default);

				var tvshow = Path.Combine(showRoot!, "tvshow.nfo");
				var season = Path.Combine(seasonDir, "season.nfo");
				var episode = Path.Combine(seasonDir, "Third.nfo");

				Assert.True(File.Exists(tvshow));
				Assert.True(File.Exists(season));
				Assert.True(File.Exists(episode));

				var tv = XDocument.Load(tvshow);
				Assert.Equal("Integration Channel", tv.Root?.Element("title")?.Value);
				Assert.Equal("2024", tv.Root?.Element("year")?.Value);
				Assert.Equal("Channel about tests", tv.Root?.Element("plot")?.Value);

				var sn = XDocument.Load(season);
				Assert.Equal("2", sn.Root?.Element("seasonnumber")?.Value);
				Assert.Equal("Curated List", sn.Root?.Element("title")?.Value);
				Assert.Equal("2024", sn.Root?.Element("year")?.Value);

				var ep = XDocument.Load(episode);
				Assert.Equal("Third", ep.Root?.Element("title")?.Value);
				Assert.Equal("2", ep.Root?.Element("season")?.Value);
				Assert.Equal("3", ep.Root?.Element("episode")?.Value);
				Assert.Equal("plot & <ok>", ep.Root?.Element("plot")?.Value);
				Assert.Equal("2024-06-18", ep.Root?.Element("aired")?.Value);

				Assert.DoesNotContain("imdb", File.ReadAllText(tvshow), StringComparison.OrdinalIgnoreCase);
				Assert.DoesNotContain("genre", File.ReadAllText(tvshow), StringComparison.OrdinalIgnoreCase);

				File.Delete(episode);

				var mm = await db.MediaManagementConfig.FirstOrDefaultAsync();
				if (mm is null)
					db.MediaManagementConfig.Add(new MediaManagementConfigEntity { Id = 1, UseCustomNfos = true });
				else
					mm.UseCustomNfos = true;

				db.VideoFiles.Add(new VideoFileEntity
				{
					VideoId = 3,
					ChannelId = 1,
					PlaylistId = 1,
					Path = mediaPath,
					RelativePath = "Season 02/Third.mkv",
					Size = 0,
					DateAdded = DateTimeOffset.UtcNow
				});
				await db.SaveChangesAsync();

				var (_, writes, _) = await NfoLibrarySyncRunner.RunAsync(db, NullLogger.Instance, default);
				Assert.True(writes >= 1);
				Assert.True(File.Exists(episode));
				var ep2 = XDocument.Load(episode);
				Assert.Equal("Third", ep2.Root?.Element("title")?.Value);
			}
		}
		finally
		{
			try
			{
				if (Directory.Exists(tempRoot))
					Directory.Delete(tempRoot, recursive: true);
			}
			catch
			{
				// ignore
			}
		}
	}

	[Fact]
	public async Task ResolveEpisodeNumber_orders_by_position_then_upload()
	{
		using var connection = new SqliteConnection("Data Source=:memory:");
		await connection.OpenAsync();
		var options = new DbContextOptionsBuilder<TubeArrDbContext>().UseSqlite(connection).Options;
		await using var db = new TubeArrDbContext(options);
		await db.Database.MigrateAsync();

		db.Channels.Add(new ChannelEntity
		{
			Id = 1,
			YoutubeChannelId = "UCtesttesttesttesttesttesttestte",
			Title = "C",
			TitleSlug = "c",
			PlaylistFolder = false
		});
		db.Playlists.Add(new PlaylistEntity
		{
			Id = 10,
			ChannelId = 1,
			YoutubePlaylistId = "PLlist",
			Title = "P",
			Monitored = true
		});
		var t0 = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
		db.Videos.Add(new VideoEntity { Id = 101, ChannelId = 1, YoutubeVideoId = "v101", Title = "A", UploadDateUtc = t0, Monitored = true });
		db.Videos.Add(new VideoEntity { Id = 102, ChannelId = 1, YoutubeVideoId = "v102", Title = "B", UploadDateUtc = t0.AddDays(1), Monitored = true });
		db.PlaylistVideos.Add(new PlaylistVideoEntity { PlaylistId = 10, VideoId = 102, Position = 0 });
		db.PlaylistVideos.Add(new PlaylistVideoEntity { PlaylistId = 10, VideoId = 101, Position = 1 });
		await db.SaveChangesAsync();

		var n102 = await NfoPlaylistEpisodeResolver.ResolveEpisodeNumberAsync(db, 10, 102, default);
		var n101 = await NfoPlaylistEpisodeResolver.ResolveEpisodeNumberAsync(db, 10, 101, default);
		Assert.Equal(1, n102);
		Assert.Equal(2, n101);
	}
}
