using Microsoft.Extensions.Configuration;

namespace TubeArr.Backend;

/// <summary>Reads boolean feature switches from configuration (<c>TubeArr:Features:*</c>).</summary>
public static class TubeArrFeatureFlagsReader
{
	public const string ConfigurationSection = "TubeArr:Features";

	public static IReadOnlyDictionary<string, bool> Read(IConfiguration configuration)
	{
		var section = configuration.GetSection("TubeArr:Features");
		var dict = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
		foreach (var child in section.GetChildren())
		{
			var key = (child.Key ?? "").Trim();
			if (key.Length == 0)
				continue;
			if (bool.TryParse(child.Value, out var b))
				dict[key] = b;
		}

		return dict;
	}
}
