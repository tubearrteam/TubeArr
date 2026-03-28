namespace TubeArr.Backend.DownloadBackends;

public sealed class DownloadBackendRouter
{
	readonly IReadOnlyDictionary<DownloadBackendKind, IDownloadBackend> _backends;

	public DownloadBackendRouter(IEnumerable<IDownloadBackend> backends)
	{
		_backends = backends.ToDictionary(b => b.Kind);
	}

	public IDownloadBackend Get(DownloadBackendKind kind) =>
		_backends.TryGetValue(kind, out var b)
			? b
			: throw new InvalidOperationException($"No download backend registered for {kind}.");
}
