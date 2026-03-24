using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using TubeArr.Backend;

namespace TubeArr.Backend;

public static class WebHostExtensions
{
	public sealed record TubeArrPreload(string ConnectionString, string PreloadedUrlBase);

	public static TubeArrPreload ConfigureTubeArrPreload(this WebApplicationBuilder builder)
	{
		var defaultDbPath = Path.Combine(builder.Environment.ContentRootPath, "TubeArr.db");
		var connectionString = builder.Configuration.GetConnectionString("TubeArr") ?? $"Data Source={defaultDbPath}";
		connectionString = SqliteConnectionPaths.NormalizeConnectionStringForConcurrency(connectionString);

		using var restoreLogFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
		var restoreLogger = restoreLogFactory.CreateLogger("DatabaseRestore");
		SqliteConnectionPaths.ApplyPendingRestoreIfPresent(connectionString, builder.Environment.ContentRootPath, restoreLogger);

		var preloadedServerSettings = ProgramStartupHelpers.TryLoadServerSettingsPreload(connectionString);
		var preloadedUrlBase = ProgramStartupHelpers.NormalizeUrlBase(preloadedServerSettings?.UrlBase);

		// Insert urlBase rewriting at the start of the pipeline (before routing).
		builder.Services.AddSingleton<IStartupFilter>(_ => new UrlBaseStartupFilter(preloadedUrlBase));

		// If the process is started with explicit urls (e.g. `dotnet run --urls` or ASPNETCORE_URLS),
		// do not override them with UI settings.
		var hasExplicitUrls = !string.IsNullOrWhiteSpace(builder.Configuration["urls"]);
		if (!hasExplicitUrls)
		{
			var bindAddress = preloadedServerSettings?.BindAddress ?? "*";
			var port = preloadedServerSettings?.Port ?? 5075;
			var enableSsl = preloadedServerSettings?.EnableSsl ?? false;
			var sslPort = preloadedServerSettings?.SslPort ?? 9898;
			var sslCertPath = preloadedServerSettings?.SslCertPath ?? "";
			var sslCertPassword = preloadedServerSettings?.SslCertPassword ?? "";

			builder.WebHost.ConfigureKestrel(options =>
			{
				var address = ProgramStartupHelpers.TryGetListenAddress(bindAddress);

				if (address is null)
				{
					options.ListenAnyIP(port);
				}
				else
				{
					options.Listen(address, port);
				}

				if (!enableSsl)
				{
					return;
				}

				try
				{
					if (string.IsNullOrWhiteSpace(sslCertPath) || !File.Exists(sslCertPath))
					{
						return;
					}

					var cert = string.IsNullOrWhiteSpace(sslCertPassword)
						? new X509Certificate2(sslCertPath)
						: new X509Certificate2(sslCertPath, sslCertPassword);

					if (address is null)
					{
						options.ListenAnyIP(sslPort, listen => listen.UseHttps(cert));
					}
					else
					{
						options.Listen(address, sslPort, listen => listen.UseHttps(cert));
					}
				}
				catch
				{
					// If HTTPS binding fails (bad cert, unsupported format, etc.) we keep HTTP working.
				}
			});
		}

		return new TubeArrPreload(connectionString, preloadedUrlBase);
	}
}

