using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class DatabaseBootstrap
{
	public static void EnsureDatabaseInitialized(IServiceProvider services)
	{
		using var scope = services.CreateScope();
		var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseBootstrap");
		var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();

		var sw = Stopwatch.StartNew();
		logger.LogInformation("Database initialization starting.");

		db.Database.Migrate();

		logger.LogInformation("Database migration completed in {ElapsedMs} ms.", sw.ElapsedMilliseconds);
	}

	public static void RunDeferredMaintenance(IServiceProvider services, CancellationToken cancellationToken)
	{
		using var scope = services.CreateScope();
		var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseBootstrap");
		var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();

		RunDeferredMaintenance(db, logger, cancellationToken);
	}

	public static void RunDeferredMaintenance(
		TubeArrDbContext db,
		ILogger logger,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var sw = Stopwatch.StartNew();
		logger.LogInformation("Deferred database maintenance starting.");

		var movedCount = ProgramDbQueueHelpers.MoveCompletedQueueItemsToHistoryBatched(db, logger: logger);
		logger.LogInformation(
			"Deferred queue/history cleanup completed. Moved {MovedCount} queue rows in {ElapsedMs} ms.",
			movedCount,
			sw.ElapsedMilliseconds);
	}
}