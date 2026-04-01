using System.Text.Json;

namespace TubeArr.Backend;

internal static class ChannelCustomPlaylistRulesHelper
{
	static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true
	};

	internal static IReadOnlyList<ChannelCustomPlaylistRule> ParseRules(string? json)
	{
		if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
			return Array.Empty<ChannelCustomPlaylistRule>();

		try
		{
			var list = JsonSerializer.Deserialize<List<ChannelCustomPlaylistRule>>(json, JsonOptions);
			return list ?? new List<ChannelCustomPlaylistRule>();
		}
		catch
		{
			return Array.Empty<ChannelCustomPlaylistRule>();
		}
	}

	internal static string SerializeRules(IReadOnlyList<ChannelCustomPlaylistRule> rules) =>
		JsonSerializer.Serialize(rules ?? Array.Empty<ChannelCustomPlaylistRule>(), JsonOptions);

	internal static string? ValidateRules(IReadOnlyList<ChannelCustomPlaylistRule> rules)
	{
		if (rules.Count > 5)
			return "At most 5 rules per custom playlist.";

		var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"title", "description", "sourcePlaylistId", "sourcePlaylistName", "publishedAt", "durationSeconds"
		};
		var ops = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"contains", "notContains", "equals", "notEquals", "startsWith", "endsWith", "in", "notIn", "gt", "gte", "lt", "lte"
		};

		foreach (var r in rules)
		{
			var f = (r.Field ?? "").Trim();
			if (!fields.Contains(f))
				return $"Invalid rule field: {f}";

			var op = (r.Operator ?? "").Trim();
			if (!ops.Contains(op))
				return $"Invalid rule operator: {op}";
		}

		return null;
	}

	internal static ChannelCustomPlaylistMatchType NormalizeMatchType(int raw) =>
		raw == (int)ChannelCustomPlaylistMatchType.Any
			? ChannelCustomPlaylistMatchType.Any
			: ChannelCustomPlaylistMatchType.All;
}
