using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend;
using TubeArr.Backend.Data;
using TubeArr.Backend.Media;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class StableTvNumberingTests
{
	[Fact]
	public async Task playlist_seasonIndex_does_not_reuse_deleted_numbers()
	{
		var dbPath = CreateTempDbPath();
		try
		{
			var options = new DbContextOptionsBuilder<TubeArrDbContext>()
				.UseSqlite("Data Source=" + dbPath)
				.Options;

			await using var db = new TubeArrDbContext(options);
			await db.Database.MigrateAsync();

			var channel = new ChannelEntity { YoutubeChannelId = "UC_TEST", Title = "Test", TitleSlug = "test" };
			db.Channels.Add(channel);
			await db.SaveChangesAsync();

			var p1 = new PlaylistEntity { ChannelId = channel.Id, YoutubePlaylistId = "PL_A", Title = "A" };
			var p2 = new PlaylistEntity { ChannelId = channel.Id, YoutubePlaylistId = "PL_B", Title = "B" };
			db.Playlists.AddRange(p1, p2);
			await db.SaveChangesAsync();

			var s1 = await StableTvNumbering.EnsurePlaylistSeasonIndexAsync(db, p1.Id, default);
			var s2 = await StableTvNumbering.EnsurePlaylistSeasonIndexAsync(db, p2.Id, default);

			Assert.Equal(2, s1);
			Assert.Equal(3, s2);

			db.Playlists.Remove(p1);
			await db.SaveChangesAsync();

			var p3 = new PlaylistEntity { ChannelId = channel.Id, YoutubePlaylistId = "PL_C", Title = "C" };
			db.Playlists.Add(p3);
			await db.SaveChangesAsync();

			var s3 = await StableTvNumbering.EnsurePlaylistSeasonIndexAsync(db, p3.Id, default);
			// Remaining playlists are renumbered 2,3 after delete (priority/activity order), not max+1 gaps.
			Assert.Equal(3, s3);
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
		return Path.Combine(root, $"stable-tv-numbering-{Guid.NewGuid():N}.sqlite");
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

	[Fact]
	public async Task playlist_season_indices_follow_priority_then_title_tiebreak()
	{
		var dbPath = CreateTempDbPath();
		try
		{
			var options = new DbContextOptionsBuilder<TubeArrDbContext>()
				.UseSqlite("Data Source=" + dbPath)
				.Options;

			await using var db = new TubeArrDbContext(options);
			await db.Database.MigrateAsync();

			var channel = new ChannelEntity { YoutubeChannelId = "UC_PRIO", Title = "P", TitleSlug = "p" };
			db.Channels.Add(channel);
			await db.SaveChangesAsync();

			var z = new PlaylistEntity { ChannelId = channel.Id, YoutubePlaylistId = "PL_Z", Title = "Zebra", Priority = 1 };
			var a = new PlaylistEntity { ChannelId = channel.Id, YoutubePlaylistId = "PL_A", Title = "Alpha", Priority = 0 };
			db.Playlists.AddRange(z, a);
			await db.SaveChangesAsync();

			await StableTvNumbering.EnsureChannelPlaylistSeasonIndicesMatchPriorityAsync(db, channel.Id, default);

			var zRow = await db.Playlists.AsNoTracking().FirstAsync(p => p.Id == z.Id);
			var aRow = await db.Playlists.AsNoTracking().FirstAsync(p => p.Id == a.Id);
			Assert.Equal(2, aRow.SeasonIndex);
			Assert.Equal(3, zRow.SeasonIndex);

			a.Priority = 2;
			await db.SaveChangesAsync();

			await StableTvNumbering.EnsureChannelPlaylistSeasonIndicesMatchPriorityAsync(db, channel.Id, default);

			zRow = await db.Playlists.AsNoTracking().FirstAsync(p => p.Id == z.Id);
			aRow = await db.Playlists.AsNoTracking().FirstAsync(p => p.Id == a.Id);
			Assert.Equal(2, zRow.SeasonIndex);
			Assert.Equal(3, aRow.SeasonIndex);
		}
		finally
		{
			TryDelete(dbPath);
		}
	}

	[Fact]
	public async Task ensure_video_plex_indices_prefer_custom_playlist_folder_when_rules_match()
	{
		using var connection = new SqliteConnection("Data Source=:memory:");
		await connection.OpenAsync();

		var options = new DbContextOptionsBuilder<TubeArrDbContext>()
			.UseSqlite(connection)
			.Options;

		await using (var db = new TubeArrDbContext(options))
		{
			await db.Database.MigrateAsync();
			db.Channels.Add(new ChannelEntity
			{
				Id = 1,
				YoutubeChannelId = "UCstablecustomplex0000000001",
				Title = "Ch",
				TitleSlug = "ch",
				PlaylistFolder = true
			});
			db.Playlists.Add(new PlaylistEntity
			{
				Id = 1,
				ChannelId = 1,
				YoutubePlaylistId = "PLstablecustom00000000000001",
				Title = "Native",
				Monitored = true
			});
			db.Videos.Add(new VideoEntity
			{
				Id = 1,
				ChannelId = 1,
				YoutubeVideoId = "stableCstVid",
				Title = "MatchMe",
				UploadDateUtc = DateTimeOffset.UtcNow,
				Monitored = true
			});
			db.PlaylistVideos.Add(new PlaylistVideoEntity { PlaylistId = 1, VideoId = 1, Position = 0 });
			db.ChannelCustomPlaylists.Add(new ChannelCustomPlaylistEntity
			{
				Id = 1,
				ChannelId = 1,
				Name = "Custom A",
				Enabled = true,
				Priority = 0,
				MatchType = 0,
				RulesJson = """[{"field":"title","operator":"equals","value":"MatchMe"}]"""
			});
			await db.SaveChangesAsync();
		}

		await using (var db = new TubeArrDbContext(options))
		{
			await StableTvNumbering.EnsureVideoPlexIndicesAsync(db, 1, [1], default);
			var v = await db.Videos.AsNoTracking().FirstAsync(x => x.Id == 1);
			Assert.Equal(NfoLibraryExporter.CustomPlaylistSeasonRangeStart + 1, v.PlexSeasonIndex);
			Assert.Equal(1, v.PlexPrimaryCustomPlaylistId);
			Assert.Null(v.PlexPrimaryPlaylistId);
		}
	}

	[Fact]
	public async Task rebuild_channel_plex_indices_clears_unlocked_then_reassigns()
	{
		using var connection = new SqliteConnection("Data Source=:memory:");
		await connection.OpenAsync();

		var options = new DbContextOptionsBuilder<TubeArrDbContext>()
			.UseSqlite(connection)
			.Options;

		await using (var db = new TubeArrDbContext(options))
		{
			await db.Database.MigrateAsync();
			db.Channels.Add(new ChannelEntity
			{
				Id = 1,
				YoutubeChannelId = "UCrebuildplexindices00000001",
				Title = "Ch",
				TitleSlug = "ch",
				PlaylistFolder = true
			});
			db.Playlists.Add(new PlaylistEntity
			{
				Id = 1,
				ChannelId = 1,
				YoutubePlaylistId = "PLrebuildplex0000000000000001",
				Title = "Native",
				Monitored = true
			});
			db.Videos.Add(new VideoEntity
			{
				Id = 1,
				ChannelId = 1,
				YoutubeVideoId = "rebuildPlexVid1",
				Title = "Stale",
				UploadDateUtc = DateTimeOffset.UtcNow,
				Monitored = true,
				PlexSeasonIndex = 2,
				PlexEpisodeIndex = 9,
				PlexPrimaryPlaylistId = 1
			});
			db.PlaylistVideos.Add(new PlaylistVideoEntity { PlaylistId = 1, VideoId = 1, Position = 0 });
			db.ChannelCustomPlaylists.Add(new ChannelCustomPlaylistEntity
			{
				Id = 1,
				ChannelId = 1,
				Name = "Match stale title",
				Enabled = true,
				Priority = 0,
				MatchType = 0,
				RulesJson = """[{"field":"title","operator":"equals","value":"Stale"}]"""
			});
			await db.SaveChangesAsync();
		}

		await using (var db = new TubeArrDbContext(options))
		{
			var (cleared, total) = await StableTvNumbering.RebuildChannelPlexIndicesAsync(db, 1, default);
			Assert.Equal(1, cleared);
			Assert.Equal(1, total);
			var v = await db.Videos.AsNoTracking().FirstAsync(x => x.Id == 1);
			Assert.Equal(NfoLibraryExporter.CustomPlaylistSeasonRangeStart + 1, v.PlexSeasonIndex);
			Assert.Equal(1, v.PlexPrimaryCustomPlaylistId);
		}
	}
}

