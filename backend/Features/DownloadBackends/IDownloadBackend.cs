namespace TubeArr.Backend.DownloadBackends;

public interface IDownloadBackend
{
	DownloadBackendKind Kind { get; }

	Task<DownloadAttemptResult> DownloadAsync(DownloadRequest request, CancellationToken cancellationToken);
}
