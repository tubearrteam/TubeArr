using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace TubeArr.Backend;

/// <summary>Input fields for matching a video against custom playlist rules.</summary>
public readonly record struct CustomPlaylistVideoContext(
	string Title,
	string? Description,
	string? PrimarySourcePlaylistYoutubeId,
	string? PrimarySourcePlaylistName,
	IReadOnlyList<string> AllSourcePlaylistYoutubeIds,
	IReadOnlyList<string> AllSourcePlaylistNames,
	DateTimeOffset PublishedAtUtc,
	int DurationSeconds);

internal static class ChannelCustomPlaylistEvaluator
{
	internal static bool Matches(
		IReadOnlyList<ChannelCustomPlaylistRule> rules,
		ChannelCustomPlaylistMatchType matchType,
		CustomPlaylistVideoContext ctx)
	{
		if (rules.Count == 0)
			return true;

		return matchType switch
		{
			ChannelCustomPlaylistMatchType.Any => rules.Any(r => EvaluateRule(r, ctx)),
			_ => rules.All(r => EvaluateRule(r, ctx))
		};
	}

	static bool EvaluateRule(ChannelCustomPlaylistRule rule, CustomPlaylistVideoContext ctx)
	{
		var op = (rule.Operator ?? "").Trim();
		var field = (rule.Field ?? "").Trim();
		return field switch
		{
			"title" => EvalStringField(ctx.Title ?? "", op, rule.Value),
			"description" => EvalStringField(ctx.Description ?? "", op, rule.Value),
			"sourcePlaylistId" => EvalPlaylistIdField(ctx, op, rule.Value),
			"sourcePlaylistName" => EvalPlaylistNameField(ctx, op, rule.Value),
			"publishedAt" => EvalPublishedAtField(ctx.PublishedAtUtc, op, rule.Value),
			"durationSeconds" => EvalDurationField(ctx.DurationSeconds, op, rule.Value),
			_ => false
		};
	}

	static bool EvalDurationField(int durationSeconds, string op, JsonElement? valueEl)
	{
		if (!TryGetDouble(valueEl, out var n))
			return false;
		var rhs = (int)Math.Round(n);
		return op switch
		{
			"equals" => durationSeconds == rhs,
			"notEquals" => durationSeconds != rhs,
			"gt" => durationSeconds > rhs,
			"gte" => durationSeconds >= rhs,
			"lt" => durationSeconds < rhs,
			"lte" => durationSeconds <= rhs,
			_ => false
		};
	}

	static bool EvalPublishedAtField(DateTimeOffset publishedAtUtc, string op, JsonElement? valueEl)
	{
		if (!TryGetDateTimeOffset(valueEl, out var rhs))
			return false;
		return op switch
		{
			"equals" => publishedAtUtc == rhs,
			"notEquals" => publishedAtUtc != rhs,
			"gt" => publishedAtUtc > rhs,
			"gte" => publishedAtUtc >= rhs,
			"lt" => publishedAtUtc < rhs,
			"lte" => publishedAtUtc <= rhs,
			_ => false
		};
	}

	static bool EvalPlaylistNameField(CustomPlaylistVideoContext ctx, string op, JsonElement? valueEl)
	{
		var primary = ctx.PrimarySourcePlaylistName ?? "";
		if (string.Equals(op, "in", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(op, "notIn", StringComparison.OrdinalIgnoreCase))
		{
			var set = ParseStringSet(valueEl);
			if (set.Count == 0)
				return string.Equals(op, "notIn", StringComparison.OrdinalIgnoreCase);

			var any = ctx.AllSourcePlaylistNames.Any(n =>
				set.Contains((n ?? "").Trim(), StringComparer.OrdinalIgnoreCase));
			return string.Equals(op, "in", StringComparison.OrdinalIgnoreCase) ? any : !any;
		}

		return EvalStringField(primary, op, valueEl);
	}

	static bool EvalPlaylistIdField(CustomPlaylistVideoContext ctx, string op, JsonElement? valueEl)
	{
		var primary = ctx.PrimarySourcePlaylistYoutubeId ?? "";
		if (string.Equals(op, "in", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(op, "notIn", StringComparison.OrdinalIgnoreCase))
		{
			var set = ParseStringSet(valueEl);
			if (set.Count == 0)
				return string.Equals(op, "notIn", StringComparison.OrdinalIgnoreCase);

			var any = ctx.AllSourcePlaylistYoutubeIds.Any(id =>
				set.Contains((id ?? "").Trim(), StringComparer.Ordinal));
			return string.Equals(op, "in", StringComparison.OrdinalIgnoreCase) ? any : !any;
		}

		return EvalStringField(primary, op, valueEl, StringComparison.Ordinal);
	}

	static bool EvalStringField(string haystack, string op, JsonElement? valueEl, StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
	{
		if (!TryGetString(valueEl, out var needle))
			return false;
		return op switch
		{
			"contains" => haystack.Contains(needle, stringComparison),
			"notContains" => !haystack.Contains(needle, stringComparison),
			"equals" => string.Equals(haystack, needle, stringComparison),
			"notEquals" => !string.Equals(haystack, needle, stringComparison),
			"startsWith" => haystack.StartsWith(needle, stringComparison),
			"endsWith" => haystack.EndsWith(needle, stringComparison),
			_ => false
		};
	}

	static HashSet<string> ParseStringSet(JsonElement? valueEl)
	{
		var set = new HashSet<string>(StringComparer.Ordinal);
		var el = valueEl.GetValueOrDefault();
		if (el.ValueKind == JsonValueKind.Undefined || el.ValueKind == JsonValueKind.Null)
			return set;
		if (el.ValueKind == JsonValueKind.Array)
		{
			foreach (var item in el.EnumerateArray())
			{
				if (item.ValueKind == JsonValueKind.String)
					set.Add(item.GetString() ?? "");
				else if (item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out var n))
					set.Add(n.ToString(CultureInfo.InvariantCulture));
			}
		}
		else if (el.ValueKind == JsonValueKind.String)
		{
			foreach (var part in (el.GetString() ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
				set.Add(part);
		}

		return set;
	}

	static bool TryGetString(JsonElement? valueEl, out string s)
	{
		s = "";
		var el = valueEl.GetValueOrDefault();
		if (el.ValueKind == JsonValueKind.Undefined || el.ValueKind == JsonValueKind.Null)
			return false;
		if (el.ValueKind == JsonValueKind.String)
		{
			s = el.GetString() ?? "";
			return true;
		}

		if (el.ValueKind == JsonValueKind.Number)
		{
			s = el.GetRawText();
			return true;
		}

		return false;
	}

	static bool TryGetDouble(JsonElement? valueEl, out double n)
	{
		n = 0;
		var el = valueEl.GetValueOrDefault();
		if (el.ValueKind == JsonValueKind.Undefined || el.ValueKind == JsonValueKind.Null)
			return false;
		if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out n))
			return true;
		if (el.ValueKind == JsonValueKind.String &&
		    double.TryParse(el.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out n))
			return true;
		return false;
	}

	static bool TryGetDateTimeOffset(JsonElement? valueEl, out DateTimeOffset dto)
	{
		dto = default;
		var el = valueEl.GetValueOrDefault();
		if (el.ValueKind == JsonValueKind.Undefined || el.ValueKind == JsonValueKind.Null)
			return false;
		if (el.ValueKind == JsonValueKind.String)
		{
			var s = el.GetString();
			if (string.IsNullOrWhiteSpace(s))
				return false;
			if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dto))
				return true;
			if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
			{
				dto = new DateTimeOffset(DateTime.SpecifyKind(dt.ToUniversalTime(), DateTimeKind.Utc));
				return true;
			}

			return false;
		}

		if (el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var unixMs))
		{
			dto = DateTimeOffset.FromUnixTimeMilliseconds(unixMs);
			return true;
		}

		return false;
	}
}
