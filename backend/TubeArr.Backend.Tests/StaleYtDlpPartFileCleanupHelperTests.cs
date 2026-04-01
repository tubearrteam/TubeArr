using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using TubeArr.Backend.Data;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class StaleYtDlpPartFileCleanupHelperTests
{
	[Fact]
	public void Cleanup_deletes_stale_part_files_and_keeps_recent()
	{
		var temp = Path.Combine(Path.GetTempPath(), "tubearr-part-test-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(temp);
		try
		{
			var stale = Path.Combine(temp, "video.mp4.part");
			var recent = Path.Combine(temp, "other.mkv.part");
			File.WriteAllText(stale, "x");
			File.WriteAllText(recent, "y");
			File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddHours(-48));
			File.SetLastWriteTimeUtc(recent, DateTime.UtcNow);

			var roots = new List<RootFolderEntity> { new() { Id = 1, Path = temp } };
			var (deleted, errors, _) = StaleYtDlpPartFileCleanupHelper.Cleanup(
				roots,
				temp,
				NullLogger.Instance,
				TimeSpan.FromHours(24),
				reportProgress: null,
				CancellationToken.None);

			Assert.Equal(0, errors);
			Assert.Equal(1, deleted);
			Assert.False(File.Exists(stale));
			Assert.True(File.Exists(recent));
		}
		finally
		{
			try
			{
				Directory.Delete(temp, true);
			}
			catch
			{
				// best-effort temp cleanup
			}
		}
	}
}
