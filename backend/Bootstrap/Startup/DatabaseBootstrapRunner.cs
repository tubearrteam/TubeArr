using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace TubeArr.Backend;

public static class DatabaseBootstrapRunner
{
	public static void InitializeDatabaseWithLogging(this WebApplication app)
	{
		app.Logger.LogInformation("Database initialization starting.");
		var sw = Stopwatch.StartNew();
		DatabaseBootstrap.EnsureDatabaseInitialized(app.Services);
		app.Logger.LogInformation("Database initialization completed in {ElapsedMs} ms.", sw.ElapsedMilliseconds);
	}
}
