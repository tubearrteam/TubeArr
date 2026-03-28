using System.Text.Json;
using TubeArr.Backend.Data;

namespace TubeArr.Backend.QualityProfile;

/// <summary>
/// Builds yt-dlp -f (selector) and -S (sort) arguments from a tokenized quality profile.
/// Uses only supported yt-dlp fields: height, fps, vcodec, acodec, ext, dynamic_range.
/// </summary>
public sealed class YtDlpQualityProfileBuilder
{
	static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = false };

	public YtDlpBuildResult Build(QualityProfileEntity profile)
	{
		var debug = new List<string>();
		var args = new List<string>();

		// Resolve list fields
		var allowedVideo = ParseJsonStringList(profile.AllowedVideoCodecsJson);
		var preferredVideo = ParseJsonStringList(profile.PreferredVideoCodecsJson);
		var allowedAudio = ParseJsonStringList(profile.AllowedAudioCodecsJson);
		var preferredAudio = ParseJsonStringList(profile.PreferredAudioCodecsJson);
		var allowedContainers = ParseJsonStringList(profile.AllowedContainersJson);
		var preferredContainers = ParseJsonStringList(profile.PreferredContainersJson);

		// Default preferred orders when not specified
		if (preferredVideo.Count == 0)
			preferredVideo = new List<string>(YouTubeVideoCodec.All);
		if (preferredAudio.Count == 0)
			preferredAudio = new List<string>(YouTubeAudioCodec.All);
		if (preferredContainers.Count == 0)
			preferredContainers = new List<string> { YouTubeContainer.MP4, YouTubeContainer.WEBM };

		var maxHeight = profile.MaxHeight ?? 4320;
		var minHeight = profile.MinHeight ?? 144;
		var minFps = profile.MinFps ?? 0;
		var maxFps = profile.MaxFps ?? 60;
		var fallbackMode = (FallbackMode)(profile.FallbackMode switch { 3 => 1, _ => profile.FallbackMode });

		// Build video part constraints (for bv*[...])
		var videoPredicates = new List<string>();

		videoPredicates.Add($"height<={maxHeight}");
		debug.Add($"height<={maxHeight} (maxHeight)");

		videoPredicates.Add($"height>={minHeight}");
		debug.Add($"height>={minHeight} (minHeight)");

		if (minFps > 0)
		{
			videoPredicates.Add($"fps>={minFps}");
			debug.Add($"fps>={minFps} (minFps)");
		}

		if (maxFps > 0)
		{
			videoPredicates.Add($"fps<={maxFps}");
			debug.Add($"fps<={maxFps} (maxFps)");
		}

		if (profile.AllowHdr && !profile.AllowSdr)
			videoPredicates.Add("dynamic_range=HDR");
		else if (profile.AllowSdr && !profile.AllowHdr)
			videoPredicates.Add("dynamic_range=SDR");
		// else both allowed, no predicate

		if (allowedVideo.Count > 0)
		{
			videoPredicates.Add(BuildVcodecPredicate(allowedVideo));
			debug.Add($"vcodec in [{string.Join(",", allowedVideo)}]");
		}

		if (allowedContainers.Count > 0)
		{
			var normalizedContainers = allowedContainers
				.Where(c => !string.IsNullOrWhiteSpace(c))
				.Select(c => c.Trim().ToLowerInvariant())
				.Distinct(StringComparer.Ordinal)
				.ToList();

			if (normalizedContainers.Count == 1)
			{
				videoPredicates.Add($"ext={normalizedContainers[0]}");
			}
			else if (normalizedContainers.Count > 1)
			{
				videoPredicates.Add(BuildExtRegexPredicate(normalizedContainers));
			}

			debug.Add($"ext in [{string.Join(",", allowedContainers)}]");
		}

		var videoFilter = string.Join("", videoPredicates.Select(p => "[" + p + "]"));
		var videoPart = "bv*" + videoFilter;

		// Audio part constraints (for ba[...])
		var audioPredicates = new List<string>();
		if (allowedAudio.Count > 0)
		{
			audioPredicates.Add(BuildAcodecPredicate(allowedAudio));
			debug.Add($"acodec in [{string.Join(",", allowedAudio)}]");
		}
		var audioFilter = string.Join("", audioPredicates.Select(p => "[" + p + "]"));
		var audioPart = "ba" + audioFilter;

		// Primary selector: best video + best audio (separate streams)
		var primarySelector = videoPart + "+" + audioPart;
		var fallbackPlan = new List<string> { "1. Preferred codecs + target height/fps + separate streams" };

		// Fallback chain
		if (fallbackMode == FallbackMode.Strict)
		{
			fallbackPlan.Add("2. Strict: no fallback");
		}
		else
		{
			fallbackPlan.Add("2. Relax audio preference");
			fallbackPlan.Add("3. Relax video codec preference");
			fallbackPlan.Add("4. Relax fps preference");
			fallbackPlan.Add("5. Any allowed codec under height ceiling");
			if (fallbackMode == FallbackMode.DegradeResolution)
				fallbackPlan.Add("6. Step down resolution through allowed heights");
			fallbackPlan.Add(profile.AllowMuxedFallback ? "7. Muxed fallback (b)" : "7. No muxed fallback");
		}

		// Build full selector with fallback alternatives
		var selectorParts = new List<string>();

		// 1) Ideal: primary
		selectorParts.Add(primarySelector);

		// 2) Relax audio (same video, any audio)
		if (fallbackMode != FallbackMode.Strict && allowedAudio.Count > 0)
			selectorParts.Add(videoPart + "+ba");

		// 3) Relax video codec (any allowed vcodec)
		if (fallbackMode != FallbackMode.Strict && allowedVideo.Count > 1)
		{
			var relaxedVideo = "bv*[height<=" + maxHeight + "][height>=" + minHeight + "]" + (maxFps > 0 ? "[fps<=" + maxFps + "]" : "") + "+" + audioPart;
			if (!selectorParts.Contains(relaxedVideo))
				selectorParts.Add(relaxedVideo);
		}

		// 4) Degrade resolution: step down from target
		if (fallbackMode == FallbackMode.DegradeResolution)
		{
			var steps = GetDegradeHeightSteps(profile);
			// Step down from the selected ceiling (maxHeight).
			foreach (var h in steps.Where(h => h < maxHeight && h >= minHeight))
			{
				var stepPredicates = new List<string> { $"height<={h}", $"height>={minHeight}" };
				if (maxFps > 0) stepPredicates.Add($"fps<={maxFps}");
				if (allowedVideo.Count > 0)
					stepPredicates.Add(BuildVcodecPredicate(allowedVideo));
				var stepFilter = string.Join("", stepPredicates.Select(p => "[" + p + "]"));
				selectorParts.Add("bv*" + stepFilter + "+" + audioPart);
			}
		}

		// 5) Muxed fallback (not in Strict mode)
		if (fallbackMode != FallbackMode.Strict && profile.AllowMuxedFallback)
		{
			var muxedPredicates = new List<string> { $"height<={maxHeight}", $"height>={minHeight}" };
			if (maxFps > 0) muxedPredicates.Add($"fps<={maxFps}");
			if (allowedVideo.Count > 0)
				muxedPredicates.Add(BuildVcodecPredicate(allowedVideo));
			var muxedFilter = string.Join("", muxedPredicates.Select(p => "[" + p + "]"));
			selectorParts.Add("b" + muxedFilter);
		}

		var selector = string.Join("/", selectorParts.Distinct());

		// Sort expression: prefer codecs in order, then res, fps, etc.
		var sortParts = new List<string>();
		if (preferredVideo.Count > 0)
			sortParts.Add("vcodec");
		sortParts.Add("res");
		if (maxFps > 0)
			sortParts.Add("fps");
		if (preferredAudio.Count > 0)
			sortParts.Add("acodec");
		if (preferredContainers.Count > 0)
			sortParts.Add("ext");
		var sort = string.Join(",", sortParts);

		args.Add("-f");
		args.Add(selector);
		args.Add("-S");
		args.Add(sort);

		return new YtDlpBuildResult
		{
			ProfileId = profile.Id,
			ProfileName = profile.Name,
			Selector = selector,
			Sort = sort,
			FallbackPlanSummary = string.Join("; ", fallbackPlan),
			YtDlpArgs = args,
			DebugMetadata = debug
		};
	}

	/// <summary>Codec filters for <c>-f</c>: single-codec uses <c>^=</c> (works in yt-dlp config files); multiple uses <c>~=</c> with double quotes.</summary>
	static string BuildVcodecPredicate(IReadOnlyList<string> allowedVideo)
	{
		var patterns = allowedVideo
			.Select(YouTubeVideoCodec.ToSelectorPattern)
			.Where(static p => p.Length > 0)
			.Distinct(StringComparer.Ordinal)
			.ToList();
		if (patterns.Count == 1)
			return $"vcodec^={patterns[0]}";
		return $"vcodec~=\"^({string.Join("|", patterns)})\"";
	}

	static string BuildAcodecPredicate(IReadOnlyList<string> allowedAudio)
	{
		var patterns = allowedAudio
			.Select(YouTubeAudioCodec.ToSelectorPattern)
			.Where(static p => p.Length > 0)
			.Distinct(StringComparer.Ordinal)
			.ToList();
		if (patterns.Count == 1)
			return $"acodec^={patterns[0]}";
		return $"acodec~=\"^({string.Join("|", patterns)})\"";
	}

	static string BuildExtRegexPredicate(IReadOnlyList<string> normalizedLowerContainers)
	{
		var alt = string.Join("|", normalizedLowerContainers);
		return $"ext~=\"^({alt})$\"";
	}

	static List<string> ParseJsonStringList(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
			return new List<string>();
		try
		{
			var list = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
			return list ?? new List<string>();
		}
		catch
		{
			return new List<string>();
		}
	}

	static IReadOnlyList<int> GetDegradeHeightSteps(QualityProfileEntity profile)
	{
		if (!string.IsNullOrWhiteSpace(profile.DegradeHeightStepsJson))
		{
			try
			{
				var list = JsonSerializer.Deserialize<List<int>>(profile.DegradeHeightStepsJson, JsonOptions);
				if (list?.Count > 0)
					return list.OrderByDescending(x => x).ToList();
			}
			catch { }
		}
		var max = profile.MaxHeight ?? 4320;
		var min = profile.MinHeight ?? 144;
		return YouTubeHeightLadder.Heights.Where(h => h <= max && h >= min).ToList();
	}
}
