using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class NotificationApiEndpoints
{
	static readonly JsonSerializerOptions ApiJson = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	internal static void Map(RouteGroupBuilder api)
	{
		api.MapGet("/notification/schema", () =>
			Results.Content(SystemMiscEndpoints.GetNotificationSchemaJson(), "application/json"));

		api.MapGet("/notification", async (TubeArrDbContext db, CancellationToken ct) =>
		{
			var schema = SystemMiscEndpoints.GetNotificationSchemaJson();
			var rows = await db.NotificationConnections.AsNoTracking()
				.OrderBy(x => x.Id)
				.ToListAsync(ct);
			var list = new JsonArray();
			foreach (var row in rows)
			{
				try
				{
					list.Add(NotificationPayloadMerger.MergeForApiResponse(row.PayloadJson, schema, row.Id));
				}
				catch
				{
					// skip invalid rows
				}
			}

			return Results.Content(JsonSerializer.Serialize(list, ApiJson), "application/json");
		});

		api.MapPost("/notification", async (HttpRequest req, TubeArrDbContext db, IHttpClientFactory httpFactory, CancellationToken ct) =>
		{
			using var doc = await JsonDocument.ParseAsync(req.Body, cancellationToken: ct);
			var schema = SystemMiscEndpoints.GetNotificationSchemaJson();
			JsonObject merged;
			try
			{
				merged = NotificationPayloadMerger.MergeForPersistence(doc.RootElement, schema);
			}
			catch (Exception ex)
			{
				return Results.BadRequest(new { message = ex.Message });
			}

			if (string.IsNullOrWhiteSpace(merged["name"]?.GetValue<string>()))
				merged["name"] = merged["implementationName"]?.GetValue<string>() ?? "Connection";
			if (!merged.ContainsKey("tags"))
				merged["tags"] = new JsonArray();

			var entity = new NotificationConnectionEntity
			{
				PayloadJson = merged.ToJsonString()
			};
			db.NotificationConnections.Add(entity);
			await db.SaveChangesAsync(ct);

			var response = NotificationPayloadMerger.MergeForApiResponse(entity.PayloadJson, schema, entity.Id);
			return Results.Content(
				JsonSerializer.Serialize(response, ApiJson),
				"application/json",
				statusCode: StatusCodes.Status201Created);
		});

		api.MapPut("/notification/{id:int}", async (int id, HttpRequest req, TubeArrDbContext db, CancellationToken ct) =>
		{
			var entity = await db.NotificationConnections.FirstOrDefaultAsync(x => x.Id == id, ct);
			if (entity is null)
				return Results.NotFound();

			using var doc = await JsonDocument.ParseAsync(req.Body, cancellationToken: ct);
			var schema = SystemMiscEndpoints.GetNotificationSchemaJson();
			JsonObject merged;
			try
			{
				merged = NotificationPayloadMerger.MergeForPersistence(doc.RootElement, schema);
			}
			catch (Exception ex)
			{
				return Results.BadRequest(new { message = ex.Message });
			}

			if (string.IsNullOrWhiteSpace(merged["name"]?.GetValue<string>()))
				merged["name"] = merged["implementationName"]?.GetValue<string>() ?? "Connection";
			if (!merged.ContainsKey("tags"))
				merged["tags"] = new JsonArray();

			entity.PayloadJson = merged.ToJsonString();
			await db.SaveChangesAsync(ct);

			var response = NotificationPayloadMerger.MergeForApiResponse(entity.PayloadJson, schema, entity.Id);
			return Results.Content(JsonSerializer.Serialize(response, ApiJson), "application/json");
		});

		api.MapDelete("/notification/{id:int}", async (int id, TubeArrDbContext db, CancellationToken ct) =>
		{
			var n = await db.NotificationConnections.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
			return n == 0 ? Results.NotFound() : Results.NoContent();
		});

		api.MapPost("/notification/test", async (HttpRequest req, IHttpClientFactory httpFactory, CancellationToken ct) =>
		{
			using var doc = await JsonDocument.ParseAsync(req.Body, cancellationToken: ct);
			var root = doc.RootElement;
			var impl = root.TryGetProperty("implementation", out var implEl) ? implEl.GetString() : null;
			if (!string.Equals(impl, "PlexMediaServer", StringComparison.OrdinalIgnoreCase))
				return Results.BadRequest(new { message = "Test is not implemented for this connection type." });

			var fields = GetFieldsNode(root);
			if (fields is null)
				return Results.BadRequest(new { message = "fields required." });

			var token = GetFieldString(fields, "authToken");
			var host = GetFieldString(fields, "host");
			var port = GetFieldInt(fields, "port", 32400);
			var useSsl = GetFieldBool(fields, "useSsl", false);
			var notify = GetFieldBool(fields, "notify", true);

			using var http = httpFactory.CreateClient();
			http.Timeout = TimeSpan.FromSeconds(60);
			try
			{
				var (ok, message) = await PlexTvNotificationClient.TestConnectionAsync(
					http, token, host, port, useSsl, notify, ct);
				if (!ok)
					return Results.BadRequest(new { message });

				return Results.Json(new { isValid = true });
			}
			catch (Exception ex)
			{
				return Results.BadRequest(new { message = ex.Message });
			}
		});

		api.MapPost("/notification/action/{action}", async (string action, HttpRequest req, IHttpClientFactory httpFactory, CancellationToken ct) =>
		{
			using var doc = await JsonDocument.ParseAsync(req.Body, cancellationToken: ct);
			var root = doc.RootElement;
			var a = (action ?? "").Trim().ToLowerInvariant();

			if (a == "servers")
			{
				var fields = GetFieldsNode(root);
				var token = fields is not null ? GetFieldString(fields, "authToken") : "";
				if (string.IsNullOrWhiteSpace(token))
					return Results.Json(new { options = Array.Empty<object>() });

				try
				{
					using var http = httpFactory.CreateClient();
					http.Timeout = TimeSpan.FromSeconds(60);
					var xml = await PlexTvNotificationClient.GetResourcesXmlAsync(http, token, ct);
					var servers = PlexTvNotificationClient.ParseServerOptions(xml);
					var options = new List<object>();
					var order = 0;
					foreach (var s in servers)
					{
						order++;
						options.Add(new
						{
							value = s.ClientIdentifier,
							name = s.DisplayName,
							hint = $"{s.Host}:{s.Port}",
							order,
							additionalProperties = new Dictionary<string, object?>
							{
								["host"] = s.Host,
								["port"] = s.Port,
								["useSsl"] = s.UseSsl,
								["machineIdentifier"] = s.ClientIdentifier,
								["server"] = s.ClientIdentifier
							}
						});
					}

					return Results.Json(new { options });
				}
				catch (Exception ex)
				{
					return Results.BadRequest(new { message = ex.Message });
				}
			}

			if (a == "startplexpin")
			{
				try
				{
					using var http = httpFactory.CreateClient();
					http.Timeout = TimeSpan.FromSeconds(30);
					var (pinId, code) = await PlexTvNotificationClient.StartPinAsync(http, ct);
					return Results.Json(new
					{
						id = pinId,
						code,
						link = PlexTvNotificationClient.BuildAppAuthLink(code)
					});
				}
				catch (Exception ex)
				{
					return Results.BadRequest(new { message = ex.Message });
				}
			}

			if (a == "checkplexpin")
			{
				if (!root.TryGetProperty("pinId", out var pinProp) || !pinProp.TryGetInt32(out var pinId))
					return Results.BadRequest(new { message = "pinId required." });
				string? pinCode = null;
				if (root.TryGetProperty("code", out var codeProp) && codeProp.ValueKind == JsonValueKind.String)
					pinCode = codeProp.GetString();
				try
				{
					using var http = httpFactory.CreateClient();
					http.Timeout = TimeSpan.FromSeconds(30);
					var tok = await PlexTvNotificationClient.TryGetPinAuthTokenAsync(http, pinId, pinCode, ct);
					if (string.IsNullOrWhiteSpace(tok))
						return Results.Json(new { authenticated = false });
					return Results.Json(new { authenticated = true, authToken = tok });
				}
				catch (Exception ex)
				{
					return Results.BadRequest(new { message = ex.Message });
				}
			}

			return Results.NotFound();
		});
	}

	static JsonArray? GetFieldsNode(JsonElement root)
	{
		if (!root.TryGetProperty("fields", out var f) || f.ValueKind != JsonValueKind.Array)
			return null;
		return JsonNode.Parse(f.GetRawText()) as JsonArray;
	}

	static string GetFieldString(JsonArray fields, string name)
	{
		foreach (var node in fields)
		{
			if (node is not JsonObject o)
				continue;
			if (!string.Equals(o["name"]?.GetValue<string>(), name, StringComparison.OrdinalIgnoreCase))
				continue;
			return o["value"]?.GetValue<string>() ?? "";
		}

		return "";
	}

	static int GetFieldInt(JsonArray fields, string name, int defaultValue)
	{
		foreach (var node in fields)
		{
			if (node is not JsonObject o)
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
		foreach (var node in fields)
		{
			if (node is not JsonObject o)
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
