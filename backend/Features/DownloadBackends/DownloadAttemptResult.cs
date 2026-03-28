namespace TubeArr.Backend.DownloadBackends;

public sealed class DownloadAttemptResult
{
	public bool Success { get; init; }
	public DownloadBackendKind SelectedBackend { get; init; }
	public DownloadFailureStage FailureStage { get; init; }
	public string? StructuredErrorCode { get; init; }
	public string? UserMessage { get; init; }
	public string? PrimaryOutputPath { get; init; }
	public IReadOnlyList<string> OutputFiles { get; init; } = Array.Empty<string>();
	public string? ChosenFormatSummary { get; init; }
	public string? DiagnosticDetails { get; init; }
}
