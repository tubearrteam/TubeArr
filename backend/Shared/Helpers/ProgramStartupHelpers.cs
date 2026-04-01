using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class ProgramStartupHelpers
{
	static readonly JsonSerializerOptions LocalizationJsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true
	};

	public static (string? BindAddress, int? Port, string? UrlBase, bool? EnableSsl, int? SslPort, string? SslCertPath, string? SslCertPassword)? TryLoadServerSettingsPreload(string connectionString)
	{
		try
		{
			using var connection = new SqliteConnection(connectionString);
			connection.Open();

			using var tableCheck = connection.CreateCommand();
			tableCheck.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name='ServerSettings' LIMIT 1;";
			var hasTable = tableCheck.ExecuteScalar() is not null;
			if (!hasTable)
			{
				return null;
			}

			using var cmd = connection.CreateCommand();
			cmd.CommandText = "SELECT BindAddress, Port, UrlBase, EnableSsl, SslPort, SslCertPath, SslCertPassword FROM ServerSettings WHERE Id = 1 LIMIT 1;";
			using var reader = cmd.ExecuteReader();
			if (!reader.Read())
			{
				return null;
			}

			var bindAddress = reader.IsDBNull(0) ? null : reader.GetString(0);
			var port = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
			var urlBase = reader.IsDBNull(2) ? null : reader.GetString(2);
			var enableSsl = reader.IsDBNull(3) ? (bool?)null : reader.GetInt32(3) != 0;
			var sslPort = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4);
			var sslCertPath = reader.IsDBNull(5) ? null : reader.GetString(5);
			var sslCertPassword = reader.IsDBNull(6) ? null : reader.GetString(6);

			return (bindAddress, port, urlBase, enableSsl, sslPort, sslCertPath, sslCertPassword);
		}
		catch
		{
			return null;
		}
	}

	public static string NormalizeUrlBase(string? urlBase)
	{
		var value = (urlBase ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(value) || value == "/")
		{
			return string.Empty;
		}

		value = value.TrimEnd('/');
		if (!value.StartsWith('/'))
		{
			value = "/" + value;
		}

		return value;
	}

	public static IPAddress? TryGetListenAddress(string bindAddress)
	{
		var raw = (bindAddress ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(raw) || raw == "*" || raw == "0.0.0.0" || raw == "::")
		{
			return null;
		}

		if (raw.Equals("localhost", StringComparison.OrdinalIgnoreCase))
		{
			return IPAddress.Loopback;
		}

		raw = raw.Trim('[', ']');
		return IPAddress.TryParse(raw, out var ip) ? ip : null;
	}

	public static IReadOnlyDictionary<string, string> LoadEnglishStrings(string backendContentRoot)
	{
		// Canonical backend dictionary location.
		var canonicalPath = Path.GetFullPath(Path.Combine(backendContentRoot, "Shared", "Localization", "en.json"));
		if (File.Exists(canonicalPath))
		{
			var canonicalJson = File.ReadAllText(canonicalPath);
			return JsonSerializer.Deserialize<Dictionary<string, string>>(canonicalJson) ?? new Dictionary<string, string>();
		}

		// Backward-compatible fallback: walk up from ContentRootPath (e.g. backend/ or
		// backend/bin/Debug/net8.0) and check the old root-level location.
		var dir = Path.GetFullPath(backendContentRoot);
		while (!string.IsNullOrEmpty(dir))
		{
			var translationPath = Path.Combine(dir, "en.json");
			if (File.Exists(translationPath))
			{
				var json = File.ReadAllText(translationPath);
				return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
			}

			var parent = Path.GetDirectoryName(dir);
			if (parent == dir) break;
			dir = parent;
		}

		return new Dictionary<string, string>();
	}

	/// <summary>Loads a single dictionary from <c>Shared/Localization/{fileName}</c> (empty if missing).</summary>
	public static IReadOnlyDictionary<string, string> LoadLocalizationDictionary(string backendContentRoot, string fileName)
	{
		if (string.IsNullOrWhiteSpace(fileName))
			return new Dictionary<string, string>();

		var safe = Path.GetFileName(fileName.Trim());
		if (string.IsNullOrEmpty(safe) || safe.Contains("..", StringComparison.Ordinal))
			return new Dictionary<string, string>();

		var path = Path.GetFullPath(Path.Combine(backendContentRoot, "Shared", "Localization", safe));
		if (!File.Exists(path))
			return new Dictionary<string, string>();

		try
		{
			var json = File.ReadAllText(path);
			return JsonSerializer.Deserialize<Dictionary<string, string>>(json, LocalizationJsonOptions)
			       ?? new Dictionary<string, string>();
		}
		catch
		{
			return new Dictionary<string, string>();
		}
	}

	/// <summary>English base plus overlay from the UI language’s dictionary (missing keys stay English).</summary>
	public static IReadOnlyDictionary<string, string> BuildMergedUiStrings(string backendContentRoot, int uiLanguageId)
	{
		var english = LoadEnglishStrings(backendContentRoot);
		if (english.Count == 0)
			english = new Dictionary<string, string>();

		if (uiLanguageId == 0)
			return english;

		var languages = LoadAvailableLanguages(backendContentRoot);
		var lang = languages.FirstOrDefault(l => l.Id == uiLanguageId && l.Enabled);
		if (lang is null)
			return english;

		if (string.Equals(lang.DictionaryFile, "en.json", StringComparison.OrdinalIgnoreCase))
			return english;

		var overlay = LoadLocalizationDictionary(backendContentRoot, lang.DictionaryFile);
		if (overlay.Count == 0)
			return english;

		var merged = new Dictionary<string, string>(english, StringComparer.Ordinal);
		foreach (var kv in overlay)
		{
			if (string.IsNullOrWhiteSpace(kv.Value))
				continue;
			merged[kv.Key] = kv.Value;
		}

		return merged;
	}

	public static IReadOnlyList<LanguageOption> LoadAvailableLanguages(string backendContentRoot)
	{
		var canonicalPath = Path.GetFullPath(Path.Combine(backendContentRoot, "Shared", "Localization", "languages.json"));
		if (File.Exists(canonicalPath))
		{
			var json = File.ReadAllText(canonicalPath);
			var parsed = JsonSerializer.Deserialize<List<LanguageOption>>(json, LocalizationJsonOptions);
			if (parsed is { Count: > 0 })
			{
				return parsed;
			}
		}

		return
		[
			new LanguageOption(0, "English", "en", "en.json", true)
		];
	}

	public static string GenerateApiKey()
	{
		return Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
	}

	public static async Task<ServerSettingsEntity> GetOrCreateServerSettingsAsync(TubeArrDbContext db)
	{
		var settings = await db.ServerSettings.FirstOrDefaultAsync(x => x.Id == 1);
		if (settings is null)
		{
			settings = new ServerSettingsEntity { Id = 1 };
			db.ServerSettings.Add(settings);
		}

		if (string.IsNullOrWhiteSpace(settings.ApiKey))
		{
			settings.ApiKey = GenerateApiKey();
		}

		if (string.IsNullOrWhiteSpace(settings.InstanceName) ||
			string.Equals(settings.InstanceName, "Development", StringComparison.OrdinalIgnoreCase))
		{
			settings.InstanceName = "TubeArr";
		}

		await db.SaveChangesAsync();
		return settings;
	}
}

internal sealed record LanguageOption(
	int Id,
	string Name,
	string Code,
	string DictionaryFile,
	bool Enabled);

sealed class UrlBaseStartupFilter : IStartupFilter
{
	private readonly PathString _urlBase;

	public UrlBaseStartupFilter(string urlBase)
	{
		_urlBase = new PathString(urlBase);
	}

	public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
	{
		if (!_urlBase.HasValue)
		{
			return next;
		}

		return app =>
		{
			app.Use(async (context, nxt) =>
			{
				if (context.Request.Path.StartsWithSegments(_urlBase, out var remaining))
				{
					context.Request.PathBase = context.Request.PathBase.Add(_urlBase);
					context.Request.Path = remaining;
				}

				await nxt();
			});

			next(app);
		};
	}
}

