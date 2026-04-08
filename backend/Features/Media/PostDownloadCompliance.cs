using System.IO;
using System.Text.Json;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;
using TubeArr.Backend.Media;

namespace TubeArr.Backend;

public enum PostDownloadDecision
{
	ImportAsIs,
	RemuxWithCopy,
	Transcode,
	Rejected,
	ManualReview
}

/// <summary>ffprobe-driven acceptance vs quality profile after an external file lands on disk.</summary>
public static class PostDownloadCompliance
{
	public static PostDownloadDecision Evaluate(
		string mediaPath,
		QualityProfileEntity profile,
		SlskdConfigEntity slskdCfg,
		string? ffmpegExecutablePath,
		out string? reason)
	{
		reason = null;
		if (string.IsNullOrWhiteSpace(mediaPath) || !File.Exists(mediaPath))
		{
			reason = "File missing.";
			return PostDownloadDecision.Rejected;
		}

		var probe = FfProbeMediaProbe.Probe(mediaPath, ffmpegExecutablePath);
		if (probe is null)
		{
			reason = "ffprobe could not read the file.";
			return PostDownloadDecision.ManualReview;
		}

		if (string.IsNullOrEmpty(probe.MediaInfo.VideoCodec) || probe.MediaInfo.VideoCodec == "unknown")
		{
			reason = "No video stream.";
			return PostDownloadDecision.Rejected;
		}

		var height = TryParseHeight(probe.MediaInfo.Resolution);
		var fps = probe.MediaInfo.VideoFps;

		if (profile.MinHeight is > 0 && height is > 0 && height < profile.MinHeight && profile.FailIfBelowMinHeight)
		{
			reason = $"Height {height} below profile minimum {profile.MinHeight}.";
			return PostDownloadDecision.Rejected;
		}

		if (profile.MinFps is > 0 && fps > 0 && fps + 0.5 < profile.MinFps)
		{
			reason = $"FPS {fps:0.##} below profile minimum {profile.MinFps}.";
			return PostDownloadDecision.Rejected;
		}

		if (profile.MaxFps is > 0 && fps > 0 && fps - 0.5 > profile.MaxFps)
		{
			reason = $"FPS {fps:0.##} above profile maximum {profile.MaxFps}.";
			return PostDownloadDecision.ManualReview;
		}

		if (profile.MaxHeight is > 0 && height is > 0 && height > profile.MaxHeight)
		{
			if (slskdCfg.HigherQualityHandling == 1)
			{
				reason = $"Height {height} exceeds profile max {profile.MaxHeight}; normalization requested.";
				return PostDownloadDecision.Transcode;
			}
		}

		var ext = Path.GetExtension(mediaPath).TrimStart('.').ToLowerInvariant();
		if (!IsContainerAllowed(profile, ext))
		{
			reason = $"Container .{ext} not in profile allowed list.";
			return PostDownloadDecision.RemuxWithCopy;
		}

		if (!IsVideoCodecAllowed(profile, probe.MediaInfo.VideoCodec))
		{
			reason = $"Video codec {probe.MediaInfo.VideoCodec} not allowed by profile.";
			return PostDownloadDecision.Transcode;
		}

		return PostDownloadDecision.ImportAsIs;
	}

	public static PostDownloadDecision ApplyTranscodeReviewPolicy(PostDownloadDecision d, SlskdConfigEntity cfg)
	{
		if (!cfg.RequireManualReviewOnTranscode)
			return d;
		if (d is PostDownloadDecision.RemuxWithCopy or PostDownloadDecision.Transcode)
			return PostDownloadDecision.ManualReview;
		return d;
	}

	static int? TryParseHeight(string resolution)
	{
		if (string.IsNullOrWhiteSpace(resolution))
			return null;
		var parts = resolution.Split('x', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (parts.Length != 2)
			return null;
		if (int.TryParse(parts[1], out var h))
			return h;
		return null;
	}

	static bool IsContainerAllowed(QualityProfileEntity profile, string ext)
	{
		var list = ParseJsonArray(profile.AllowedContainersJson);
		if (list.Count == 0)
			return true;
		return list.Contains(ext, StringComparer.OrdinalIgnoreCase);
	}

	static bool IsVideoCodecAllowed(QualityProfileEntity profile, string codecRaw)
	{
		var list = ParseJsonArray(profile.AllowedVideoCodecsJson);
		if (list.Count == 0)
			return true;
		var c = (codecRaw ?? "").Trim().ToLowerInvariant();
		foreach (var token in list)
		{
			var t = token.Trim().ToLowerInvariant();
			if (t == "avc" && c is "h264" or "avc" or "avc1")
				return true;
			if (t == c)
				return true;
		}

		return false;
	}

	static List<string> ParseJsonArray(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
			return new List<string>();
		try
		{
			return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
		}
		catch
		{
			return new List<string>();
		}
	}
}
