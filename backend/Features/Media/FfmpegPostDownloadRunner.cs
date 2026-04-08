using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace TubeArr.Backend;

/// <summary>Best-effort remux/transcode for post-download compliance.</summary>
public static class FfmpegPostDownloadRunner
{
	public static async Task<(bool Ok, string? OutputPath, string? Error)> RemuxCopyAsync(
		string ffmpegExecutablePath,
		string inputPath,
		string outputPath,
		CancellationToken ct,
		ILogger? logger = null)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
		var args = $"-y -hide_banner -loglevel error -i \"{EscapeArg(inputPath)}\" -c copy \"{EscapeArg(outputPath)}\"";
		return await RunAsync(ffmpegExecutablePath, args, inputPath, outputPath, ct, logger);
	}

	public static async Task<(bool Ok, string? OutputPath, string? Error)> TranscodeToMaxHeightAsync(
		string ffmpegExecutablePath,
		string inputPath,
		string outputPath,
		int maxHeight,
		CancellationToken ct,
		ILogger? logger = null)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
		var vfArg = maxHeight > 0 ? $"-vf scale=-2:{maxHeight}" : "";
		var args = $"-y -hide_banner -loglevel error -i \"{EscapeArg(inputPath)}\" {vfArg} -c:v libx264 -crf 20 -c:a aac -b:a 192k \"{EscapeArg(outputPath)}\"";
		return await RunAsync(ffmpegExecutablePath, args.Trim(), inputPath, outputPath, ct, logger);
	}

	static string EscapeArg(string p) => p.Replace("\"", "\\\"", StringComparison.Ordinal);

	static async Task<(bool Ok, string? OutputPath, string? Error)> RunAsync(
		string ffmpegExecutablePath,
		string argString,
		string inputPath,
		string outputPath,
		CancellationToken ct,
		ILogger? logger)
	{
		try
		{
			if (File.Exists(outputPath))
				File.Delete(outputPath);
		}
		catch
		{
			/* ignore */
		}

		using var proc = new Process
		{
			StartInfo = new ProcessStartInfo
			{
				FileName = ffmpegExecutablePath,
				Arguments = argString,
				UseShellExecute = false,
				RedirectStandardError = true,
				RedirectStandardOutput = true,
				CreateNoWindow = true
			}
		};
		try
		{
			proc.Start();
		}
		catch (Exception ex)
		{
			return (false, null, ex.Message);
		}

		var err = await proc.StandardError.ReadToEndAsync(ct);
		await proc.WaitForExitAsync(ct);
		if (proc.ExitCode != 0 || !File.Exists(outputPath))
		{
			logger?.LogWarning("ffmpeg failed exit={Code} stderr={Err}", proc.ExitCode, err);
			return (false, null, string.IsNullOrWhiteSpace(err) ? "ffmpeg failed" : err);
		}

		return (true, outputPath, null);
	}
}
