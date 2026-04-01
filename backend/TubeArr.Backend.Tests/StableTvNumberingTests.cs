using Microsoft.EntityFrameworkCore;
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
			Assert.Equal(4, s3);
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
}

