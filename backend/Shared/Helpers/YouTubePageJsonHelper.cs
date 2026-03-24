using System.Text.Json;
using System.Text.RegularExpressions;

namespace TubeArr.Backend;

public static class YouTubePageJsonHelper
{
	public static JsonDocument? TryExtractJsonDocument(string html, params string[] markers)
	{
		if (!TryExtractJson(html, markers, out var json))
			return null;

		try
		{
			return JsonDocument.Parse(json);
		}
		catch
		{
			return null;
		}
	}

	public static bool TryExtractJson(string html, IReadOnlyList<string> markers, out string json)
	{
		json = string.Empty;
		if (string.IsNullOrWhiteSpace(html))
			return false;

		foreach (var marker in markers)
		{
			if (string.IsNullOrWhiteSpace(marker))
				continue;

			var markerIndex = html.IndexOf(marker, StringComparison.Ordinal);
			if (markerIndex < 0)
				continue;

			var startIndex = html.IndexOf('{', markerIndex + marker.Length);
			if (startIndex < 0)
				continue;

			if (!TryFindJsonObjectEnd(html, startIndex, out var endIndex))
				continue;

			json = html.Substring(startIndex, endIndex - startIndex + 1);
			return true;
		}

		return false;
	}

	public static string? TryExtractInnertubeApiKey(string html, JsonDocument? ytcfg = null)
	{
		if (ytcfg is not null &&
			ytcfg.RootElement.ValueKind == JsonValueKind.Object &&
			ytcfg.RootElement.TryGetProperty("INNERTUBE_API_KEY", out var apiKeyElement) &&
			apiKeyElement.ValueKind == JsonValueKind.String)
		{
			var apiKey = apiKeyElement.GetString()?.Trim();
			if (!string.IsNullOrWhiteSpace(apiKey))
				return apiKey;
		}

		var match = Regex.Match(html ?? string.Empty, @"""INNERTUBE_API_KEY""\s*:\s*""([^""]+)""");
		return match.Success ? match.Groups[1].Value.Trim() : null;
	}

	public static string? TryExtractInnertubeContextJson(JsonDocument? ytcfg)
	{
		if (ytcfg is null || ytcfg.RootElement.ValueKind != JsonValueKind.Object)
			return null;

		if (!ytcfg.RootElement.TryGetProperty("INNERTUBE_CONTEXT", out var contextElement) ||
			contextElement.ValueKind != JsonValueKind.Object)
			return null;

		return contextElement.GetRawText();
	}

	static bool TryFindJsonObjectEnd(string value, int startIndex, out int endIndex)
	{
		endIndex = -1;
		var depth = 0;
		var inString = false;
		var escape = false;

		for (var i = startIndex; i < value.Length; i++)
		{
			var c = value[i];

			if (inString)
			{
				if (escape)
				{
					escape = false;
					continue;
				}

				if (c == '\\')
				{
					escape = true;
					continue;
				}

				if (c == '"')
				{
					inString = false;
				}

				continue;
			}

			if (c == '"')
			{
				inString = true;
				continue;
			}

			if (c == '{')
			{
				depth++;
				continue;
			}

			if (c != '}')
				continue;

			depth--;
			if (depth == 0)
			{
				endIndex = i;
				return true;
			}
		}

		return false;
	}
}
