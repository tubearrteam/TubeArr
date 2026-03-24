using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class ApplicationUpdateChecker
{
	public static async Task<(bool Success, string Message)> CheckAsync(
		TubeArrDbContext db,
		IConfiguration configuration,
		IHttpClientFactory httpClientFactory,
		CancellationToken ct = default)
	{
		var serverSettings = await ProgramStartupHelpers.GetOrCreateServerSettingsAsync(db);
		var mechanism = (serverSettings.UpdateMechanism ?? "").Trim();
		if (mechanism.Contains("docker", StringComparison.OrdinalIgnoreCase))
		{
			return (true, "Docker update mechanism: update the container image outside TubeArr.");
		}

		var url = (configuration["TubeArr:UpdateCheckUrl"] ?? "").Trim();
		if (string.IsNullOrWhiteSpace(url))
		{
			return (true, "No update check URL configured. Set TubeArr:UpdateCheckUrl (e.g. GitHub releases API JSON) to enable.");
		}

		var current = GetCurrentVersionLabel();

		try
		{
			var client = url.Contains("api.github.com", StringComparison.OrdinalIgnoreCase)
				? httpClientFactory.CreateClient("GitHub")
				: httpClientFactory.CreateClient();
			if (!url.Contains("api.github.com", StringComparison.OrdinalIgnoreCase))
				client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("TubeArr", "1.0"));

			using var response = await client.GetAsync(url, ct);
			if (!response.IsSuccessStatusCode)
				return (false, $"Update check failed: HTTP {(int)response.StatusCode}.");

			var json = await response.Content.ReadAsStringAsync(ct);
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			string? remote = null;
			if (root.TryGetProperty("tag_name", out var tagEl) && tagEl.ValueKind == JsonValueKind.String)
				remote = tagEl.GetString();
			else if (root.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
				remote = nameEl.GetString();

			if (string.IsNullOrWhiteSpace(remote))
				return (true, $"Current: {current}. Update response had no recognizable version field (tag_name/name).");

			return (true, $"Current: {current}. Remote: {remote}.");
		}
		catch (Exception ex)
		{
			return (false, "Update check failed: " + (ex.Message ?? "Unknown error"));
		}
	}

	static string GetCurrentVersionLabel()
	{
		var asm = Assembly.GetExecutingAssembly();
		var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
		if (!string.IsNullOrWhiteSpace(info))
			return info!;
		var v = asm.GetName().Version;
		return v is null ? "unknown" : v.ToString();
	}
}
