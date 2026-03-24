namespace TubeArr.Backend.QualityProfile;

/// <summary>
/// YouTube video codecs supported by yt-dlp. Normalized for profile storage; mapped to vcodec selectors.
/// </summary>
public static class YouTubeVideoCodec
{
	public const string AV1 = "AV1";
	public const string VP9 = "VP9";
	public const string AVC = "AVC";

	public static readonly IReadOnlyList<string> All = new[] { AV1, VP9, AVC };

	/// <summary>yt-dlp vcodec selector pattern (e.g. av01, vp9, avc).</summary>
	public static string ToSelectorPattern(string codec)
	{
		return codec switch
		{
			AV1 => "av01",
			VP9 => "vp9",
			AVC => "avc",
			_ => codec?.ToLowerInvariant() ?? ""
		};
	}
}

/// <summary>
/// YouTube audio codecs supported by yt-dlp. Normalized for profile storage; mapped to acodec selectors.
/// </summary>
public static class YouTubeAudioCodec
{
	public const string OPUS = "OPUS";
	public const string MP4A = "MP4A";

	public static readonly IReadOnlyList<string> All = new[] { OPUS, MP4A };

	/// <summary>yt-dlp acodec selector pattern (e.g. opus, mp4a).</summary>
	public static string ToSelectorPattern(string codec)
	{
		return codec switch
		{
			OPUS => "opus",
			MP4A => "mp4a",
			_ => codec?.ToLowerInvariant() ?? ""
		};
	}
}

/// <summary>
/// Container formats supported by yt-dlp for YouTube.
/// </summary>
public static class YouTubeContainer
{
	public const string MP4 = "mp4";
	public const string WEBM = "webm";
	public const string M4A = "m4a";
	public const string THREE_GP = "3gp";

	public static readonly IReadOnlyList<string> All = new[] { MP4, WEBM, M4A, THREE_GP };
}

/// <summary>
/// Canonical YouTube resolution ladder for degrade steps (highest to lowest).
/// </summary>
public static class YouTubeHeightLadder
{
	public static readonly IReadOnlyList<int> Heights = new[] { 4320, 2160, 1440, 1080, 720, 480, 360, 240, 144 };
}
