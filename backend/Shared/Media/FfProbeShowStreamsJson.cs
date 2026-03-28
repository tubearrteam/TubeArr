using System.Diagnostics;
using System.Threading.Tasks;

namespace TubeArr.Backend.Media;

/// <summary>
/// Shared ffprobe invocation: same CLI as <c>ffprobe -v error -print_format json -show_streams</c>,
/// with async stdout drain to avoid pipe deadlocks on Windows.
/// </summary>
internal static class FfProbeShowStreamsJson
{
	internal static string? Run(string ffprobePath, string mediaPath, TimeSpan timeout)
	{
		if (string.IsNullOrWhiteSpace(ffprobePath) || !File.Exists(ffprobePath))
			return null;

		if (string.IsNullOrWhiteSpace(mediaPath) || !File.Exists(mediaPath))
			return null;

		try
		{
			var startInfo = new ProcessStartInfo
			{
				FileName = ffprobePath,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = false,
				CreateNoWindow = true
			};
			startInfo.ArgumentList.Add("-v");
			startInfo.ArgumentList.Add("error");
			startInfo.ArgumentList.Add("-print_format");
			startInfo.ArgumentList.Add("json");
			startInfo.ArgumentList.Add("-show_streams");
			startInfo.ArgumentList.Add(mediaPath);

			using var process = Process.Start(startInfo);
			if (process is null)
				return null;

			var readTask = Task.Run(() => process.StandardOutput.ReadToEnd());
			if (!readTask.Wait(timeout))
			{
				try { process.Kill(true); } catch { }
				return null;
			}

			string json;
			try
			{
				json = readTask.Result;
			}
			catch
			{
				return null;
			}

			process.WaitForExit();
			if (process.ExitCode != 0)
				return null;

			return string.IsNullOrWhiteSpace(json) ? null : json;
		}
		catch
		{
			return null;
		}
	}
}
