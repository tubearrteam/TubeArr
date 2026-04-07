using System.Text.Json;
using TubeArr.Backend;

namespace TubeArr.Backend.DownloadBackends;

/// <summary>Central place for classifying download failures (retry, auth, format). Used by the queue and backends.</summary>
public static class DownloadRetryPolicy
{
	static readonly int[] DefaultRetryDelaysSeconds = { 30, 60, 120 };

	public enum FailureClass
	{
		Unknown = 0,
		TransientNetwork = 1,
		AuthOrCookies = 2,
		FormatOrUnavailable = 3,
		Configuration = 4,
		Permanent = 5
	}

	public static FailureClass Classify(DownloadFailureStage stage, string? structuredErrorCode, string? stderrSnippet)
	{
		if (!string.IsNullOrWhiteSpace(structuredErrorCode))
		{
			if (string.Equals(structuredErrorCode, TubeArrErrorCodes.YtDlpAuthOrCookiesRequired, StringComparison.OrdinalIgnoreCase))
				return FailureClass.AuthOrCookies;
			if (string.Equals(structuredErrorCode, TubeArrErrorCodes.MissingYtDlpProfileHints, StringComparison.OrdinalIgnoreCase)
			    || string.Equals(structuredErrorCode, TubeArrErrorCodes.QualityProfileConfigMissing, StringComparison.OrdinalIgnoreCase))
				return FailureClass.Configuration;
		}

		if (stage == DownloadFailureStage.InvalidConfiguration)
			return FailureClass.Configuration;

		var s = stderrSnippet ?? "";
		if (DownloadQueueProcessor.LooksLikeYtDlpCookieAuthFailure(s))
			return FailureClass.AuthOrCookies;

		if (s.Contains("Requested format is not available", StringComparison.OrdinalIgnoreCase)
		    || s.Contains("No video formats found", StringComparison.OrdinalIgnoreCase)
		    || s.Contains("format not available", StringComparison.OrdinalIgnoreCase))
			return FailureClass.FormatOrUnavailable;

		if (s.Contains("Unable to download", StringComparison.OrdinalIgnoreCase)
		    && (s.Contains("timed out", StringComparison.OrdinalIgnoreCase)
		        || s.Contains("timeout", StringComparison.OrdinalIgnoreCase)
		        || s.Contains("Connection reset", StringComparison.OrdinalIgnoreCase)
		        || s.Contains("Temporary failure", StringComparison.OrdinalIgnoreCase)))
			return FailureClass.TransientNetwork;

		return FailureClass.Unknown;
	}

	/// <summary>Whether another download attempt may help (transient/unknown). Auth and format failures need user action or profile changes.</summary>
	public static bool ShouldRetryAfterFailure(FailureClass c) =>
		c is FailureClass.TransientNetwork or FailureClass.Unknown;

	public static int[] ParseRetryDelaysSecondsJson(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
			return (int[])DefaultRetryDelaysSeconds.Clone();
		try
		{
			var arr = JsonSerializer.Deserialize<int[]>(json.Trim());
			if (arr is null || arr.Length == 0)
				return (int[])DefaultRetryDelaysSeconds.Clone();
			var cleaned = arr.Where(s => s > 0 && s < 86_400).Take(12).ToArray();
			return cleaned.Length > 0 ? cleaned : (int[])DefaultRetryDelaysSeconds.Clone();
		}
		catch
		{
			return (int[])DefaultRetryDelaysSeconds.Clone();
		}
	}

	/// <summary>User-facing hint when cookies may be missing or stale.</summary>
	public static string FormatCookieActionHint(string? resolvedCookiesPath, bool cookiesFileReadable)
	{
		if (!cookiesFileReadable || string.IsNullOrWhiteSpace(resolvedCookiesPath))
			return " Configure a Netscape cookies file in Settings → Tools → yt-dlp (or export from your browser).";
		try
		{
			var fi = new FileInfo(resolvedCookiesPath);
			if (!fi.Exists)
				return " Cookies file path is set but the file was not found; update Settings → Tools → yt-dlp.";
			return $" Cookies file last written {fi.LastWriteTimeUtc:yyyy-MM-dd HH:mm} UTC ({fi.Name}). Re-export if YouTube still rejects the session.";
		}
		catch
		{
			return " Re-export browser cookies from Settings → Tools → yt-dlp if downloads still fail with sign-in errors.";
		}
	}
}
