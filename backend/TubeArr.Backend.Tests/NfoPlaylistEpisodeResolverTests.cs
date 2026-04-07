using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Data;
using TubeArr.Backend.Media.Nfo;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class NfoPlaylistEpisodeResolverTests
{
	[Fact]
	public async Task playlist_folder_uses_playlist_order_not_plex_episode_index()
	{
		var dbPath = CreateTempDbPath();
		try
		{
			var options = new DbContextOptionsBuilder<TubeArrDbContext>()
				.UseSqlite("Data Source=" + dbPath)
				.Options;

			await using var db = new TubeArrDbContext(options);
			await db.Database.MigrateAsync();

			db.Channels.Add(new ChannelEntity
			{
				Id = 1,
				YoutubeChannelId = "UCpfolderplaylistfolderplaylst",
				Title = "Ch",
				TitleSlug = "ch",
				PlaylistFolder = true
			});
			db.Playlists.Add(new PlaylistEntity
			{
				Id = 10,
				ChannelId = 1,
				YoutubePlaylistId = "PLx",
				Title = "P",
				Monitored = true
			});
			var t0 = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
			db.Videos.Add(new VideoEntity
			{
				Id = 101,
				ChannelId = 1,
				YoutubeVideoId = "aaaaaaaaaaa",
				Title = "A",
				UploadDateUtc = t0,
				Monitored = true,
				PlexEpisodeIndex = 9,
				PlexSeasonIndex = 2
			});
			db.Videos.Add(new VideoEntity
			{
				Id = 102,
				ChannelId = 1,
				YoutubeVideoId = "bbbbbbbbbbb",
				Title = "B",
				UploadDateUtc = t0.AddDays(1),
				Monitored = true,
				PlexEpisodeIndex = 9,
				PlexSeasonIndex = 2
			});
			db.PlaylistVideos.Add(new PlaylistVideoEntity { PlaylistId = 10, VideoId = 102, Position = 0 });
			db.PlaylistVideos.Add(new PlaylistVideoEntity { PlaylistId = 10, VideoId = 101, Position = 1 });
			await db.SaveChangesAsync();

			var n102 = await NfoPlaylistEpisodeResolver.ResolveEpisodeNumberAsync(db, 10, 102, default);
			var n101 = await NfoPlaylistEpisodeResolver.ResolveEpisodeNumberAsync(db, 10, 101, default);
			Assert.Equal(1, n102);
			Assert.Equal(2, n101);
		}
		finally
		{
			TryDelete(dbPath);
		}
	}

	[Fact]
	public async Task channel_only_counts_season_01_videos_not_entire_channel()
	{
		var dbPath = CreateTempDbPath();
		try
		{
			var options = new DbContextOptionsBuilder<TubeArrDbContext>()
				.UseSqlite("Data Source=" + dbPath)
				.Options;

			await using var db = new TubeArrDbContext(options);
			await db.Database.MigrateAsync();

			db.Channels.Add(new ChannelEntity
			{
				Id = 1,
				YoutubeChannelId = "UCchonlyseason01scopeeeeeeeeee",
				Title = "Ch",
				TitleSlug = "ch",
				PlaylistFolder = true
			});
			db.Playlists.Add(new PlaylistEntity
			{
				Id = 10,
				ChannelId = 1,
				YoutubePlaylistId = "PLcuratedonly",
				Title = "Curated",
				Monitored = true,
				SeasonIndex = 2,
				SeasonIndexLocked = true
			});
			var t0 = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
			db.Videos.Add(new VideoEntity
			{
				Id = 201,
				ChannelId = 1,
				YoutubeVideoId = "ddddddddddd",
				Title = "Older upload playlist primary",
				UploadDateUtc = t0,
				Monitored = true,
				PlexSeasonIndex = 2,
				PlexEpisodeIndex = 1,
				PlexPrimaryPlaylistId = 10
			});
			db.Videos.Add(new VideoEntity
			{
				Id = 202,
				ChannelId = 1,
				YoutubeVideoId = "eeeeeeeeeee",
				Title = "Channel upload A",
				UploadDateUtc = t0.AddDays(1),
				Monitored = true,
				PlexSeasonIndex = 1,
				PlexEpisodeIndex = 1
			});
			db.Videos.Add(new VideoEntity
			{
				Id = 203,
				ChannelId = 1,
				YoutubeVideoId = "fffffffffff",
				Title = "Channel upload B",
				UploadDateUtc = t0.AddDays(2),
				Monitored = true,
				PlexSeasonIndex = 1,
				PlexEpisodeIndex = 2
			});
			db.PlaylistVideos.Add(new PlaylistVideoEntity { PlaylistId = 10, VideoId = 201, Position = 0 });
			await db.SaveChangesAsync();

			var n202 = await NfoPlaylistEpisodeResolver.ResolveEpisodeNumberAsync(db, null, 202, default);
			var n203 = await NfoPlaylistEpisodeResolver.ResolveEpisodeNumberAsync(db, null, 203, default);
			Assert.Equal(1, n202);
			Assert.Equal(2, n203);
		}
		finally
		{
			TryDelete(dbPath);
		}
	}

	[Fact]
	public async Task without_playlist_folder_uses_playlist_order_not_stale_plex_episode_index()
	{
		var dbPath = CreateTempDbPath();
		try
		{
			var options = new DbContextOptionsBuilder<TubeArrDbContext>()
				.UseSqlite("Data Source=" + dbPath)
				.Options;

			await using var db = new TubeArrDbContext(options);
			await db.Database.MigrateAsync();

			db.Channels.Add(new ChannelEntity
			{
				Id = 1,
				YoutubeChannelId = "UCnoplaylistfolderaaaaaaaaaaaaaa",
				Title = "Ch",
				TitleSlug = "ch",
				PlaylistFolder = false
			});
			db.Playlists.Add(new PlaylistEntity
			{
				Id = 10,
				ChannelId = 1,
				YoutubePlaylistId = "PLy",
				Title = "P",
				Monitored = true,
				SeasonIndex = 2,
				SeasonIndexLocked = true
			});
			var t0 = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
			db.Videos.Add(new VideoEntity
			{
				Id = 101,
				ChannelId = 1,
				YoutubeVideoId = "ccccccccccc",
				Title = "A",
				UploadDateUtc = t0,
				Monitored = true,
				PlexEpisodeIndex = 7,
				PlexSeasonIndex = 2,
				PlexPrimaryPlaylistId = 10
			});
			db.PlaylistVideos.Add(new PlaylistVideoEntity { PlaylistId = 10, VideoId = 101, Position = 0 });
			await db.SaveChangesAsync();

			var n = await NfoPlaylistEpisodeResolver.ResolveEpisodeNumberAsync(db, 10, 101, default);
			Assert.Equal(1, n);
		}
		finally
		{
			TryDelete(dbPath);
		}
	}

	[Fact]
	public async Task plex_index_locked_keeps_db_episode_even_if_playlist_position_differs()
	{
		var dbPath = CreateTempDbPath();
		try
		{
			var options = new DbContextOptionsBuilder<TubeArrDbContext>()
				.UseSqlite("Data Source=" + dbPath)
				.Options;

			await using var db = new TubeArrDbContext(options);
			await db.Database.MigrateAsync();

			db.Channels.Add(new ChannelEntity
			{
				Id = 1,
				YoutubeChannelId = "UCplexlockedplaylistepresolverrr",
				Title = "Ch",
				TitleSlug = "ch",
				PlaylistFolder = false
			});
			db.Playlists.Add(new PlaylistEntity
			{
				Id = 10,
				ChannelId = 1,
				YoutubePlaylistId = "PLz",
				Title = "P",
				Monitored = true,
				SeasonIndex = 2,
				SeasonIndexLocked = true
			});
			var t0 = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
			db.Videos.Add(new VideoEntity
			{
				Id = 101,
				ChannelId = 1,
				YoutubeVideoId = "ggggggggggg",
				Title = "A",
				UploadDateUtc = t0,
				Monitored = true,
				PlexEpisodeIndex = 7,
				PlexSeasonIndex = 2,
				PlexPrimaryPlaylistId = 10,
				PlexIndexLocked = true
			});
			db.PlaylistVideos.Add(new PlaylistVideoEntity { PlaylistId = 10, VideoId = 101, Position = 0 });
			await db.SaveChangesAsync();

			var n = await NfoPlaylistEpisodeResolver.ResolveEpisodeNumberAsync(db, 10, 101, default);
			Assert.Equal(7, n);
		}
		finally
		{
			TryDelete(dbPath);
		}
	}

	[Fact]
	public async Task custom_playlist_season_uses_upload_order_not_native_primary_playlist_order()
	{
		var dbPath = CreateTempDbPath();
		try
		{
			var options = new DbContextOptionsBuilder<TubeArrDbContext>()
				.UseSqlite("Data Source=" + dbPath)
				.Options;

			await using var db = new TubeArrDbContext(options);
			await db.Database.MigrateAsync();

			const int customSeason = NfoLibraryExporter.CustomPlaylistSeasonRangeStart + 1;

			db.Channels.Add(new ChannelEntity
			{
				Id = 1,
				YoutubeChannelId = "UCcustomplaylisteporderaaaaaaaa",
				Title = "Ch",
				TitleSlug = "ch",
				PlaylistFolder = true
			});
			db.Playlists.Add(new PlaylistEntity
			{
				Id = 10,
				ChannelId = 1,
				YoutubePlaylistId = "PLnativeprimary",
				Title = "Native",
				Monitored = true,
				SeasonIndex = 2,
				SeasonIndexLocked = true
			});
			var tEarly = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
			var tLate = new DateTimeOffset(2020, 1, 5, 0, 0, 0, TimeSpan.Zero);
			db.Videos.Add(new VideoEntity
			{
				Id = 301,
				ChannelId = 1,
				YoutubeVideoId = "hhhhhhhhhhh",
				Title = "Later upload",
				UploadDateUtc = tLate,
				Monitored = true,
				PlexSeasonIndex = customSeason,
				PlexEpisodeIndex = 2,
				PlexPrimaryCustomPlaylistId = 1,
				PlexPrimaryPlaylistId = null
			});
			db.Videos.Add(new VideoEntity
			{
				Id = 302,
				ChannelId = 1,
				YoutubeVideoId = "iiiiiiiiiii",
				Title = "Earlier upload",
				UploadDateUtc = tEarly,
				Monitored = true,
				PlexSeasonIndex = customSeason,
				PlexEpisodeIndex = 1,
				PlexPrimaryCustomPlaylistId = 1,
				PlexPrimaryPlaylistId = null
			});
			// Native playlist order is opposite of upload order; episode numbers must follow StableTvNumbering (upload).
			db.PlaylistVideos.Add(new PlaylistVideoEntity { PlaylistId = 10, VideoId = 301, Position = 0 });
			db.PlaylistVideos.Add(new PlaylistVideoEntity { PlaylistId = 10, VideoId = 302, Position = 1 });
			await db.SaveChangesAsync();

			var n301 = await NfoPlaylistEpisodeResolver.ResolveEpisodeNumberAsync(db, 10, 301, default);
			var n302 = await NfoPlaylistEpisodeResolver.ResolveEpisodeNumberAsync(db, 10, 302, default);
			Assert.Equal(2, n301);
			Assert.Equal(1, n302);
		}
		finally
		{
			TryDelete(dbPath);
		}
	}

	static string CreateTempDbPath()
	{
		var root = Path.Combine(Path.GetTempPath(), "TubeArrTests");
		Directory.CreateDirectory(root);
		return Path.Combine(root, $"nfo-ep-resolver-{Guid.NewGuid():N}.sqlite");
	}

	static void TryDelete(string path)
	{
		try
		{
			if (File.Exists(path))
				File.Delete(path);
		}
		catch
		{
		}
	}
}
