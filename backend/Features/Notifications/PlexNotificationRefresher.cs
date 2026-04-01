using System.Net.Http;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class PlexNotificationRefresher
{
	public static async Task TryAfterVideoFileImportedAsync(
		TubeArrDbContext db,
		IHttpClientFactory httpClientFactory,
		string notificationSchemaJson,
		ILogger? logger,
		CancellationToken ct)
	{
		List<NotificationConnectionEntity> connections;
		try
		{
			connections = await db.NotificationConnections.AsNoTracking().ToListAsync(ct);
		}
		catch (Exception ex)
		{
			logger?.LogDebug(ex, "Notification connections not available for Plex refresh.");
			return;
		}

		if (connections.Count == 0)
			return;

		using var http = httpClientFactory.CreateClient();
		http.Timeout = TimeSpan.FromSeconds(60);

		foreach (var row in connections)
		{
			JsonObject payload;
			try
			{
				payload = NotificationPayloadMerger.MergeForApiResponse(row.PayloadJson, notificationSchemaJson, row.Id);
			}
			catch (Exception ex)
			{
				logger?.LogDebug(ex, "Skip notification id={Id}: invalid payload.", row.Id);
				continue;
			}

			if (!string.Equals(
				    payload["implementation"]?.GetValue<string>(),
				    "PlexMediaServer",
				    StringComparison.OrdinalIgnoreCase))
				continue;

			var onDownload = payload["onDownload"]?.GetValue<bool>() ?? false;
			var onImport = payload["onImportComplete"]?.GetValue<bool>() ?? false;
			if (!onDownload && !onImport)
				continue;

			if (payload["fields"] is not JsonArray fields)
				continue;

			if (!GetFieldBool(fields, "updateLibrary", true))
				continue;

			var token = GetFieldString(fields, "authToken");
			var host = GetFieldString(fields, "host");
			var port = GetFieldInt(fields, "port", 32400);
			var useSsl = GetFieldBool(fields, "useSsl", false);
			if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(host))
				continue;

			try
			{
				var (ok, msg) = await PlexTvNotificationClient.RefreshTvShowLibrariesAsync(
					http, token, host, port, useSsl, ct);
				if (!ok)
					logger?.LogWarning("Plex library refresh failed for notification id={Id}: {Message}", row.Id, msg);
				else
					logger?.LogInformation("Plex TV libraries refreshed after import (notification id={Id}).", row.Id);
			}
			catch (Exception ex)
			{
				logger?.LogWarning(ex, "Plex library refresh threw for notification id={Id}.", row.Id);
			}
		}
	}

	static string GetFieldString(JsonArray fields, string name)
	{
		foreach (var f in fields)
		{
			if (f is not JsonObject o)
				continue;
			if (!string.Equals(o["name"]?.GetValue<string>(), name, StringComparison.OrdinalIgnoreCase))
				continue;
			return o["value"]?.GetValue<string>() ?? "";
		}

		return "";
	}

	static int GetFieldInt(JsonArray fields, string name, int defaultValue)
	{
		foreach (var f in fields)
		{
			if (f is not JsonObject o)
				continue;
			if (!string.Equals(o["name"]?.GetValue<string>(), name, StringComparison.OrdinalIgnoreCase))
				continue;
			var v = o["value"];
			if (v is JsonValue jv && jv.TryGetValue<int>(out var i))
				return i;
			if (v is JsonValue jv2 && jv2.TryGetValue<double>(out var d))
				return (int)d;
		}

		return defaultValue;
	}

	static bool GetFieldBool(JsonArray fields, string name, bool defaultValue)
	{
		foreach (var f in fields)
		{
			if (f is not JsonObject o)
				continue;
			if (!string.Equals(o["name"]?.GetValue<string>(), name, StringComparison.OrdinalIgnoreCase))
				continue;
			var v = o["value"];
			if (v is JsonValue jv && jv.TryGetValue<bool>(out var b))
				return b;
		}

		return defaultValue;
	}
}
