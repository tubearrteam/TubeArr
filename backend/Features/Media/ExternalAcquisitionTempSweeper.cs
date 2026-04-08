using Microsoft.Extensions.Logging;

namespace TubeArr.Backend;

/// <summary>Removes abandoned <c>tubearr-external-*</c> work directories under the system temp folder.</summary>
static class ExternalAcquisitionTempSweeper
{
	internal const string DirectoryPrefix = "tubearr-external-";
	static readonly TimeSpan MinAge = TimeSpan.FromHours(24);

	internal static void TrySweep(ILogger logger, DateTimeOffset nowUtc)
	{
		var root = Path.GetTempPath();
		string[] dirs;
		try
		{
			dirs = Directory.GetDirectories(root, DirectoryPrefix + "*", SearchOption.TopDirectoryOnly);
		}
		catch (Exception ex)
		{
			logger.LogDebug(ex, "External acquisition temp sweep: could not list temp directory.");
			return;
		}

		foreach (var dir in dirs)
		{
			try
			{
				var lastWrite = Directory.GetLastWriteTimeUtc(dir);
				if (nowUtc.UtcDateTime - lastWrite < MinAge)
					continue;
				Directory.Delete(dir, recursive: true);
				logger.LogInformation("Removed stale external acquisition temp dir {Path}", dir);
			}
			catch (Exception ex)
			{
				logger.LogDebug(ex, "External acquisition temp sweep: skip {Path}", dir);
			}
		}
	}
}
