using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.Data;

namespace TubeArr.Backend.QualityProfile;

/// <summary>
/// Builds yt-dlp config.txt content from a structured quality profile and parses hints from saved config text.
/// </summary>
public static class QualityProfileYtDlpConfigContent
{
	public static string BuildConfigFileBodyFromEntity(QualityProfileEntity profile, bool ffmpegConfigured, ILogger? logger, int logContextId)
	{
		var builder = new YtDlpQualityProfileBuilder();
		var result = builder.Build(profile);
		var preferred = GetPreferredOutputContainer(profile);
		var argv = BuildConfigArgv(profile, result, ffmpegConfigured, preferred, logger, logContextId);
		return ToConfigFileBody(argv);
	}

	public static string ToConfigFileBody(IReadOnlyList<string> argv)
	{
		if (argv.Count == 0)
			return string.Empty;
		var sb = new StringBuilder();
		foreach (var a in argv)
		{
			sb.Append(a);
			sb.Append('\n');
		}
		return sb.ToString();
	}

	public static List<string> BuildConfigArgv(
		QualityProfileEntity profile,
		YtDlpBuildResult result,
		bool ffmpegConfigured,
		string? preferredOutputContainer,
		ILogger? logger,
		int logContextId)
	{
		var argv = new List<string>(result.YtDlpArgs);
		if (!string.IsNullOrWhiteSpace(preferredOutputContainer))
		{
			argv.Add("--merge-output-format");
			argv.Add(preferredOutputContainer!);
			if (ffmpegConfigured)
			{
				if (CanKeepContainerAsIs(profile, preferredOutputContainer!))
				{
					argv.Add("--remux-video");
					argv.Add(preferredOutputContainer!);
					logger?.LogInformation("Post-process action=remux queueId={QueueId} container={Container}", logContextId, preferredOutputContainer);
				}
				else
				{
					argv.Add("--recode-video");
					argv.Add(preferredOutputContainer!);
					logger?.LogInformation("Post-process action=recode queueId={QueueId} container={Container}", logContextId, preferredOutputContainer);
				}
			}
		}

		var advancedArgs = GetAdvancedYtDlpArgs(profile);
		if (advancedArgs.Count > 0)
		{
			argv.AddRange(advancedArgs);
			logger?.LogInformation("Applying advanced yt-dlp args queueId={QueueId} argCount={ArgCount}", logContextId, advancedArgs.Count);
		}

		RemoveYoutubeExtractorArgsFromArgv(argv);
		return argv;
	}

	/// <summary>
	/// Drops <c>--extractor-args</c> targeting <c>youtube:…</c> so quality-profile buckets or legacy config cannot
	/// force <c>fetch_pot</c> / <c>player_client</c> overrides that break auth (valid cookies + bad client path).
	/// Manual yt-dlp without these matches default ANDROID_VR-style behavior.
	/// </summary>
	public static void RemoveYoutubeExtractorArgsFromArgv(List<string> argv)
	{
		for (var i = 0; i < argv.Count; i++)
		{
			var a = argv[i];
			if (IsBareExtractorArgsFlag(a))
			{
				if (i + 1 < argv.Count && ContainsYoutubeExtractorScope(argv[i + 1]))
				{
					argv.RemoveAt(i + 1);
					argv.RemoveAt(i);
					i--;
				}
				continue;
			}

			if (IsExtractorArgsWithInlineValue(a, out var inline) && ContainsYoutubeExtractorScope(inline))
			{
				argv.RemoveAt(i);
				i--;
			}
		}
	}

	static bool IsBareExtractorArgsFlag(string a) =>
		string.Equals(a, "--extractor-args", StringComparison.OrdinalIgnoreCase)
		|| string.Equals(a, "-extractor-args", StringComparison.OrdinalIgnoreCase);

	static bool IsExtractorArgsWithInlineValue(string a, out string value)
	{
		value = "";
		if (!a.StartsWith("--extractor-args=", StringComparison.OrdinalIgnoreCase)
		    && !a.StartsWith("-extractor-args=", StringComparison.OrdinalIgnoreCase))
			return false;
		var eq = a.IndexOf('=');
		if (eq < 0 || eq >= a.Length - 1)
			return false;
		value = a[(eq + 1)..];
		return true;
	}

	static bool ContainsYoutubeExtractorScope(string token) =>
		token.Contains("youtube:", StringComparison.OrdinalIgnoreCase);

	/// <summary>Same as <see cref="RemoveYoutubeExtractorArgsFromArgv"/> but for one-option-per-line yt-dlp config files.</summary>
	public static string SanitizeConfigTextForYtDlp(string? configText)
	{
		if (string.IsNullOrEmpty(configText))
			return configText ?? string.Empty;

		var lines = configText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
		var result = new List<string>(lines.Length);
		for (var i = 0; i < lines.Length; i++)
		{
			var line = lines[i];
			var t = line.Trim();
			if (t.Length == 0 || t.StartsWith('#'))
			{
				result.Add(line);
				continue;
			}

			if (IsBareExtractorArgsFlag(t))
			{
				if (i + 1 < lines.Length && ContainsYoutubeExtractorScope(lines[i + 1].Trim()))
				{
					i++;
					continue;
				}

				result.Add(line);
				continue;
			}

			if (IsExtractorArgsWithInlineValue(t, out var inline) && ContainsYoutubeExtractorScope(inline))
				continue;

			result.Add(line);
		}

		return string.Join("\n", result);
	}

	/// <summary>
	/// Removes <c>--cookies</c> / <c>--no-cookies</c> from profile config text so a later CLI <c>--cookies</c> (after
	/// <c>--config</c>) is the single source of truth. Otherwise yt-dlp applies config after early argv and can wipe cookies.
	/// </summary>
	public static string RemoveCookiesDirectivesFromConfigText(string? configText)
	{
		if (string.IsNullOrEmpty(configText))
			return configText ?? string.Empty;

		var lines = configText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
		var result = new List<string>(lines.Length);
		for (var i = 0; i < lines.Length; i++)
		{
			var line = lines[i];
			var t = line.Trim();
			if (t.Length == 0 || t.StartsWith('#'))
			{
				result.Add(line);
				continue;
			}

			if (string.Equals(t, "--no-cookies", StringComparison.OrdinalIgnoreCase))
				continue;

			if (string.Equals(t, "--cookies", StringComparison.OrdinalIgnoreCase)
			    || string.Equals(t, "-cookies", StringComparison.OrdinalIgnoreCase))
			{
				if (i + 1 < lines.Length)
					i++;
				continue;
			}

			if (t.StartsWith("--cookies=", StringComparison.OrdinalIgnoreCase)
			    || t.StartsWith("-cookies=", StringComparison.OrdinalIgnoreCase))
				continue;

			result.Add(line);
		}

		return string.Join("\n", result);
	}

	/// <summary>
	/// One yt-dlp config file for a download: sanitized profile body plus <c>-o</c> template, optional <c>--ffmpeg-location</c>,
	/// and optional <c>--cookies</c> as the <b>last</b> lines so profile/options cannot override Netscape auth.
	/// </summary>
	/// <remarks>
	/// yt-dlp parses each config line like a shell: spaces split arguments unless quoted. Output templates often contain
	/// spaces (e.g. <c>%(upload_date)s - %(title)s</c>); those must be a single <c>-o</c> value. Backslashes are normalized
	/// to forward slashes so Windows paths are not misread as line continuations or escapes.
	/// </remarks>
	public static string BuildMergedDownloadConfigBody(
		string sanitizedProfileConfig,
		string outputTemplate,
		string? ffmpegLocation,
		string? netscapeCookiesPath = null)
	{
		var sb = new StringBuilder();
		var profile = (sanitizedProfileConfig ?? "").TrimEnd();
		if (profile.Length > 0)
		{
			sb.Append(profile);
			sb.Append('\n');
		}

		var templateLine = "-o " + QuoteForYtDlpConfigFile(NormalizeFilesystemPathForYtDlpConfig(outputTemplate));
		sb.Append(templateLine);
		sb.Append('\n');
		if (!string.IsNullOrWhiteSpace(ffmpegLocation))
		{
			var loc = NormalizeExecutablePathForYtDlpConfig(ffmpegLocation.Trim());
			sb.Append("--ffmpeg-location ");
			sb.Append(QuoteForYtDlpConfigFile(loc));
			sb.Append('\n');
		}

		if (!string.IsNullOrWhiteSpace(netscapeCookiesPath))
		{
			try
			{
				var full = Path.GetFullPath(netscapeCookiesPath.Trim());
				if (File.Exists(full))
				{
					var normalized = NormalizeFilesystemPathForYtDlpConfig(full);
					sb.Append("--cookies\n");
					sb.Append(QuoteForYtDlpConfigFile(normalized));
					sb.Append('\n');
				}
			}
			catch
			{
				/* ignore bad paths */
			}
		}

		return sb.ToString();
	}

	internal static string NormalizeFilesystemPathForYtDlpConfig(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
			return path;
		// Do not pass yt-dlp field templates through Path.GetFullPath (invalid / odd semantics).
		if (!path.Contains("%(", StringComparison.Ordinal))
		{
			try
			{
				if (path.Contains('\\', StringComparison.Ordinal) || path.Contains('/', StringComparison.Ordinal))
					path = Path.GetFullPath(path);
			}
			catch
			{
				// keep original
			}
		}

		return path.Replace('\\', '/');
	}

	static string NormalizeExecutablePathForYtDlpConfig(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
			return path;
		try
		{
			if (File.Exists(path) || Directory.Exists(path))
				path = Path.GetFullPath(path);
		}
		catch
		{
		}

		return path.Replace('\\', '/');
	}

	/// <summary>Double-quote values that would otherwise be split on spaces when yt-dlp parses the config line.</summary>
	internal static string QuoteForYtDlpConfigFile(string value)
	{
		if (string.IsNullOrEmpty(value))
			return "\"\"";

		ReadOnlySpan<char> triggers = stackalloc char[] { ' ', '\t', '\r', '\n', '"', '\'' };
		var needsQuote = value.AsSpan().IndexOfAny(triggers) >= 0;
		if (!needsQuote)
			return value;

		return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
	}

	public static string? GetPreferredOutputContainer(QualityProfileEntity profile)
	{
		static string? FirstValue(string? json)
		{
			if (string.IsNullOrWhiteSpace(json))
				return null;
			try
			{
				var list = JsonSerializer.Deserialize<List<string>>(json);
				return list?.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
			}
			catch
			{
				return null;
			}
		}

		var preferred = FirstValue(profile.PreferredContainersJson);
		if (!string.IsNullOrWhiteSpace(preferred))
			return preferred.Trim().ToLowerInvariant();

		var allowed = FirstValue(profile.AllowedContainersJson);
		if (!string.IsNullOrWhiteSpace(allowed))
			return allowed.Trim().ToLowerInvariant();

		return null;
	}

	public static string? TryGetMergeOutputFormatFromConfigText(string? configText)
	{
		if (string.IsNullOrWhiteSpace(configText))
			return null;
		var m = Regex.Match(configText, @"--merge-output-format\s+(\S+)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
		if (!m.Success)
			return null;
		return m.Groups[1].Value.Trim().Trim('"', '\'').ToLowerInvariant();
	}

	public static bool ConfigTextMentionsAudioExtraction(string? configText)
	{
		if (string.IsNullOrWhiteSpace(configText))
			return false;
		if (configText.Contains("--extract-audio", StringComparison.OrdinalIgnoreCase))
			return true;
		foreach (var line in configText.Split('\n'))
		{
			var t = line.Trim();
			if (t.Length == 0 || t.StartsWith('#'))
				continue;
			var parts = SplitCliArgs(t);
			if (parts.Any(p => string.Equals(p, "-x", StringComparison.OrdinalIgnoreCase)))
				return true;
		}
		return false;
	}

	static bool CanKeepContainerAsIs(QualityProfileEntity profile, string container)
	{
		var target = (container ?? "").Trim().ToLowerInvariant();
		var allowedVideo = ParseCodecs(profile.AllowedVideoCodecsJson);
		var preferredVideo = ParseCodecs(profile.PreferredVideoCodecsJson);
		var allowedAudio = ParseCodecs(profile.AllowedAudioCodecsJson);
		var preferredAudio = ParseCodecs(profile.PreferredAudioCodecsJson);

		var effectiveVideo = preferredVideo.Count > 0 ? preferredVideo : allowedVideo;
		var effectiveAudio = preferredAudio.Count > 0 ? preferredAudio : allowedAudio;

		if (effectiveVideo.Count == 0 && effectiveAudio.Count == 0)
			return false;

		static bool IsSubset(HashSet<string> source, params string[] allowed) =>
			source.Count > 0 && source.All(v => allowed.Contains(v, StringComparer.OrdinalIgnoreCase));

		return target switch
		{
			"mp4" => IsSubset(effectiveVideo, "AVC") && IsSubset(effectiveAudio, "MP4A"),
			"webm" => IsSubset(effectiveVideo, "VP9", "AV1") && IsSubset(effectiveAudio, "OPUS"),
			"3gp" => IsSubset(effectiveVideo, "AVC") && IsSubset(effectiveAudio, "MP4A"),
			"m4a" => IsSubset(effectiveAudio, "MP4A"),
			_ => false
		};
	}

	static HashSet<string> ParseCodecs(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
			return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			var list = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
			return new HashSet<string>(
				list.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()),
				StringComparer.OrdinalIgnoreCase
			);
		}
		catch
		{
			return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		}
	}

	static List<string> GetAdvancedYtDlpArgs(QualityProfileEntity profile)
	{
		var result = new List<string>();
		AppendBucket(result, profile.SelectionArgs);
		AppendBucket(result, profile.MuxArgs);
		AppendBucket(result, profile.AudioArgs);
		AppendBucket(result, profile.TimeArgs);
		AppendBucket(result, profile.SubtitleArgs);
		AppendBucket(result, profile.ThumbnailArgs);
		AppendBucket(result, profile.MetadataArgs);
		AppendBucket(result, profile.CleanupArgs);
		AppendBucket(result, profile.SponsorblockArgs);
		return result;
	}

	static void AppendBucket(List<string> target, string? bucket)
	{
		if (string.IsNullOrWhiteSpace(bucket))
			return;
		target.AddRange(SplitCliArgs(bucket));
	}

	static List<string> SplitCliArgs(string input)
	{
		var args = new List<string>();
		if (string.IsNullOrWhiteSpace(input))
			return args;

		var current = new StringBuilder();
		var inSingleQuote = false;
		var inDoubleQuote = false;

		for (var i = 0; i < input.Length; i++)
		{
			var ch = input[i];
			if (ch == '\'' && !inDoubleQuote)
			{
				inSingleQuote = !inSingleQuote;
				continue;
			}
			if (ch == '"' && !inSingleQuote)
			{
				inDoubleQuote = !inDoubleQuote;
				continue;
			}

			if (!inSingleQuote && !inDoubleQuote && char.IsWhiteSpace(ch))
			{
				if (current.Length > 0)
				{
					args.Add(current.ToString());
					current.Clear();
				}
				continue;
			}

			current.Append(ch);
		}

		if (current.Length > 0)
			args.Add(current.ToString());

		return args;
	}
}
