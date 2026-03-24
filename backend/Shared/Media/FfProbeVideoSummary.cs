using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TubeArr.Backend.Media;

/// <summary>
/// Best-effort video stream summary via ffprobe (same install as FFmpeg).
/// </summary>
public static class FfProbeVideoSummary
{
	sealed class FfRoot
	{
		[JsonPropertyName("streams")]
		public List<FfStream>? Streams { get; set; }
	}

	sealed class FfStream
	{
		[JsonPropertyName("codec_type")]
		public string? CodecType { get; set; }

		[JsonPropertyName("codec_name")]
		public string? CodecName { get; set; }

		[JsonPropertyName("width")]
		public int? Width { get; set; }

		[JsonPropertyName("height")]
		public int? Height { get; set; }

		[JsonPropertyName("r_frame_rate")]
		public string? RFrameRate { get; set; }
	}

	public sealed record Result(int? Width, int? Height, double? FrameRate, string? VideoCodec);

	public static Result? Probe(string mediaPath, string? ffmpegExecutablePath)
	{
		var ffprobe = ResolveFfprobePath(ffmpegExecutablePath);
		if (string.IsNullOrWhiteSpace(ffprobe) || !File.Exists(ffprobe))
			return null;

		if (string.IsNullOrWhiteSpace(mediaPath) || !File.Exists(mediaPath))
			return null;

		try
		{
			var startInfo = new ProcessStartInfo
			{
				FileName = ffprobe,
				Arguments = $"-v error -select_streams v:0 -print_format json -show_streams \"{mediaPath}\"",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			};

			using var process = Process.Start(startInfo);
			if (process is null)
				return null;

			if (!process.WaitForExit(20_000))
			{
				try { process.Kill(true); } catch { }
				return null;
			}

			if (process.ExitCode != 0)
				return null;

			var json = process.StandardOutput.ReadToEnd();
			if (string.IsNullOrWhiteSpace(json))
				return null;

			var root = JsonSerializer.Deserialize<FfRoot>(json);
			var stream = root?.Streams?.FirstOrDefault(s =>
				string.Equals(s.CodecType, "video", StringComparison.OrdinalIgnoreCase));
			if (stream is null)
				return null;

			var fps = ParseFrameRate(stream.RFrameRate);
			var codec = NormalizeCodecName(stream.CodecName);
			return new Result(stream.Width, stream.Height, fps, codec);
		}
		catch
		{
			return null;
		}
	}

	public static string BuildQualityLabel(int? height, double? frameRate)
	{
		var res = FormatResolutionLabel(height);
		var fps = FormatFpsLabel(frameRate);
		if (string.IsNullOrEmpty(res) && string.IsNullOrEmpty(fps))
			return string.Empty;
		if (string.IsNullOrEmpty(res))
			return fps;
		if (string.IsNullOrEmpty(fps))
			return res;
		return $"{res} {fps}";
	}

	public static string FormatCodecContainerLabel(string? videoCodec, string? containerExtension)
	{
		var c = (videoCodec ?? "").Trim().ToLowerInvariant();
		var ext = (containerExtension ?? "").Trim().TrimStart('.').ToLowerInvariant();
		if (string.IsNullOrEmpty(c) && string.IsNullOrEmpty(ext))
			return string.Empty;
		if (string.IsNullOrEmpty(c))
			return ext;
		if (string.IsNullOrEmpty(ext))
			return c;
		return $"{c}/{ext}";
	}

	static string FormatResolutionLabel(int? height)
	{
		if (!height.HasValue || height.Value <= 0)
			return string.Empty;
		return $"{height.Value}p";
	}

	static string FormatFpsLabel(double? fps)
	{
		if (!fps.HasValue || !double.IsFinite(fps.Value) || fps.Value <= 0)
			return string.Empty;

		var v = fps.Value;
		var rounded = Math.Round(v);
		if (Math.Abs(v - rounded) < 0.05)
			return $"{(int)rounded}fps";
		return $"{v:0.##}fps";
	}

	static double? ParseFrameRate(string? rFrameRate)
	{
		if (string.IsNullOrWhiteSpace(rFrameRate))
			return null;

		var parts = rFrameRate.Split('/');
		if (parts.Length != 2)
			return null;

		if (!double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float,
			    System.Globalization.CultureInfo.InvariantCulture, out var num))
			return null;

		if (!double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float,
			    System.Globalization.CultureInfo.InvariantCulture, out var den) || den == 0)
			return null;

		var q = num / den;
		return double.IsFinite(q) && q > 0 ? q : null;
	}

	static string? NormalizeCodecName(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
			return null;

		var n = raw.Trim().ToLowerInvariant();
		return n switch
		{
			"avc1" or "avc" => "h264",
			"h265" or "hevc" or "hev1" => "hevc",
			_ => n
		};
	}

	static string? ResolveFfprobePath(string? ffmpegLocation)
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
