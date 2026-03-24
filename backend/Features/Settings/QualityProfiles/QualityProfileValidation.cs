using System.Text.Json;
using TubeArr.Backend.Data;

namespace TubeArr.Backend.QualityProfile;

public static class QualityProfileValidation
{
	static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = false };

	public static IReadOnlyList<string> Validate(QualityProfileEntity profile)
	{
		var errors = new List<string>();

		if (profile.MinHeight.HasValue && profile.MaxHeight.HasValue && profile.MinHeight.Value > profile.MaxHeight.Value)
			errors.Add("minHeight cannot be greater than maxHeight.");

		if (profile.MinFps.HasValue && profile.MaxFps.HasValue && profile.MinFps.Value > profile.MaxFps.Value)
			errors.Add("minFps cannot be greater than maxFps.");

		if (profile.FallbackMode == 2 /* DegradeResolution */)
		{
			var steps = GetDegradeSteps(profile);
			if (steps.Count == 0 && (profile.MaxHeight ?? 0) > (profile.MinHeight ?? 0))
				errors.Add("degradeHeightSteps is empty but fallback mode requires resolution degradation; add steps or set steps between minHeight and maxHeight.");
		}

		var allowedVideo = ParseJsonStringList(profile.AllowedVideoCodecsJson);
		if (allowedVideo.Count > 0)
		{
			var invalid = allowedVideo.Where(c => !YouTubeVideoCodec.All.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
			if (invalid.Count > 0)
				errors.Add($"Invalid or unknown video codec(s): {string.Join(", ", invalid)}.");
		}

		var allowedAudio = ParseJsonStringList(profile.AllowedAudioCodecsJson);
		if (allowedAudio.Count > 0)
		{
			var invalid = allowedAudio.Where(c => !YouTubeAudioCodec.All.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
			if (invalid.Count > 0)
				errors.Add($"Invalid or unknown audio codec(s): {string.Join(", ", invalid)}.");
		}

		if (!profile.AllowHdr && !profile.AllowSdr)
			errors.Add("At least one of allowHdr or allowSdr must be true.");

		var allowedContainers = ParseJsonStringList(profile.AllowedContainersJson);
		if (allowedContainers.Count > 0)
		{
			var invalid = allowedContainers.Where(c => !YouTubeContainer.All.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
			if (invalid.Count > 0)
				errors.Add($"Invalid or unknown container(s): {string.Join(", ", invalid)}.");
		}

		if (!profile.PreferSeparateStreams && !profile.AllowMuxedFallback)
			errors.Add("When separate streams are disallowed, muxed fallback must be allowed or no format may match.");

		return errors;
	}

	static List<string> ParseJsonStringList(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return new List<string>();
		try
		{
			var list = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
			return list ?? new List<string>();
		}
		catch { return new List<string>(); }
	}

	static List<int> GetDegradeSteps(QualityProfileEntity profile)
	{
		if (!string.IsNullOrWhiteSpace(profile.DegradeHeightStepsJson))
		{
			try
			{
				var list = JsonSerializer.Deserialize<List<int>>(profile.DegradeHeightStepsJson, JsonOptions);
				if (list?.Count > 0) return list;
			}
			catch { }
		}
		var max = profile.MaxHeight ?? 4320;
		var min = profile.MinHeight ?? 144;
		return YouTubeHeightLadder.Heights.Where(h => h <= max && h >= min).ToList();
	}
}
