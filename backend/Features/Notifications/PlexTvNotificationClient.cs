using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Xml.Linq;

namespace TubeArr.Backend;

internal static class PlexTvNotificationClient
{
	internal const string PlexClientIdentifier = "tubearr-notifications-plex-v1";
	internal const string PlexProductName = "TubeArr";

	/// <summary>
	/// With <c>strong=true</c>, Plex returns a long code; users must open this URL (not plex.tv/link).
	/// </summary>
	public static string BuildAppAuthLink(string pinCode)
	{
		if (string.IsNullOrWhiteSpace(pinCode))
			return "https://app.plex.tv/auth";
		var qs =
			"clientID=" + Uri.EscapeDataString(PlexClientIdentifier) +
			"&code=" + Uri.EscapeDataString(pinCode.Trim()) +
			"&context%5Bdevice%5D%5Bproduct%5D=" + Uri.EscapeDataString(PlexProductName);
		return "https://app.plex.tv/auth#?" + qs;
	}

	public static async Task<(int PinId, string Code)> StartPinAsync(HttpClient http, CancellationToken ct)
	{
		using var req = new HttpRequestMessage(
			HttpMethod.Post,
			"https://plex.tv/api/v2/pins?strong=true");
		AddPlexHeaders(req);
		req.Headers.TryAddWithoutValidation("Accept", "application/json");
		using var res = await http.SendAsync(req, ct);
		res.EnsureSuccessStatusCode();
		await using var stream = await res.Content.ReadAsStreamAsync(ct);
		using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
		var root = doc.RootElement;
		var id = root.GetProperty("id").GetInt32();
		var code = root.GetProperty("code").GetString() ?? "";
		return (id, code);
	}

	public static async Task<string?> TryGetPinAuthTokenAsync(HttpClient http, int pinId, string? pinCode, CancellationToken ct)
	{
		var url = "https://plex.tv/api/v2/pins/" + pinId.ToString(CultureInfo.InvariantCulture);
		if (!string.IsNullOrWhiteSpace(pinCode))
			url += "?code=" + Uri.EscapeDataString(pinCode.Trim());
		using var req = new HttpRequestMessage(HttpMethod.Get, url);
		AddPlexHeaders(req);
		req.Headers.TryAddWithoutValidation("Accept", "application/json");
		using var res = await http.SendAsync(req, ct);
		res.EnsureSuccessStatusCode();
		await using var stream = await res.Content.ReadAsStreamAsync(ct);
		using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
		if (!doc.RootElement.TryGetProperty("authToken", out var tokEl))
			return null;
		if (tokEl.ValueKind == JsonValueKind.Null)
			return null;
		var s = tokEl.GetString();
		return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
	}

	public static async Task<string> GetResourcesXmlAsync(HttpClient http, string authToken, CancellationToken ct)
	{
		using var req = new HttpRequestMessage(
			HttpMethod.Get,
			"https://plex.tv/api/resources?includeHttps=1&includeRelay=1");
		req.Headers.TryAddWithoutValidation("X-Plex-Token", authToken);
		req.Headers.TryAddWithoutValidation("Accept", "application/xml");
		using var res = await http.SendAsync(req, ct);
		res.EnsureSuccessStatusCode();
		return await res.Content.ReadAsStringAsync(ct);
	}

	public static List<PlexServerOption> ParseServerOptions(string resourcesXml)
	{
		var list = new List<PlexServerOption>();
		var doc = XDocument.Parse(resourcesXml);
		foreach (var device in doc.Descendants().Where(e => e.Name.LocalName == "Device"))
		{
			var provides = (string?)device.Attribute("provides") ?? "";
			if (!provides.Contains("server", StringComparison.OrdinalIgnoreCase))
				continue;
			var name = (string?)device.Attribute("name") ?? "Plex Server";
			var clientId = (string?)device.Attribute("clientIdentifier") ?? "";
			if (string.IsNullOrWhiteSpace(clientId))
				continue;

			XElement? bestConn = null;
			foreach (var c in device.Elements().Where(e => e.Name.LocalName == "Connection"))
			{
				var local = (string?)c.Attribute("local");
				if (string.Equals(local, "1", StringComparison.OrdinalIgnoreCase))
				{
					bestConn = c;
					break;
				}
			}

			bestConn ??= device.Elements().FirstOrDefault(e => e.Name.LocalName == "Connection");
			if (bestConn is null)
				continue;

			var uriStr = (string?)bestConn.Attribute("uri");
			if (string.IsNullOrWhiteSpace(uriStr) || !Uri.TryCreate(uriStr, UriKind.Absolute, out var uri))
				continue;

			var useSsl = string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase);
			var host = uri.Host;
			var port = uri.IsDefaultPort ? (useSsl ? 443 : 80) : uri.Port;

			list.Add(new PlexServerOption(name, clientId, host, port, useSsl));
		}

		return list;
	}

	public static async Task<(bool Ok, string Message)> TestConnectionAsync(
		HttpClient http,
		string authToken,
		string host,
		int port,
		bool useSsl,
		bool sendNotify,
		CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(authToken))
			return (false, "Auth token is required.");
		if (string.IsNullOrWhiteSpace(host))
			return (false, "Host is required (pick a server after signing in).");

		NormalizePlexDirectScheme(host, ref useSsl);
		var scheme = useSsl ? "https" : "http";
		var baseUri = $"{scheme}://{host}:{port.ToString(CultureInfo.InvariantCulture)}";

		using (var ping = new HttpRequestMessage(HttpMethod.Get, baseUri + "/"))
		{
			AddPlexServerAuthHeaders(ping, authToken);
			using var pingRes = await http.SendAsync(ping, ct);
			if (!pingRes.IsSuccessStatusCode)
				return (false, $"Could not reach Plex server ({(int)pingRes.StatusCode}).");
		}

		var sectionsUrl = baseUri + "/library/sections";
		using var secReq = new HttpRequestMessage(HttpMethod.Get, sectionsUrl);
		AddPlexServerAuthHeaders(secReq, authToken);
		using var secRes = await http.SendAsync(secReq, ct);
		var secXml = await secRes.Content.ReadAsStringAsync(ct);
		if (!secRes.IsSuccessStatusCode)
			return (false, "Could not read Plex library sections.");

		var tvKeys = FindTvShowSectionKeys(secXml);
		if (tvKeys.Count == 0)
			return (false, "At least one TV library is required on the Plex server.");

		if (sendNotify)
		{
			var notifyUrl = baseUri + "/:/notify?" + WebUtility.UrlEncode("title") + "=TubeArr&" +
			                WebUtility.UrlEncode("subtitle") + "=" + WebUtility.UrlEncode("Test notification");
			using var nReq = new HttpRequestMessage(HttpMethod.Get, notifyUrl);
			AddPlexServerAuthHeaders(nReq, authToken);
			using var nRes = await http.SendAsync(nReq, ct);
			if (!nRes.IsSuccessStatusCode)
				return (false, $"Plex notify failed ({(int)nRes.StatusCode}).");
		}

		return (true, "OK");
	}

	public static async Task<(bool Ok, string Message)> RefreshTvShowLibrariesAsync(
		HttpClient http,
		string authToken,
		string host,
		int port,
		bool useSsl,
		CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(authToken) || string.IsNullOrWhiteSpace(host))
			return (false, "Plex connection is not configured.");

		NormalizePlexDirectScheme(host, ref useSsl);
		var scheme = useSsl ? "https" : "http";
		var baseUri = $"{scheme}://{host}:{port.ToString(CultureInfo.InvariantCulture)}";
		var sectionsUrl = baseUri + "/library/sections";
		using var secReq = new HttpRequestMessage(HttpMethod.Get, sectionsUrl);
		AddPlexServerAuthHeaders(secReq, authToken);
		using var secRes = await http.SendAsync(secReq, ct);
		var secXml = await secRes.Content.ReadAsStringAsync(ct);
		if (!secRes.IsSuccessStatusCode)
			return (false, "Could not read Plex library sections.");

		var tvKeys = FindTvShowSectionKeys(secXml);
		foreach (var key in tvKeys)
		{
			var refreshUrl = $"{baseUri}/library/sections/{WebUtility.UrlEncode(key)}/refresh";
			using var rReq = new HttpRequestMessage(HttpMethod.Get, refreshUrl);
			AddPlexServerAuthHeaders(rReq, authToken);
			using var rRes = await http.SendAsync(rReq, ct);
			if (!rRes.IsSuccessStatusCode)
				return (false, $"Library refresh failed for section {key} ({(int)rRes.StatusCode}).");
		}

		return (true, "OK");
	}

	static List<string> FindTvShowSectionKeys(string sectionsXml)
	{
		var keys = new List<string>();
		var doc = XDocument.Parse(sectionsXml);
		foreach (var dir in doc.Descendants().Where(e => e.Name.LocalName == "Directory"))
		{
			var type = (string?)dir.Attribute("type");
			if (!string.Equals(type, "show", StringComparison.OrdinalIgnoreCase))
				continue;
			var key = (string?)dir.Attribute("key");
			if (!string.IsNullOrWhiteSpace(key))
				keys.Add(key.Trim());
		}

		return keys;
	}

	static void AddPlexHeaders(HttpRequestMessage req)
	{
		req.Headers.TryAddWithoutValidation("X-Plex-Client-Identifier", PlexClientIdentifier);
		req.Headers.TryAddWithoutValidation("X-Plex-Product", PlexProductName);
		req.Headers.TryAddWithoutValidation("X-Plex-Version", "1.0.0");
	}

	/// <summary>
	/// Plex <c>*.plex.direct</c> hostnames only speak HTTPS on the media port; HTTP fails or never connects.
	/// </summary>
	static void NormalizePlexDirectScheme(string host, ref bool useSsl)
	{
		if (host.Contains(".plex.direct", StringComparison.OrdinalIgnoreCase))
			useSsl = true;
	}

	static void AddPlexServerAuthHeaders(HttpRequestMessage req, string authToken)
	{
		req.Headers.TryAddWithoutValidation("X-Plex-Token", authToken);
		AddPlexHeaders(req);
		req.Headers.TryAddWithoutValidation("Accept", "application/xml");
	}

	internal readonly record struct PlexServerOption(string DisplayName, string ClientIdentifier, string Host, int Port, bool UseSsl);
}
