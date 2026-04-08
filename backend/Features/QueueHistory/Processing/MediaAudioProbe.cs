using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TubeArr.Backend;

/// <summary>ffprobe-based audio stream detection for downloaded media.</summary>
public static class MediaAudioProbe
{
	public static (bool probeRan, bool hasAudio, string? error) ProbeHasAudioStream(string mediaPath, string? ffmpegLocation, ILogger? logger)
	{
		try
		{
			var ffprobePath = ResolveFfprobePath(ffmpegLocation);
			if (string.IsNullOrWhiteSpace(ffprobePath))
				return (false, false, "ffprobe path unavailable");

			if (!File.Exists(ffprobePath))
				return (false, false, $"ffprobe not found: {ffprobePath}");

			var startInfo = new ProcessStartInfo
			{
				FileName = ffprobePath,
				Arguments = $"-v error -select_streams a -show_entries stream=codec_type -of csv=p=0 \"{mediaPath}\"",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = false,
				CreateNoWindow = true
			};

			using var process = Process.Start(startInfo);
			if (process is null)
				return (false, false, "failed to start ffprobe");

			var readTask = Task.Run(() => process.StandardOutput.ReadToEnd());
			if (!readTask.Wait(TimeSpan.FromSeconds(15)))
			{
				try { process.Kill(true); } catch { /* best-effort */ }
				return (false, false, "ffprobe timed out");
			}

			string stdout;
			try
			{
				stdout = readTask.Result;
			}
			catch
			{
				return (false, false, "ffprobe read failed");
			}

			process.WaitForExit();
			var hasAudio = process.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdout);
			return (true, hasAudio, process.ExitCode == 0 ? null : $"ffprobe exit code {process.ExitCode}");
		}
		catch (Exception ex)
		{
			logger?.LogDebug(ex, "Audio stream probe failed for path={Path}", mediaPath);
			return (false, false, ex.Message);
		}
	}

	public static string? ResolveFfprobePath(string? ffmpegLocation)
	{
		if (string.IsNullOrWhiteSpace(ffmpegLocation))
			return null;

		var location = ffmpegLocation.Trim().Trim('"');
		if (string.IsNullOrWhiteSpace(location))
			return null;

		if (Directory.Exists(location))
			return Path.Combine(location, OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");

		if (File.Exists(location))
		{
			var directory = Path.GetDirectoryName(location);
			if (string.IsNullOrWhiteSpace(directory))
				return null;

			return Path.Combine(directory, OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");
		}

		var asDirectory = Path.GetDirectoryName(location);
		if (string.IsNullOrWhiteSpace(asDirectory))
			return null;

		return Path.Combine(asDirectory, OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");
	}
}
