namespace TubeArr.Backend.DownloadBackends;

public enum DownloadBackendKind
{
	YtDlp = 0
}

public static class DownloadBackendKindParser
{
	public const string YtDlpString = "yt-dlp";

	public static bool TryParse(string? value, out DownloadBackendKind kind)
	{
		kind = DownloadBackendKind.YtDlp;
		if (string.IsNullOrWhiteSpace(value))
			return false;
		var v = value.Trim().ToLowerInvariant();
		if (v == YtDlpString || v == "ytdlp")
			return true;

		return false;
	}

	public static DownloadBackendKind ParseOrDefault(string? value, DownloadBackendKind defaultKind = DownloadBackendKind.YtDlp)
		=> TryParse(value, out var k) ? k : defaultKind;

	public static string ToApiString(DownloadBackendKind kind) => YtDlpString;
}
