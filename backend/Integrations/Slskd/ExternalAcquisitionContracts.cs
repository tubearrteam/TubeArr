using System.Text.Json;
using System.Text.Json.Serialization;
using TubeArr.Backend.Data;

namespace TubeArr.Backend.Integrations.Slskd;

public static class ExternalAcquisitionPhases
{
	public const string None = "none";
	public const string PendingYtDlp = "pendingYtDlp";
	public const string PendingSearch = "pendingSearch";
	public const string CandidatesReady = "candidatesReady";
	public const string AwaitingManualPick = "awaitingManualPick";
	public const string QueuedTransfer = "queuedTransfer";
	public const string Transferring = "transferring";
	public const string DownloadedLocal = "downloadedLocal";
	public const string ComplianceCheck = "complianceCheck";
	public const string Transcoding = "transcoding";
	public const string Importing = "importing";
	public const string Done = "done";
	public const string Failed = "failed";
	public const string ManualReview = "manualReview";
}

/// <summary>Serialized to <see cref="TubeArr.Backend.Data.DownloadQueueEntity.ExternalAcquisitionJson"/>.</summary>
public sealed class ExternalAcquisitionState
{
	public string Phase { get; set; } = ExternalAcquisitionPhases.None;

	/// <summary>yt-dlp or slskd</summary>
	public string ActiveProvider { get; set; } = "yt-dlp";

	public bool FallbackUsed { get; set; }

	public string? PrimaryFailureSummary { get; set; }

	public List<ExternalDownloadCandidateDto> Candidates { get; set; } = new();

	public ExternalDownloadCandidateDto? ChosenCandidate { get; set; }

	public Guid? SearchId { get; set; }

	public string? TransferUsername { get; set; }

	public Guid? TransferId { get; set; }

	/// <summary>Worker should pick this row again while running.</summary>
	public bool ResumeProcessor { get; set; }

	public string? ComplianceSummary { get; set; }

	public string? LastSlskdError { get; set; }

	public int YtDlpAttemptCount { get; set; }
}

public sealed class ExternalDownloadCandidateDto
{
	public string Id { get; set; } = "";

	public string Source { get; set; } = AcquisitionMethodIds.Slskd;

	public string Username { get; set; } = "";

	public string Filename { get; set; } = "";

	public long Size { get; set; }

	public string? Extension { get; set; }

	public int? DurationSeconds { get; set; }

	public int? BitrateKbps { get; set; }

	public int MatchScore { get; set; }

	public string Confidence { get; set; } = "low";

	public List<ScoreSignalDto> MatchedSignals { get; set; } = new();

	public string? SearchQueryUsed { get; set; }
}

public sealed class ScoreSignalDto
{
	public string Code { get; set; } = "";
	public int Weight { get; set; }
	public string? Detail { get; set; }
}

public static class ExternalAcquisitionJsonSerializer
{
	static readonly JsonSerializerOptions Options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false
	};

	public static ExternalAcquisitionState? TryDeserialize(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
			return null;
		try
		{
			return JsonSerializer.Deserialize<ExternalAcquisitionState>(json, Options);
		}
		catch
		{
			return null;
		}
	}

	public static string Serialize(ExternalAcquisitionState state) =>
		JsonSerializer.Serialize(state, Options);
}
