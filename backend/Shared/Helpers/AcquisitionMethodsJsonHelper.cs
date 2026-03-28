using System.Linq;
using System.Text.Json;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

public static class AcquisitionMethodsJsonHelper
{
	static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	public static string DefaultCommandJson => "[]";

	public static string DefaultDownloadJson =>
		JsonSerializer.Serialize(new[] { AcquisitionMethodIds.YtDlp }, JsonOptions);

	public static List<string> Parse(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
			return new List<string>();

		try
		{
			var arr = JsonSerializer.Deserialize<string[]>(json, JsonOptions);
			return arr is { Length: > 0 }
				? arr.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
				: new List<string>();
		}
		catch
		{
			return new List<string>();
		}
	}

	public static string Serialize(IReadOnlyList<string> methods)
	{
		var ordered = methods.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
		return JsonSerializer.Serialize(ordered, JsonOptions);
	}

	public static string MergeOne(string? existingJson, string methodId)
	{
		var list = Parse(existingJson);
		if (!list.Contains(methodId, StringComparer.OrdinalIgnoreCase))
			list.Add(methodId);
		return Serialize(list);
	}

	/// <summary>UI and history should always show at least one source; phases that only touched local DB/logic have no merged id unless we default.</summary>
	public static string[] ToArrayOrInternalDefault(IReadOnlyList<string> methods)
	{
		if (methods is { Count: > 0 })
			return methods.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
		return new[] { AcquisitionMethodIds.Internal };
	}
}
