namespace TubeArr.Backend.Data;

/// <summary>Singleton (Id=1) settings for slskd HTTP integration.</summary>
public sealed class SlskdConfigEntity
{
	public int Id { get; set; } = 1;

	public bool Enabled { get; set; }

	/// <summary>Base URL without trailing slash, e.g. https://host:5030</summary>
	public string BaseUrl { get; set; } = "";

	public string ApiKey { get; set; } = "";

	/// <summary>Optional. Absolute path on the TubeArr host to slskd&apos;s downloads directory (shared volume).</summary>
	public string LocalDownloadsPath { get; set; } = "";

	public int SearchTimeoutSeconds { get; set; } = 30;

	public int MaxCandidatesStored { get; set; } = 50;

	public int AutoPickMinScore { get; set; } = 85;

	public bool ManualReviewOnly { get; set; } = true;

	public int RetryAttempts { get; set; } = 2;

	/// <summary><see cref="AcquisitionOrderKind"/> stored as int.</summary>
	public int AcquisitionOrder { get; set; }

	public bool FallbackToSlskdOnYtDlpFailure { get; set; } = true;

	public bool FallbackToYtDlpOnSlskdFailure { get; set; } = true;

	/// <summary>0 = keep higher quality; 1 = normalize to profile max via transcode.</summary>
	public int HigherQualityHandling { get; set; }

	public bool RequireManualReviewOnTranscode { get; set; } = true;

	public bool KeepOriginalAfterTranscode { get; set; }
}
