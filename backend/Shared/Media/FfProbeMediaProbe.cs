using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TubeArr.Backend.Contracts;

namespace TubeArr.Backend.Media;

/// <summary>Reads container/video/audio summary from ffprobe JSON for on-disk media files.</summary>
public static class FfProbeMediaProbe
{
	public static VideoFileMediaProbePayload? Probe(string mediaPath, string? ffmpegExecutablePath)
	{
		var ffprobe = FfProbeVideoSummary.GetFfprobePath(ffmpegExecutablePath);
		if (string.IsNullOrWhiteSpace(ffprobe) || !File.Exists(ffprobe))
			return null;

		if (string.IsNullOrWhiteSpace(mediaPath) || !File.Exists(mediaPath))
			return null;

		try
		{
			var startInfo = new ProcessStartInfo
			{
				FileName = ffprobe,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				// Do not redirect stderr unless consumed — a full stderr pipe can deadlock the child on Windows.
				RedirectStandardError = false,
				CreateNoWindow = true
			};
			// Matches CLI: ffprobe -v error -print_format json -show_streams "<path>"
			startInfo.ArgumentList.Add("-v");
			startInfo.ArgumentList.Add("error");
			startInfo.ArgumentList.Add("-print_format");
			startInfo.ArgumentList.Add("json");
			startInfo.ArgumentList.Add("-show_streams");
			startInfo.ArgumentList.Add(mediaPath);

			using var process = Process.Start(startInfo);
			if (process is null)
				return null;

			// Must drain stdout (or async-read) before WaitForExit; otherwise stdout/stderr pipes can deadlock.
			var readTask = Task.Run(() => process.StandardOutput.ReadToEnd());
			if (!readTask.Wait(TimeSpan.FromSeconds(60)))
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

			process.WaitForExit(); // reap; exit code is set after stdout closes
			if (process.ExitCode != 0)
				return null;

			if (string.IsNullOrWhiteSpace(json))
				return null;

			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;

			if (!root.TryGetProperty("streams", out var streamsEl) || streamsEl.ValueKind != JsonValueKind.Array)
				return null;

			if (streamsEl.GetArrayLength() == 0)
				return null;

			var durationSeconds = 0;
			var formatName = "";
			if (root.TryGetProperty("format", out var formatEl))
			{
				durationSeconds = ParseDurationSecondsFromContainer(formatEl);
				if (formatEl.TryGetProperty("format_name", out var fn) && fn.ValueKind == JsonValueKind.String)
					formatName = fn.GetString() ?? "";
			}

			JsonElement? videoStream = null;
			JsonElement? audioStream = null;
			var audioCount = 0;
			foreach (var s in streamsEl.EnumerateArray())
			{
				if (!s.TryGetProperty("codec_type", out var ct) || ct.ValueKind != JsonValueKind.String)
					continue;
				var t = ct.GetString();
				if (string.Equals(t, "video", StringComparison.OrdinalIgnoreCase) && videoStream is null)
					videoStream = s;
				else if (string.Equals(t, "audio", StringComparison.OrdinalIgnoreCase))
				{
					audioCount++;
					if (audioStream is null)
						audioStream = s;
				}
			}

			if (durationSeconds == 0 && videoStream is { } vsDur)
				durationSeconds = ParseDurationSecondsFromContainer(vsDur);
			if (durationSeconds == 0 && audioStream is { } asDur)
				durationSeconds = ParseDurationSecondsFromContainer(asDur);

			var vCodec = "";
			int? width = null;
			int? height = null;
			double? fps = null;
			if (videoStream is { } vs)
			{
				if (vs.TryGetProperty("codec_name", out var cn) && cn.ValueKind == JsonValueKind.String)
					vCodec = NormalizeVideoCodec(cn.GetString());
				if (vs.TryGetProperty("width", out var w) && w.TryGetInt32(out var wi))
					width = wi;
				if (vs.TryGetProperty("height", out var h) && h.TryGetInt32(out var hi))
					height = hi;
				if (vs.TryGetProperty("r_frame_rate", out var rf) && rf.ValueKind == JsonValueKind.String)
					fps = ParseFrameRate(rf.GetString());
			}

			var aCodec = "";
			double channels = 0;
			var langParts = new List<string>();
			if (audioStream is { } au)
			{
				if (au.TryGetProperty("codec_name", out var ac) && ac.ValueKind == JsonValueKind.String)
					aCodec = (ac.GetString() ?? "").Trim().ToLowerInvariant();
				if (au.TryGetProperty("channels", out var ch) && ch.ValueKind == JsonValueKind.Number && ch.TryGetDouble(out var chd))
					channels = chd;
				if (au.TryGetProperty("tags", out var atags) && atags.ValueKind == JsonValueKind.Object &&
				    atags.TryGetProperty("language", out var alang) && alang.ValueKind == JsonValueKind.String)
				{
					var l = alang.GetString();
					if (!string.IsNullOrWhiteSpace(l))
						langParts.Add(l.Trim());
				}
			}

			var containerLabel = PrimaryFormatToken(formatName);
			var codecLabel = string.IsNullOrEmpty(vCodec) ? containerLabel : vCodec;
			var formats = new List<VideoFileProbeFormatLabel>();
			if (!string.IsNullOrWhiteSpace(codecLabel) || !string.IsNullOrWhiteSpace(containerLabel))
			{
				var label = string.IsNullOrWhiteSpace(containerLabel)
					? codecLabel
					: string.IsNullOrWhiteSpace(codecLabel)
						? containerLabel
						: $"{codecLabel} · {containerLabel}";
				formats.Add(new VideoFileProbeFormatLabel { Id = 1, Name = label });
			}

			var resolution = "";
			if (width.HasValue && height.HasValue && width > 0 && height > 0)
				resolution = $"{width}x{height}";

			var runTime = durationSeconds > 0
				? TimeSpan.FromSeconds(durationSeconds).ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
				: "";

			var snap = new VideoFileMediaInfoSnapshot
			{
				AudioBitrate = 0,
				AudioChannels = channels,
				AudioCodec = aCodec,
				AudioLanguages = string.Join("/", langParts.Distinct(StringComparer.OrdinalIgnoreCase)),
				AudioStreamCount = audioCount,
				VideoBitrate = 0,
				VideoCodec = vCodec,
				VideoFps = fps ?? 0,
				VideoDynamicRange = "",
				VideoDynamicRangeType = "",
				Resolution = resolution,
				RunTime = runTime,
				ScanType = "",
				Subtitles = ""
			};

			return new VideoFileMediaProbePayload
			{
				DurationSeconds = durationSeconds,
				MediaInfo = snap,
				CustomFormats = formats.ToArray()
			};
		}
		catch
		{
			return null;
		}
	}

	static int ParseDurationSecondsFromContainer(JsonElement el)
	{
		if (!el.TryGetProperty("duration", out var durEl))
			return 0;

		if (durEl.ValueKind == JsonValueKind.String &&
		    double.TryParse(durEl.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d0))
			return (int)Math.Round(Math.Max(0, d0));
		if (durEl.ValueKind == JsonValueKind.Number && durEl.TryGetDouble(out var d1))
			return (int)Math.Round(Math.Max(0, d1));

		return 0;
	}

	static string PrimaryFormatToken(string formatName)
	{
		if (string.IsNullOrWhiteSpace(formatName))
			return "";
		var first = formatName.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
		return first?.ToLowerInvariant() ?? "";
	}

	static string NormalizeVideoCodec(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
			return "";
		var n = raw.Trim().ToLowerInvariant();
		return n switch
		{
			"avc1" or "avc" => "h264",
			"h265" or "hevc" or "hev1" => "hevc",
			"av01" => "av1",
			_ => n
		};
	}

	static double? ParseFrameRate(string? rFrameRate)
	{
		if (string.IsNullOrWhiteSpace(rFrameRate))
			return null;
		var parts = rFrameRate.Split('/');
		if (parts.Length != 2)
			return null;
		if (!double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
			return null;
		if (!double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var den) || den == 0)
			return null;
		var q = num / den;
		return double.IsFinite(q) && q > 0 ? q : null;
	}
}
