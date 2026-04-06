using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class ApiSecurityMiddlewareExtensions
{
	internal static void UseTubeArrApiSecurity(this WebApplication app)
	{
		app.Use(async (context, next) =>
		{
			if (!RequiresProtection(context.Request.Path))
			{
				await next();
				return;
			}

			var db = context.RequestServices.GetRequiredService<TubeArrDbContext>();
			var cache = context.RequestServices.GetRequiredService<ApiSecuritySettingsCache>();
			var snap = await cache.GetAsync(db, context.RequestAborted);
			if (!snap.ApiKeyEnforced)
			{
				await next();
				return;
			}

			if (snap.ExpectedKeySha256 is null)
			{
				context.Response.StatusCode = StatusCodes.Status401Unauthorized;
				return;
			}

			var provided = context.Request.Headers["X-Api-Key"].FirstOrDefault();
			if (string.IsNullOrWhiteSpace(provided))
				provided = context.Request.Query["apikey"].FirstOrDefault();
			if (string.IsNullOrWhiteSpace(provided) && context.Request.Path.StartsWithSegments("/signalr", StringComparison.OrdinalIgnoreCase))
				provided = context.Request.Query["access_token"].FirstOrDefault();
			if (string.IsNullOrWhiteSpace(provided) && context.Request.Path.StartsWithSegments("/api/v1/signalr", StringComparison.OrdinalIgnoreCase))
				provided = context.Request.Query["access_token"].FirstOrDefault();

			if (!ApiSecuritySettingsCache.FixedTimeApiKeyEquals(snap.ExpectedKeySha256, provided))
			{
				context.Response.StatusCode = StatusCodes.Status401Unauthorized;
				return;
			}

			await next();
		});
	}

	static bool RequiresProtection(PathString path) =>
		path.StartsWithSegments("/api/v1", StringComparison.OrdinalIgnoreCase)
		|| path.StartsWithSegments("/signalr", StringComparison.OrdinalIgnoreCase);
}
