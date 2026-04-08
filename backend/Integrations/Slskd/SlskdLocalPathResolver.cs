using System.Linq;

namespace TubeArr.Backend.Integrations.Slskd;

public static class SlskdLocalPathResolver
{
	/// <summary>Map Soulseek virtual filename to a path under slskd&apos;s downloads directory.</summary>
	public static string ToRelativeUnderDownloads(string soulseekFilename)
	{
		var s = (soulseekFilename ?? "").Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar).Trim();
		while (s.StartsWith('@'))
			s = s[1..].TrimStart(Path.DirectorySeparatorChar);

		var parts = s.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length <= 1)
			return parts.Length == 1 ? parts[0] : s;
		// drop first segment (peer share root / username)
		return Path.Combine(parts.Skip(1).ToArray());
	}

	public static string? TryResolveCompletedPath(string? localDownloadsRoot, string soulseekFilename, string? transferReportedFilename)
	{
		foreach (var candidate in new[] { transferReportedFilename, soulseekFilename })
		{
			if (string.IsNullOrWhiteSpace(candidate))
				continue;
			try
			{
				if (Path.IsPathRooted(candidate) && File.Exists(candidate))
					return Path.GetFullPath(candidate);
			}
			catch
			{
				/* ignore */
			}
		}

		if (string.IsNullOrWhiteSpace(localDownloadsRoot))
			return null;

		try
		{
			var rel = ToRelativeUnderDownloads(soulseekFilename);
			var combined = Path.GetFullPath(Path.Combine(localDownloadsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), rel));
			if (File.Exists(combined))
				return combined;
			var nameOnly = Path.GetFileName(soulseekFilename.Replace('\\', Path.DirectorySeparatorChar));
			if (!string.IsNullOrEmpty(nameOnly))
			{
				var flat = Path.GetFullPath(Path.Combine(localDownloadsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), nameOnly));
				if (File.Exists(flat))
					return flat;
			}
		}
		catch
		{
			/* ignore */
		}

		return null;
	}
}
