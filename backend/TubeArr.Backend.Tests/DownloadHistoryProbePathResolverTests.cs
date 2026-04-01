using TubeArr.Backend;
using TubeArr.Backend.Data;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class DownloadHistoryProbePathResolverTests
{
	[Fact]
	public void Failed_row_does_not_use_latest_library_file_when_output_missing()
	{
		var dir = Path.Combine(Path.GetTempPath(), "tubearr-hist-probe-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try
		{
			var lib = Path.Combine(dir, "lib.mkv");
			File.WriteAllText(lib, "x");

			var h = new DownloadHistoryEntity
			{
				VideoId = 1,
				EventType = 4,
				OutputPath = Path.Combine(dir, "gone.mkv")
			};
			var latest = new Dictionary<int, string?> { [1] = lib };

			Assert.Null(DownloadHistoryProbePathResolver.Resolve(h, latest));
		}
		finally
		{
			try { Directory.Delete(dir, true); } catch { /* best-effort */ }
		}
	}

	[Fact]
	public void Imported_row_uses_latest_library_file_when_output_missing()
	{
		var dir = Path.Combine(Path.GetTempPath(), "tubearr-hist-probe-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try
		{
			var lib = Path.Combine(dir, "lib.mkv");
			File.WriteAllText(lib, "x");

			var h = new DownloadHistoryEntity
			{
				VideoId = 2,
				EventType = 3,
				OutputPath = Path.Combine(dir, "moved.mkv")
			};
			var latest = new Dictionary<int, string?> { [2] = lib };

			Assert.Equal(lib, DownloadHistoryProbePathResolver.Resolve(h, latest));
		}
		finally
		{
			try { Directory.Delete(dir, true); } catch { /* best-effort */ }
		}
	}

	[Fact]
	public void Any_row_prefers_existing_output_path()
	{
		var dir = Path.Combine(Path.GetTempPath(), "tubearr-hist-probe-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try
		{
			var outPath = Path.Combine(dir, "out.mkv");
			File.WriteAllText(outPath, "x");
			var other = Path.Combine(dir, "other.mkv");
			File.WriteAllText(other, "y");

			var h = new DownloadHistoryEntity
			{
				VideoId = 3,
				EventType = 4,
				OutputPath = outPath
			};
			var latest = new Dictionary<int, string?> { [3] = other };

			Assert.Equal(outPath, DownloadHistoryProbePathResolver.Resolve(h, latest));
		}
		finally
		{
			try { Directory.Delete(dir, true); } catch { /* best-effort */ }
		}
	}
}
