using TubeArr.Backend.Data;
using TubeArr.Backend.QualityProfile;
using TubeArr.Shared.Infrastructure;

namespace TubeArr.Backend.DownloadBackends;

/// <summary>Input for a single download attempt (scoped to one queue processing call).</summary>
public sealed class DownloadRequest
{
	public int QueueId { get; init; }
	public int VideoId { get; init; }
	public int ChannelId { get; init; }

	public string YoutubeVideoId { get; init; } = "";
	public string WatchUrl { get; init; } = "";
	public string OutputDirectory { get; init; } = "";

	public DownloadBackendKind BackendKind { get; init; }

	public string ContentRoot { get; init; } = "";

	public TubeArrDbContext Db { get; init; } = null!;

	public string YtDlpExecutablePath { get; init; } = "";
	public string? CookiesPath { get; init; }
	public bool CookiesFileReadable { get; init; }
	public string? ResolvedCookiesPath { get; init; }
	public string QualityProfileConfigPath { get; init; } = "";
	public string RawQualityProfileConfigText { get; init; } = "";
	public string OutputTemplate { get; init; } = "";
	public YtDlpBuildResult? YtDlpProfileHints { get; init; }
	public string? PreferredOutputContainer { get; init; }
	public bool FfmpegConfigured { get; init; }
	public string? FfmpegExecutablePath { get; init; }

	public Func<DownloadProgressInfo, ValueTask>? OnProgress { get; init; }
	public IBrowserCookieService? BrowserCookieService { get; init; }
}

/// <summary>Aligned with <see cref="Integrations.YtDlp.YtDlpProcessRunner.DownloadProgressInfo"/>.</summary>
public readonly record struct DownloadProgressInfo(
	double? Progress,
	int? EstimatedSecondsRemaining,
	string? FormatSummary = null,
	long? DownloadedBytes = null,
	long? TotalBytes = null,
	long? SpeedBytesPerSecond = null);
