using TubeArr.Backend;

namespace TubeArr.Backend.DownloadBackends;

/// <summary>Central place for classifying download failures (retry, auth, format). Used by the queue and backends.</summary>
public static class DownloadRetryPolicy
{
	public const int MaxTransientRetries = 2;

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

	public static bool MayAutoRetry(FailureClass c) =>
		c == FailureClass.TransientNetwork && MaxTransientRetries > 0;
}
