using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TubeArr.Backend.Integrations.Slskd;

/// <summary>Low-level HTTP calls to slskd <c>/api/v0</c> with <c>X-API-Key</c>.</summary>
public sealed class SlskdHttpClient
{
	readonly IHttpClientFactory _httpClientFactory;

	public SlskdHttpClient(IHttpClientFactory httpClientFactory)
	{
		_httpClientFactory = httpClientFactory;
	}

	public HttpClient CreateClient(string baseUrl, string apiKey)
	{
		var client = _httpClientFactory.CreateClient(nameof(SlskdHttpClient));
		client.Timeout = TimeSpan.FromMinutes(10);
		var root = (baseUrl ?? "").Trim().TrimEnd('/');
		if (!string.IsNullOrEmpty(root))
			client.BaseAddress = new Uri(root + "/", UriKind.Absolute);
		client.DefaultRequestHeaders.Remove("X-API-Key");
		if (!string.IsNullOrWhiteSpace(apiKey))
			client.DefaultRequestHeaders.Add("X-API-Key", apiKey.Trim());
		client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		return client;
	}

	public static async Task<(bool Ok, int StatusCode, string? Body, string? Error)> GetAsync(
		HttpClient client, string relativeUri, CancellationToken ct)
	{
		try
		{
			using var resp = await client.GetAsync(relativeUri, ct);
			var body = await resp.Content.ReadAsStringAsync(ct);
			return (resp.IsSuccessStatusCode, (int)resp.StatusCode, body, resp.IsSuccessStatusCode ? null : body);
		}
		catch (Exception ex)
		{
			return (false, 0, null, ex.Message);
		}
	}

	public static async Task<(bool Ok, int StatusCode, string? Body, string? Error)> PostJsonAsync(
		HttpClient client, string relativeUri, object body, CancellationToken ct)
	{
		try
		{
			var json = JsonSerializer.Serialize(body, CamelCaseJson.Options);
			using var content = new StringContent(json, Encoding.UTF8, "application/json");
			using var resp = await client.PostAsync(relativeUri, content, ct);
			var text = await resp.Content.ReadAsStringAsync(ct);
			return (resp.IsSuccessStatusCode, (int)resp.StatusCode, text, resp.IsSuccessStatusCode ? null : text);
		}
		catch (Exception ex)
		{
			return (false, 0, null, ex.Message);
		}
	}

	public static async Task<(bool Ok, int StatusCode, string? Body, string? Error)> DeleteAsync(
		HttpClient client, string relativeUri, CancellationToken ct)
	{
		try
		{
			using var resp = await client.DeleteAsync(relativeUri, ct);
			var text = await resp.Content.ReadAsStringAsync(ct);
			return (resp.IsSuccessStatusCode, (int)resp.StatusCode, text, resp.IsSuccessStatusCode ? null : text);
		}
		catch (Exception ex)
		{
			return (false, 0, null, ex.Message);
		}
	}
}

file static class CamelCaseJson
{
	internal static readonly JsonSerializerOptions Options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};
}
