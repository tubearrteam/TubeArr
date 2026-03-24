using Microsoft.AspNetCore.Builder;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static partial class QualityProfileAndConfigEndpoints
{
	static partial void MapServerSettingsEndpoints(RouteGroupBuilder api)
	{
		api.MapGet("/server/settings", async (TubeArrDbContext db) =>
		{
			var serverSettings = await ProgramStartupHelpers.GetOrCreateServerSettingsAsync(db);
			return Results.Json(ToServerSettingsResource(serverSettings));
		});

		api.MapGet("/config/host", async (TubeArrDbContext db) =>
		{
			var serverSettings = await ProgramStartupHelpers.GetOrCreateServerSettingsAsync(db);
			return Results.Json(ToServerSettingsResource(serverSettings));
		});

		api.MapPut("/server/settings", async (ServerSettingsResource request, TubeArrDbContext db) =>
		{
			var failures = ValidateServerSettings(request);
			if (failures.Count > 0)
				return Results.Json(failures.ToArray(), statusCode: 400);

			var serverSettings = await ProgramStartupHelpers.GetOrCreateServerSettingsAsync(db);
			ApplyServerSettings(request, serverSettings);
			await db.SaveChangesAsync();
			return Results.Json(ToServerSettingsResource(serverSettings));
		});

		api.MapPut("/config/host", async (ServerSettingsResource request, TubeArrDbContext db) =>
		{
			var failures = ValidateServerSettings(request);
			if (failures.Count > 0)
				return Results.Json(failures.ToArray(), statusCode: 400);

			var serverSettings = await ProgramStartupHelpers.GetOrCreateServerSettingsAsync(db);
			ApplyServerSettings(request, serverSettings);
			await db.SaveChangesAsync();
			return Results.Json(ToServerSettingsResource(serverSettings));
		});
	}

	private static List<object> ValidateServerSettings(ServerSettingsResource request)
	{
		var failures = new List<object>();

		static void AddFailure(List<object> list, string propertyName, string errorMessage, bool isWarning = false)
		{
			list.Add(new
			{
				propertyName,
				errorMessage,
				isWarning,
				infoLink = (string?)null,
				detailedDescription = (string?)null
			});
		}

		if (request.Port is < 1 or > 65535)
			AddFailure(failures, "port", "Port must be between 1 and 65535.");
		if (request.EnableSsl && (request.SslPort is < 1 or > 65535))
			AddFailure(failures, "sslPort", "SSL port must be between 1 and 65535.");
		if (request.ProxyEnabled && (request.ProxyPort is < 1 or > 65535))
			AddFailure(failures, "proxyPort", "Proxy port must be between 1 and 65535.");
		if (request.LogSizeLimit is < 1 or > 10)
			AddFailure(failures, "logSizeLimit", "Log size limit must be between 1 and 10.");

		var authEnabled = !string.Equals(request.AuthenticationMethod, "none", StringComparison.OrdinalIgnoreCase);
		if (authEnabled && !string.IsNullOrWhiteSpace(request.Password) && request.Password != request.PasswordConfirmation)
			AddFailure(failures, "passwordConfirmation", "Passwords do not match.");

		return failures;
	}

	private static ServerSettingsResource ToServerSettingsResource(ServerSettingsEntity entity)
	{
		return new ServerSettingsResource(
			Id: entity.Id,
			BindAddress: entity.BindAddress,
			Port: entity.Port,
			SslPort: entity.SslPort,
			EnableSsl: entity.EnableSsl,
			LaunchBrowser: entity.LaunchBrowser,
			AuthenticationMethod: entity.AuthenticationMethod,
			AuthenticationRequired: entity.AuthenticationRequired,
			AnalyticsEnabled: entity.AnalyticsEnabled,
			Username: entity.Username,
			Password: "",
			PasswordConfirmation: "",
			LogLevel: entity.LogLevel,
			ConsoleLogLevel: entity.ConsoleLogLevel,
			Branch: entity.Branch,
			ApiKey: entity.ApiKey,
			SslCertPath: entity.SslCertPath,
			SslCertPassword: "",
			UrlBase: entity.UrlBase,
			InstanceName: entity.InstanceName,
			ApplicationUrl: entity.ApplicationUrl,
			UpdateAutomatically: entity.UpdateAutomatically,
			UpdateMechanism: entity.UpdateMechanism,
			UpdateScriptPath: entity.UpdateScriptPath,
			ProxyEnabled: entity.ProxyEnabled,
			ProxyType: entity.ProxyType,
			ProxyHostname: entity.ProxyHostname,
			ProxyPort: entity.ProxyPort,
			ProxyUsername: entity.ProxyUsername,
			ProxyPassword: "",
			ProxyBypassFilter: entity.ProxyBypassFilter,
			ProxyBypassLocalAddresses: entity.ProxyBypassLocalAddresses,
			CertificateValidation: entity.CertificateValidation,
			BackupFolder: entity.BackupFolder,
			BackupInterval: entity.BackupInterval,
			BackupRetention: entity.BackupRetention,
			LogSizeLimit: entity.LogSizeLimit
		);
	}

	private static void ApplyServerSettings(ServerSettingsResource request, ServerSettingsEntity serverSettings)
	{
		serverSettings.BindAddress = request.BindAddress?.Trim() ?? "";
		serverSettings.Port = request.Port;
		serverSettings.UrlBase = request.UrlBase?.Trim() ?? "";
		serverSettings.InstanceName = request.InstanceName?.Trim() ?? "";
		serverSettings.ApplicationUrl = request.ApplicationUrl?.Trim() ?? "";
		serverSettings.EnableSsl = request.EnableSsl;
		serverSettings.SslPort = request.SslPort;
		serverSettings.SslCertPath = request.SslCertPath?.Trim() ?? "";
		if (!string.IsNullOrWhiteSpace(request.SslCertPassword))
			serverSettings.SslCertPassword = request.SslCertPassword;
		serverSettings.LaunchBrowser = request.LaunchBrowser;
		serverSettings.AuthenticationMethod = request.AuthenticationMethod?.Trim() ?? "none";
		serverSettings.AuthenticationRequired = request.AuthenticationRequired?.Trim() ?? "enabled";
		serverSettings.Username = request.Username?.Trim() ?? "";
		if (!string.IsNullOrWhiteSpace(request.Password))
			serverSettings.Password = request.Password;
		if (!string.IsNullOrWhiteSpace(request.ApiKey))
			serverSettings.ApiKey = request.ApiKey.Trim();
		if (string.IsNullOrWhiteSpace(serverSettings.ApiKey))
			serverSettings.ApiKey = ProgramStartupHelpers.GenerateApiKey();
		serverSettings.CertificateValidation = request.CertificateValidation?.Trim() ?? "enabled";
		serverSettings.ProxyEnabled = request.ProxyEnabled;
		serverSettings.ProxyType = request.ProxyType?.Trim() ?? "http";
		serverSettings.ProxyHostname = request.ProxyHostname?.Trim() ?? "";
		serverSettings.ProxyPort = request.ProxyPort;
		serverSettings.ProxyUsername = request.ProxyUsername?.Trim() ?? "";
		if (!string.IsNullOrWhiteSpace(request.ProxyPassword))
			serverSettings.ProxyPassword = request.ProxyPassword;
		serverSettings.ProxyBypassFilter = request.ProxyBypassFilter?.Trim() ?? "";
		serverSettings.ProxyBypassLocalAddresses = request.ProxyBypassLocalAddresses;
		serverSettings.LogLevel = request.LogLevel?.Trim() ?? "info";
		serverSettings.ConsoleLogLevel = request.ConsoleLogLevel?.Trim() ?? serverSettings.LogLevel;
		serverSettings.LogSizeLimit = request.LogSizeLimit;
		serverSettings.AnalyticsEnabled = request.AnalyticsEnabled;
		serverSettings.Branch = request.Branch?.Trim() ?? "main";
		serverSettings.UpdateAutomatically = request.UpdateAutomatically;
		serverSettings.UpdateMechanism = request.UpdateMechanism?.Trim() ?? "builtIn";
		serverSettings.UpdateScriptPath = request.UpdateScriptPath?.Trim() ?? "";
		serverSettings.BackupFolder = request.BackupFolder?.Trim() ?? "";
		serverSettings.BackupInterval = request.BackupInterval;
		serverSettings.BackupRetention = request.BackupRetention;
	}
}
