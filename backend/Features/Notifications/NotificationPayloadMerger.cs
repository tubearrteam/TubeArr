using System.Text.Json;
using System.Text.Json.Nodes;

namespace TubeArr.Backend;

internal static class NotificationPayloadMerger
{
	public static JsonObject MergeForPersistence(JsonElement postedBody, string schemaJson)
	{
		var schemaArray = JsonNode.Parse(schemaJson)!.AsArray();
		if (!postedBody.TryGetProperty("implementation", out var implEl))
			throw new InvalidOperationException("implementation is required.");
		var impl = implEl.GetString() ?? "";
		var schemaItem = FindSchemaItem(schemaArray, impl)
			?? throw new InvalidOperationException($"Unknown notification implementation: {impl}");

		var merged = JsonNode.Parse(postedBody.GetRawText()) as JsonObject ?? new JsonObject();

		foreach (var p in schemaItem.AsObject())
		{
			if (p.Key.StartsWith("supports", StringComparison.OrdinalIgnoreCase) && !merged.ContainsKey(p.Key) && p.Value is not null)
				merged[p.Key] = JsonNode.Parse(p.Value.ToJsonString());
		}

		if (!merged.ContainsKey("implementationName") && schemaItem["implementationName"] is not null)
			merged["implementationName"] = JsonNode.Parse(schemaItem["implementationName"]!.ToJsonString());
		if (!merged.ContainsKey("infoLink") && schemaItem["infoLink"] is not null)
			merged["infoLink"] = JsonNode.Parse(schemaItem["infoLink"]!.ToJsonString());

		if (schemaItem["fields"] is JsonArray fieldTemplates)
			merged["fields"] = MergeFieldTemplates(fieldTemplates, merged["fields"]);
		else if (!merged.ContainsKey("fields"))
			merged["fields"] = new JsonArray();

		merged.Remove("id");
		return merged;
	}

	public static JsonObject MergeForApiResponse(string storedPayloadJson, string schemaJson, int id)
	{
		var schemaArray = JsonNode.Parse(schemaJson)!.AsArray();
		var stored = JsonNode.Parse(string.IsNullOrWhiteSpace(storedPayloadJson) ? "{}" : storedPayloadJson) as JsonObject
			?? new JsonObject();

		if (!stored.TryGetPropertyValue("implementation", out var implNode) || implNode is null)
			throw new InvalidOperationException("Stored notification payload is missing implementation.");

		var impl = implNode.GetValue<string>();
		var schemaItem = FindSchemaItem(schemaArray, impl);
		if (schemaItem is null)
		{
			stored["id"] = id;
			return stored;
		}

		foreach (var p in schemaItem.AsObject())
		{
			if (p.Key.StartsWith("supports", StringComparison.OrdinalIgnoreCase) && !stored.ContainsKey(p.Key) && p.Value is not null)
				stored[p.Key] = JsonNode.Parse(p.Value.ToJsonString());
		}

		if (!stored.ContainsKey("implementationName") && schemaItem["implementationName"] is not null)
			stored["implementationName"] = JsonNode.Parse(schemaItem["implementationName"]!.ToJsonString());
		if (!stored.ContainsKey("infoLink") && schemaItem["infoLink"] is not null)
			stored["infoLink"] = JsonNode.Parse(schemaItem["infoLink"]!.ToJsonString());

		if (schemaItem["fields"] is JsonArray fieldTemplates)
			stored["fields"] = MergeFieldTemplates(fieldTemplates, stored["fields"]);

		stored["id"] = id;
		return stored;
	}

	static JsonObject? FindSchemaItem(JsonArray schemaArray, string implementation)
	{
		foreach (var n in schemaArray)
		{
			if (n is not JsonObject o)
				continue;
			if (o.TryGetPropertyValue("implementation", out var implNode) &&
			    string.Equals(implNode?.GetValue<string>(), implementation, StringComparison.OrdinalIgnoreCase))
				return o;
		}

		return null;
	}

	static JsonArray MergeFieldTemplates(JsonArray fieldTemplates, JsonNode? existingFields)
	{
		var values = ExtractFieldValues(existingFields);
		var outArr = new JsonArray();
		foreach (var tmpl in fieldTemplates)
		{
			if (tmpl is not JsonObject templateObj)
				continue;
			var clone = JsonNode.Parse(templateObj.ToJsonString())!.AsObject();
			if (!clone.TryGetPropertyValue("name", out var nameNode) || nameNode is null)
				continue;
			var name = nameNode.GetValue<string>();
			if (values.TryGetValue(name, out var v))
				clone["value"] = v is null ? null : JsonNode.Parse(v.ToJsonString());
			outArr.Add(clone);
		}

		return outArr;
	}

	static Dictionary<string, JsonNode?> ExtractFieldValues(JsonNode? existingFields)
	{
		var d = new Dictionary<string, JsonNode?>(StringComparer.OrdinalIgnoreCase);
		if (existingFields is not JsonArray arr)
			return d;
		foreach (var el in arr)
		{
			if (el is not JsonObject o)
				continue;
			if (!o.TryGetPropertyValue("name", out var n) || n is null)
				continue;
			var key = n.GetValue<string>();
			o.TryGetPropertyValue("value", out var val);
			d[key] = val;
		}

		return d;
	}
}
