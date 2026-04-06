namespace TubeArr.Backend;

/// <summary>Stable machine-readable codes for failures (downloads, commands, API). Prefer these over parsing user-facing text.</summary>
public static class TubeArrErrorCodes
{
	public const string MissingYtDlpProfileHints = "MissingYtDlpProfileHints";
	public const string OutputFileMissing = "OutputFileMissing";
	public const string IntermediateStreamOnly = "IntermediateStreamOnly";
	public const string NoAudioInVideo = "NoAudioInVideo";

	/// <summary>yt-dlp stderr suggests login, cookies, or expired session.</summary>
	public const string YtDlpAuthOrCookiesRequired = "YtDlpAuthOrCookiesRequired";

	public const string VideoOrChannelNotFound = "VideoOrChannelNotFound";
	public const string NoQualityProfile = "NoQualityProfile";
	public const string OutputDirectoryUnresolved = "OutputDirectoryUnresolved";
	public const string InvalidYoutubeVideoId = "InvalidYoutubeVideoId";
	public const string YtDlpNotConfigured = "YtDlpNotConfigured";
	public const string QualityProfileConfigMissing = "QualityProfileConfigMissing";

	public static string YtDlpExitCode(int exitCode) => $"YtDlpExit{exitCode}";
}
