using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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
			var settings = await db.ServerSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1) ?? new ServerSettingsEntity();
			if (!IsApiKeyEnforced(settings))
			{
				await next();
				return;
			}

			var expected = settings.ApiKey ?? string.Empty;
			var provided = context.Request.Headers["X-Api-Key"].FirstOrDefault();
			if (string.IsNullOrWhiteSpace(provided))
				provided = context.Request.Query["apikey"].FirstOrDefault();
			if (string.IsNullOrWhiteSpace(provided) && context.Request.Path.StartsWithSegments("/signalr", StringComparison.OrdinalIgnoreCase))
				provided = context.Request.Query["access_token"].FirstOrDefault();
			if (string.IsNullOrWhiteSpace(provided) && context.Request.Path.StartsWithSegments("/api/v1/signalr", StringComparison.OrdinalIgnoreCase))
				provided = context.Request.Query["access_token"].FirstOrDefault();

			if (string.IsNullOrWhiteSpace(expected) || !string.Equals(expected, provided, StringComparison.Ordinal))
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

	static bool IsApiKeyEnforced(ServerSettingsEntity settings)
	{
		var authRequired = settings.AuthenticationRequired?.Trim() ?? "enabled";
		var authMethod = settings.AuthenticationMethod?.Trim() ?? "none";
		return !authRequired.Equals("disabled", StringComparison.OrdinalIgnoreCase)
			&& authMethod.Equals("apikey", StringComparison.OrdinalIgnoreCase);
	}
}
