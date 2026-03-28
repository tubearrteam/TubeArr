using System.Text.Json;

namespace TubeArr.Backend;

/// <summary>Fetches remote application releases (GitHub Releases API) for System → Updates.</summary>
internal static class RemoteUpdateCatalog
{
	internal const string DefaultGitHubReleasesUrl = "https://api.github.com/repos/tubearrteam/TubeArr/releases?per_page=20";

	public static async Task<IReadOnlyList<UpdateItemDto>> FetchAsync(
		IHttpClientFactory httpClientFactory,
		IConfiguration configuration,
		CancellationToken cancellationToken)
	{
		var url = (configuration["TubeArr:UpdateCheckUrl"] ?? "").Trim();
		if (string.IsNullOrEmpty(url))
			url = DefaultGitHubReleasesUrl;

		var client = url.Contains("api.github.com", StringComparison.OrdinalIgnoreCase)
			? httpClientFactory.CreateClient("GitHub")
			: httpClientFactory.CreateClient();

		using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
		if (!response.IsSuccessStatusCode)
			return Array.Empty<UpdateItemDto>();

		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
		if (doc.RootElement.ValueKind != JsonValueKind.Array)
			return Array.Empty<UpdateItemDto>();

		var current = ApplicationVersion.GetDisplayVersion();
		var currentNorm = NormalizeCurrentLabel(current);
		var rows = new List<(Version Ver, string Normalized, JsonElement El)>();
		foreach (var el in doc.RootElement.EnumerateArray())
		{
			if (!el.TryGetProperty("tag_name", out var tagProp))
				continue;
			var tag = tagProp.GetString();
			if (!TryParseReleaseVersion(tag, out var normalized, out var ver))
				continue;
			rows.Add((ver, normalized, el));
		}

		rows.Sort(static (a, b) => b.Ver.CompareTo(a.Ver));

		var items = new List<UpdateItemDto>(rows.Count);
		for (var i = 0; i < rows.Count; i++)
		{
			var (_, normalized, el) = rows[i];
			DateTimeOffset published = DateTimeOffset.MinValue;
			if (el.TryGetProperty("published_at", out var p) && DateTimeOffset.TryParse(p.GetString(), out var dto))
				published = dto;

			var body = el.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
			var htmlUrl = el.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "";

			var installed = currentNorm is not null
				&& string.Equals(normalized, currentNorm, StringComparison.OrdinalIgnoreCase);

			items.Add(new UpdateItemDto(
				Id: i + 1,
				Version: normalized,
				Branch: "main",
				ReleaseDate: published.ToString("O"),
				FileName: "",
				Url: htmlUrl,
				Installed: installed,
				InstalledOn: "",
				Installable: false,
				Latest: i == 0,
				Changes: string.IsNullOrWhiteSpace(body)
					? null
					: new UpdateChangesDto(new[] { body.Trim() }, Array.Empty<string>()),
				Hash: ""));
		}

		return items;
	}

	internal static bool TryParseReleaseVersion(string? tag, out string normalized, out Version version)
	{
		normalized = "";
		version = new Version(0, 0);
		if (string.IsNullOrWhiteSpace(tag))
			return false;

		var s = tag.Trim();
		if (s.StartsWith('v') || s.StartsWith('V'))
			s = s[1..];

		// Trim prerelease suffix for System.Version (e.g. 1.2.3-beta1 → 1.2.3)
		var dash = s.IndexOf('-', StringComparison.Ordinal);
		if (dash > 0)
			s = s[..dash];

		if (!Version.TryParse(s, out var parsed))
			return false;

		version = parsed;
		normalized = $"{parsed.Major}.{parsed.Minor}.{parsed.Build}";
		return true;
	}

	static string? NormalizeCurrentLabel(string? current)
	{
		if (!TryParseReleaseVersion(current, out var norm, out _))
			return null;
		return norm;
	}
}

internal sealed record UpdateChangesDto(
	[property: System.Text.Json.Serialization.JsonPropertyName("new")] string[] New,
	[property: System.Text.Json.Serialization.JsonPropertyName("fixed")] string[] Fixed);

internal sealed record UpdateItemDto(
	int Id,
	string Version,
	string Branch,
	string ReleaseDate,
	string FileName,
	string Url,
	bool Installed,
	string InstalledOn,
	bool Installable,
	bool Latest,
	UpdateChangesDto? Changes,
	string Hash);
