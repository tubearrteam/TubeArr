using System.Reflection;

namespace TubeArr.Backend;

/// <summary>Resolved app version for UI, API, and update checks (matches SDK / informational version rules).</summary>
public static class ApplicationVersion
{
	public static string GetDisplayVersion()
	{
		var asm = Assembly.GetExecutingAssembly();
		var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
		if (!string.IsNullOrWhiteSpace(info))
		{
			var plus = info.IndexOf('+', StringComparison.Ordinal);
			return plus > 0 ? info[..plus] : info;
		}

		var v = asm.GetName().Version;
		if (v is null || (v.Major == 0 && v.Minor == 0 && v.Build == 0 && v.Revision == 0))
			return "0.0.0-dev";

		return v.ToString();
	}
}
