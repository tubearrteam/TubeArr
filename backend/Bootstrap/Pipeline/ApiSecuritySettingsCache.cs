using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal readonly struct ApiSecuritySnapshot(bool apiKeyEnforced, byte[]? expectedKeySha256)
{
	public bool ApiKeyEnforced => apiKeyEnforced;
	/// <summary>SHA256 of UTF-8 API key; null when the key is missing (reject all).</summary>
	public byte[]? ExpectedKeySha256 => expectedKeySha256;
}

/// <summary>
/// Caches auth mode and API key hash for API middleware. Invalidated when settings change; also refreshes after <see cref="MaxStaleSeconds"/>.
/// </summary>
internal sealed class ApiSecuritySettingsCache
{
	internal const int MaxStaleSeconds = 60;

	readonly object _lock = new();
	bool _loaded;
	DateTime _loadedUtc;
	bool _enforced;
	byte[]? _expectedKeySha256;

	public void Invalidate()
	{
		lock (_lock)
			_loaded = false;
	}

	public async ValueTask<ApiSecuritySnapshot> GetAsync(TubeArrDbContext db, CancellationToken ct)
	{
		lock (_lock)
		{
			if (_loaded && (DateTime.UtcNow - _loadedUtc).TotalSeconds < MaxStaleSeconds)
				return new ApiSecuritySnapshot(_enforced, _expectedKeySha256);
		}

		var row = await db.ServerSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1, ct) ?? new ServerSettingsEntity();
		var enforced = IsApiKeyAuthEnforced(row);
		var key = row.ApiKey ?? "";
		byte[]? hash = null;
		if (!string.IsNullOrWhiteSpace(key))
			hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));

		lock (_lock)
		{
			_enforced = enforced;
			_expectedKeySha256 = hash;
			_loaded = true;
			_loadedUtc = DateTime.UtcNow;
			return new ApiSecuritySnapshot(_enforced, _expectedKeySha256);
		}
	}

	internal static bool FixedTimeApiKeyEquals(byte[] expectedSha256, string? provided)
	{
		var hp = SHA256.HashData(Encoding.UTF8.GetBytes(provided ?? ""));
		return CryptographicOperations.FixedTimeEquals(expectedSha256, hp);
	}

	internal static bool IsApiKeyAuthEnforced(ServerSettingsEntity settings)
	{
		var authRequired = settings.AuthenticationRequired?.Trim() ?? "enabled";
		var authMethod = settings.AuthenticationMethod?.Trim() ?? "none";
		return !authRequired.Equals("disabled", StringComparison.OrdinalIgnoreCase)
			&& authMethod.Equals("apikey", StringComparison.OrdinalIgnoreCase);
	}
}
