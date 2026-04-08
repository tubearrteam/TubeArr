using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TubeArr.Backend;

public sealed class TubeArrDbPersistQueueHostedService : BackgroundService
{
	readonly TubeArrDbPersistQueue _queue;
	readonly IServiceScopeFactory _scopeFactory;

	public TubeArrDbPersistQueueHostedService(TubeArrDbPersistQueue queue, IServiceScopeFactory scopeFactory)
	{
		_queue = queue;
		_scopeFactory = scopeFactory;
	}

	protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
		_queue.DrainLoopAsync(_scopeFactory, stoppingToken);
}
