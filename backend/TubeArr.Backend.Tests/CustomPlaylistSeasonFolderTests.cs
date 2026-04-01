using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Data;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class CustomPlaylistSeasonFolderTests
{
	[Fact]
	public async Task ResolveSeasonNumberForPlaylistFolder_uses_10001_range_for_first_matching_custom_playlist()
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
				YoutubeChannelId = "UCtestchannelid00000000000000001",
				Title = "Ch",
				TitleSlug = "ch",
				PlaylistFolder = true
			});
			db.Playlists.Add(new PlaylistEntity
			{
				Id = 1,
				ChannelId = 1,
				YoutubePlaylistId = "PLnative00000000000000000000001",
				Title = "Native",
				Monitored = true
			});
			db.Videos.Add(new VideoEntity
			{
				Id = 1,
				ChannelId = 1,
				YoutubeVideoId = "vidaaaaaaaaaa",
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
			var video = await db.Videos.AsNoTracking().FirstAsync();
			var (season, src) = await NfoLibraryExporter.ResolveSeasonNumberForPlaylistFolderAsync(db, 1, video, 1, default);
			Assert.Equal(NfoLibraryExporter.CustomPlaylistSeasonRangeStart + 1, season);
			Assert.NotNull(src);
			Assert.Equal("Custom A", src!.Name);
		}
	}

	[Fact]
	public async Task ResolveSeasonNumberForPlaylistFolder_falls_back_to_native_when_custom_does_not_match()
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
				YoutubeChannelId = "UCtestchannelid00000000000000002",
				Title = "Ch",
				TitleSlug = "ch",
				PlaylistFolder = true
			});
			db.Playlists.Add(new PlaylistEntity
			{
				Id = 1,
				ChannelId = 1,
				YoutubePlaylistId = "PLnative00000000000000000000002",
				Title = "Native",
				Monitored = true
			});
			db.Videos.Add(new VideoEntity
			{
				Id = 1,
				ChannelId = 1,
				YoutubeVideoId = "vidbbbbbbbbbb",
				Title = "Other",
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
			var video = await db.Videos.AsNoTracking().FirstAsync();
			var (season, src) = await NfoLibraryExporter.ResolveSeasonNumberForPlaylistFolderAsync(db, 1, video, 1, default);
			Assert.Equal(2, season);
			Assert.Null(src);
		}
	}
}
