using System.Text;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class VideoFileNaming
{
	private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

	private static readonly HashSet<string> SupportedTokens = new(StringComparer.OrdinalIgnoreCase)
	{
		"Channel Name",
		"Channel Id",
		"Playlist Title",
		"Playlist Id",
		"Playlist Index",
		"Playlist Number",
		"Video Title",
		"Video Id",
		"YouTube Video Id",
		"Upload Date",
		"Upload Year",
		"Upload Month",
		"Upload Day",
		"Quality Full",
		"Resolution",
		"Ext",
		"Uploader",

		// Media Info (yt-dlp metadata)
		"MediaInfo Codec",
		"MediaInfo Audio Codec",
		"MediaInfo Resolution",
		"MediaInfo Framerate",
		"MediaInfo HDR/SDR",
		"MediaInfo Audio Channels",
		"MediaInfo Bitrate",
		"MediaInfo Container"
	};

	public sealed record NamingContext(
		ChannelEntity? Channel,
		PlaylistEntity? Playlist,
		VideoEntity Video,
		int? PlaylistIndex,
		string? QualityFull,
		string? Resolution,
		string? Extension,
		int? PlaylistNumber = null,
		string? MediaInfoCodec = null,
		string? MediaInfoAudioCodec = null,
		string? MediaInfoResolution = null,
		string? MediaInfoFramerate = null,
		string? MediaInfoDynamicRange = null,
		string? MediaInfoAudioChannels = null,
		string? MediaInfoBitrate = null,
		string? MediaInfoContainer = null
	);

	public static IReadOnlyCollection<string> GetSupportedTokens() => SupportedTokens;

	public static string BuildFileName(string pattern, NamingContext context, NamingConfigEntity namingConfig)
	{
		var raw = ResolvePattern(pattern, context, namingConfig);
		return SanitizeFileName(raw, namingConfig);
	}

	public static string BuildFolderName(string pattern, NamingContext context, NamingConfigEntity namingConfig)
	{
		var raw = ResolvePattern(pattern, context, namingConfig);
		return SanitizeFileName(raw, namingConfig);
	}

	public static IReadOnlyCollection<(string Token, string Error)> ValidatePattern(string pattern)
	{
		var failures = new List<(string Token, string Error)>();

		foreach (var (token, _, _, _) in EnumerateTokens(pattern))
		{
			if (!SupportedTokens.Contains(token))
			{
				failures.Add((token, $"Unknown token '{{{token}}}'."));
			}
		}

		return failures;
	}

	private static string ResolvePattern(string pattern, NamingContext context, NamingConfigEntity namingConfig)
	{
		if (string.IsNullOrWhiteSpace(pattern))
		{
			return context.Video.Title ?? string.Empty;
		}

		var sb = new StringBuilder(pattern.Length + 64);
		var lastIndex = 0;

		foreach (var (token, format, start, end) in EnumerateTokens(pattern))
		{
			if (start > lastIndex)
			{
				sb.Append(pattern.AsSpan(lastIndex, start - lastIndex));
			}

			var value = ResolveToken(token, format, context, namingConfig);
			sb.Append(value);

			lastIndex = end;
		}

		if (lastIndex < pattern.Length)
		{
			sb.Append(pattern.AsSpan(lastIndex));
		}

		var result = sb.ToString();
		return CleanupSeparators(result);
	}

	private static IEnumerable<(string Token, string? Format, int Start, int End)> EnumerateTokens(string pattern)
	{
		var length = pattern.Length;
		var index = 0;

		while (index < length)
		{
			var open = pattern.IndexOf('{', index);
			if (open == -1)
			{
				yield break;
			}

			var close = pattern.IndexOf('}', open + 1);
			if (close == -1)
			{
				yield break;
			}

			var inner = pattern.Substring(open + 1, close - open - 1);
			string token;
			string? format = null;

			var colonIndex = inner.IndexOf(':');
			if (colonIndex >= 0)
			{
				token = inner[..colonIndex].Trim();
				format = inner[(colonIndex + 1)..].Trim();
			}
			else
			{
				token = inner.Trim();
			}

			yield return (token, format, open, close + 1);

			index = close + 1;
		}
	}

	private static string ResolveToken(string token, string? format, NamingContext context, NamingConfigEntity namingConfig)
	{
		switch (token.ToLowerInvariant())
		{
			case "channel name":
				return context.Channel?.Title ?? string.Empty;
			case "channel id":
				return context.Channel?.YoutubeChannelId ?? string.Empty;
			case "uploader":
				return context.Channel?.Title ?? string.Empty;
			case "playlist title":
				return context.Playlist?.Title ?? string.Empty;
			case "playlist id":
				return context.Playlist?.YoutubePlaylistId ?? string.Empty;
			case "playlist index":
				{
					if (context.PlaylistIndex is null)
					{
						return string.Empty;
					}

					return ApplyNumericFormat(context.PlaylistIndex.Value, format);
				}
			case "playlist number":
				{
					if (context.PlaylistNumber is null)
					{
						return string.Empty;
					}

					return ApplyNumericFormat(context.PlaylistNumber.Value, format);
				}
			case "video title":
				return context.Video.Title ?? string.Empty;
			case "video id":
				return context.Video.YoutubeVideoId ?? string.Empty;
			case "youtube video id":
				return context.Video.YoutubeVideoId ?? string.Empty;
			case "upload date":
				{
					var dt = context.Video.UploadDateUtc;
					return dt.ToString("yyyy-MM-dd");
				}
			case "upload year":
				return context.Video.UploadDateUtc.Year.ToString("D4");
			case "upload month":
				return context.Video.UploadDateUtc.Month.ToString("D2");
			case "upload day":
				return context.Video.UploadDateUtc.Day.ToString("D2");
			case "quality full":
				return context.QualityFull ?? string.Empty;
			case "resolution":
				return context.Resolution ?? string.Empty;
			case "ext":
				return (context.Extension ?? string.Empty).TrimStart('.');
			case "mediainfo codec":
				return context.MediaInfoCodec ?? string.Empty;
			case "mediainfo audio codec":
				return context.MediaInfoAudioCodec ?? string.Empty;
			case "mediainfo resolution":
				return context.MediaInfoResolution ?? context.Resolution ?? string.Empty;
			case "mediainfo framerate":
				return context.MediaInfoFramerate ?? string.Empty;
			case "mediainfo hdr/sdr":
				return context.MediaInfoDynamicRange ?? string.Empty;
			case "mediainfo audio channels":
				return context.MediaInfoAudioChannels ?? string.Empty;
			case "mediainfo bitrate":
				return context.MediaInfoBitrate ?? string.Empty;
			case "mediainfo container":
				// Container is usually synonymous with the extension in our naming context.
				return context.MediaInfoContainer ?? (context.Extension ?? string.Empty).TrimStart('.');
			default:
				return string.Empty;
		}
	}

	private static string ApplyNumericFormat(int value, string? format)
	{
		if (string.IsNullOrWhiteSpace(format))
		{
			return value.ToString();
		}

		// Support zero-padding-style formats such as "00" or "000".
		if (format.All(c => c == '0'))
		{
			return value.ToString("D" + format.Length);
		}

		return value.ToString(format);
	}

	private static string SanitizeFileName(string value, NamingConfigEntity namingConfig)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}

		var sanitized = value;

		if (namingConfig.ReplaceIllegalCharacters)
		{
			sanitized = ReplaceIllegalCharacters(sanitized, namingConfig);
		}

		sanitized = CleanupSeparators(sanitized);

		if (OperatingSystem.IsWindows())
		{
			sanitized = sanitized.TrimEnd('.', ' ');
		}

		return sanitized;
	}

	private static string ReplaceIllegalCharacters(string value, NamingConfigEntity namingConfig)
	{
		var colonReplacement = namingConfig.ColonReplacementFormat switch
		{
			0 => "",
			1 => "-",
			2 => " -",
			3 => " - ",
			4 => "-", // "Smart" behaviour is overkill here; a simple dash is predictable.
			5 => namingConfig.CustomColonReplacementFormat ?? "",
			_ => ""
		};

		var sb = new StringBuilder(value.Length);
		foreach (var ch in value)
		{
			if (ch == ':' && colonReplacement.Length > 0)
			{
				sb.Append(colonReplacement);
				continue;
			}

			if (InvalidFileNameChars.Contains(ch) || ch == '/' || ch == '\\')
			{
				// Drop invalid characters entirely; surrounding whitespace / separators
				// are normalised in CleanupSeparators.
				continue;
			}

			sb.Append(ch);
		}

		return sb.ToString();
	}

	private static string CleanupSeparators(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}

		var result = value;

		// Collapse multiple spaces.
		while (result.Contains("  ", StringComparison.Ordinal))
		{
			result = result.Replace("  ", " ", StringComparison.Ordinal);
		}

		// Remove common leftover sequences around separators and brackets.
		result = result.Replace(" - - ", " - ", StringComparison.Ordinal);
		result = result.Replace("--", "-", StringComparison.Ordinal);
		result = result.Replace("[]", string.Empty, StringComparison.Ordinal);
		result = result.Replace("()", string.Empty, StringComparison.Ordinal);

		result = result.Trim();

		return result;
	}
}

