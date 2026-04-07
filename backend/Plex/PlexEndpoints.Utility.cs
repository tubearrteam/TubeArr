using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace TubeArr.Backend.Plex;

internal static partial class PlexEndpoints
{
	static bool? QueryFlag(HttpRequest req, string key)
	{
		if (!req.Query.TryGetValue(key, out var v))
			return null;
		var s = v.ToString();
		if (string.IsNullOrWhiteSpace(s))
			return null;
		return s == "1" || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
	}

	static Dictionary<string, JsonElement> BuildCaseInsensitivePropertyMap(JsonElement root)
	{
		var d = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
		if (root.ValueKind != JsonValueKind.Object)
			return d;
		foreach (var p in root.EnumerateObject())
		{
			if (!d.ContainsKey(p.Name))
				d[p.Name] = p.Value;
		}
		return d;
	}

	static int GetIntFromPropertyMap(IReadOnlyDictionary<string, JsonElement> map, string name)
	{
		if (!map.TryGetValue(name, out var el))
			return 0;
		if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var i))
			return i;
		if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var s))
			return s;
		return 0;
	}

	static string CoalesceStringFromPropertyMap(IReadOnlyDictionary<string, JsonElement> map, params string[] keys)
	{
		foreach (var key in keys)
		{
			if (!map.TryGetValue(key, out var el) || el.ValueKind != JsonValueKind.String)
				continue;
			var s = el.GetString();
			if (!string.IsNullOrWhiteSpace(s))
				return s.Trim();
		}
		return "";
	}

	/// <summary>
	/// Plex may send paths only under <c>Media[].Part[].file</c>; property names may differ in casing.
	/// </summary>
	static string ExtractMatchPath(IReadOnlyDictionary<string, JsonElement> map)
	{
		foreach (var key in new[] { "filename", "path", "file", "location", "url" })
		{
			if (!map.TryGetValue(key, out var el) || el.ValueKind != JsonValueKind.String)
				continue;
			var s = el.GetString();
			if (!string.IsNullOrWhiteSpace(s))
				return s.Trim();
		}
		if (!map.TryGetValue("Media", out var mediaEl) || mediaEl.ValueKind != JsonValueKind.Array)
			return "";
		foreach (var mediaItem in mediaEl.EnumerateArray())
		{
			if (mediaItem.ValueKind != JsonValueKind.Object)
				continue;
			var mediaMap = BuildCaseInsensitivePropertyMap(mediaItem);
			if (!mediaMap.TryGetValue("Part", out var partEl) || partEl.ValueKind != JsonValueKind.Array)
				continue;
			foreach (var part in partEl.EnumerateArray())
			{
				if (part.ValueKind != JsonValueKind.Object)
					continue;
				var partMap = BuildCaseInsensitivePropertyMap(part);
				if (!partMap.TryGetValue("file", out var fileEl) || fileEl.ValueKind != JsonValueKind.String)
					continue;
				var p = fileEl.GetString();
				if (!string.IsNullOrWhiteSpace(p))
					return p.Trim();
			}
		}
		return "";
	}

	static string? TryGetString(JsonElement root, string name)
	{
		return root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;
	}

	static int TryGetInt(JsonElement root, string name)
	{
		if (!root.TryGetProperty(name, out var el))
			return 0;
		if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var i))
			return i;
		if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var s))
			return s;
		return 0;
	}
}
