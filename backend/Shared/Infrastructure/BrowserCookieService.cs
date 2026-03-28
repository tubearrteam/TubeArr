using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace TubeArr.Shared.Infrastructure;

/// <summary>
/// Service for extracting cookies from browser profiles without external scripts.
/// Handles closing/reopening browser and decrypting Chrome cookies.
/// </summary>
public interface IBrowserCookieService
{
    Task<BrowserCookieExportResult> ExportBrowserCookiesAsync(string browser, string outputPath, bool reopenBrowser = true);
}

public class BrowserCookieService : IBrowserCookieService
{
    /// <summary>UTF-8 without BOM — yt-dlp treats a BOM as part of the first line and rejects the cookie file.</summary>
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// One export at a time for the whole process. Concurrent runs shared the same browser profile,
    /// output file, and kill/reopen steps — interleaving caused races (double kill, copy while browser
    /// restarted, torn writes to cookies.txt).
    /// </summary>
    private static readonly SemaphoreSlim ExportGate = new(1, 1);

    private static readonly Dictionary<string, BrowserInfo> Browsers = new()
    {
        { "chrome", new BrowserInfo { ProcessName = "chrome", FolderName = "Google/Chrome" } },
        { "edge", new BrowserInfo { ProcessName = "msedge", FolderName = "Microsoft/Edge" } },
        { "chromium", new BrowserInfo { ProcessName = "chromium", FolderName = "Chromium" } }
    };

    private readonly ILogger<BrowserCookieService> _logger;

    public BrowserCookieService(ILogger<BrowserCookieService> logger)
    {
        _logger = logger;
    }

    public async Task<BrowserCookieExportResult> ExportBrowserCookiesAsync(string browser, string outputPath, bool reopenBrowser = true)
    {
        browser = browser.ToLowerInvariant();

        if (!Browsers.TryGetValue(browser, out var browserInfo))
        {
            return new BrowserCookieExportResult
            {
                Success = false,
                Message = $"Unsupported browser: {browser}. Supported: chrome, edge, chromium"
            };
        }

        await ExportGate.WaitAsync();
        try
        {
            try
            {
                _logger.LogInformation("Cookie export: starting for browser={Browser}, processName={Process}, outputPath={Output}",
                    browser, browserInfo.ProcessName, outputPath);

                var closedProcesses = await CloseBrowserAsync(browserInfo.ProcessName);

                if (closedProcesses == 0)
                {
                    _logger.LogWarning("Cookie export: no running processes matched {Process}", browserInfo.ProcessName);
                    return new BrowserCookieExportResult
                    {
                        Success = false,
                        Message = $"{browser} is not currently running. Please open it first and log into YouTube."
                    };
                }

                _logger.LogInformation("Cookie export: closed {Count} browser process(es); waiting for file locks to release", closedProcesses);
                await Task.Delay(2000);

                var profilePath = FindBrowserProfilePath(browserInfo.FolderName);
                if (string.IsNullOrEmpty(profilePath))
                {
                    _logger.LogWarning("Cookie export: profile directory not found for vendor folder {Folder}", browserInfo.FolderName);
                    return new BrowserCookieExportResult
                    {
                        Success = false,
                        Message = $"Could not find {browser} profile directory"
                    };
                }

                _logger.LogInformation("Cookie export: using profile directory {ProfilePath}", profilePath);

                var (cookies, cookieDbPath) = await ExtractCookiesFromProfileAsync(profilePath, browserInfo.FolderName);
                if (!cookies.Any())
                {
                    _logger.LogWarning("Cookie export: zero YouTube/Google cookies after read from {CookieDb}", cookieDbPath ?? "(no db)");
                    return new BrowserCookieExportResult
                    {
                        Success = false,
                        Message = "No cookies found in browser profile. Please log into YouTube first."
                    };
                }

                _logger.LogInformation("Cookie export: extracted {Count} cookie row(s) for export from {CookieDb}", cookies.Count, cookieDbPath);

                var exportResult = WriteCookiesToFile(cookies, outputPath);
                if (!exportResult)
                {
                    _logger.LogError("Cookie export: failed writing Netscape file to {Output}", outputPath);
                    return new BrowserCookieExportResult
                    {
                        Success = false,
                        Message = $"Failed to write cookies to {outputPath}"
                    };
                }

                try
                {
                    var length = new FileInfo(outputPath).Length;
                    _logger.LogInformation("Cookie export: wrote Netscape file {Output} ({Length} bytes)", outputPath, length);
                }
                catch
                {
                    _logger.LogInformation("Cookie export: wrote Netscape file {Output}", outputPath);
                }

                if (reopenBrowser)
                {
                    _logger.LogInformation("Cookie export: reopening browser process {Process}", browserInfo.ProcessName);
                    await ReopenBrowserAsync(browserInfo.ProcessName);
                }

                _logger.LogInformation("Cookie export: finished OK ({Count} cookies)", cookies.Count);
                return new BrowserCookieExportResult
                {
                    Success = true,
                    Message = $"Successfully exported {cookies.Count} cookies",
                    CookiesPath = outputPath,
                    CookieCount = cookies.Count
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cookie export: unhandled exception");
                return new BrowserCookieExportResult
                {
                    Success = false,
                    Message = $"Error exporting cookies: {ex.Message}"
                };
            }
        }
        finally
        {
            ExportGate.Release();
        }
    }

    private async Task<int> CloseBrowserAsync(string processName)
    {
        var processes = Process.GetProcessesByName(processName);

        if (processes.Length == 0)
            return 0;

        foreach (var process in processes)
        {
            try
            {
                process.Kill(true);
                process.WaitForExit(5000);
            }
            catch
            {
                // Continue with other processes
            }
            finally
            {
                process.Dispose();
            }
        }

        await Task.CompletedTask;
        return processes.Length;
    }

    /// <summary>
    /// Chromium defaults to LocalApplicationData (e.g. %LOCALAPPDATA%\Microsoft\Edge). Roaming is only
    /// used with policy; try both so extraction is not silently pointed at an empty tree.
    /// </summary>
    private string? FindBrowserProfilePath(string folderName)
    {
        var vendorRoot = folderName.Replace('/', Path.DirectorySeparatorChar);

        foreach (var rootBase in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                 })
        {
            if (string.IsNullOrEmpty(rootBase))
                continue;

            var appDataPath = Path.Combine(rootBase, vendorRoot);
            if (!Directory.Exists(appDataPath))
                continue;

            var defaultProfile = Path.Combine(appDataPath, "User Data", "Default");
            if (Directory.Exists(defaultProfile))
            {
                _logger.LogDebug("Cookie export: resolved profile via {Root}\\{Vendor}\\User Data\\Default", rootBase, vendorRoot);
                return defaultProfile;
            }

            var userDataPath = Path.Combine(appDataPath, "User Data");
            if (Directory.Exists(userDataPath))
            {
                var namedProfile = Directory.GetDirectories(userDataPath)
                    .Where(p => Path.GetFileName(p).StartsWith("Profile", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(namedProfile))
                {
                    _logger.LogDebug("Cookie export: resolved profile {Profile} under {UserData}", namedProfile, userDataPath);
                    return namedProfile;
                }
            }
        }

        return null;
    }

    private static bool UsesChromiumDpapiCookies(string folderName) =>
        folderName.Contains("Chrome", StringComparison.OrdinalIgnoreCase)
        || folderName.Contains("Edge", StringComparison.OrdinalIgnoreCase)
        || folderName.Contains("Chromium", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Chromium stores expires_utc as microseconds since 1601-01-01 UTC (not Unix time).
    /// </summary>
    private static DateTime ChromiumExpiresUtcToLocal(long expiresUtc)
    {
        if (expiresUtc == 0)
            return DateTime.MaxValue;

        try
        {
            var epoch = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddTicks(expiresUtc * 10).ToLocalTime();
        }
        catch
        {
            return DateTime.MaxValue;
        }
    }

    [SupportedOSPlatform("windows")]
    private static byte[]? TryLoadChromiumAesMasterKey(string userDataDirectory, ILogger logger)
    {
        try
        {
            var localStatePath = Path.Combine(userDataDirectory, "Local State");
            if (!File.Exists(localStatePath))
            {
                logger.LogDebug("Cookie export: no Local State at {Path}", localStatePath);
                return null;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(localStatePath));
            if (!doc.RootElement.TryGetProperty("os_crypt", out var osCrypt)
                || !osCrypt.TryGetProperty("encrypted_key", out var keyEl))
            {
                logger.LogDebug("Cookie export: Local State missing os_crypt.encrypted_key");
                return null;
            }

            var b64 = keyEl.GetString();
            if (string.IsNullOrEmpty(b64))
                return null;

            var decoded = Convert.FromBase64String(b64);
            if (decoded.Length > 5
                && Encoding.ASCII.GetString(decoded.AsSpan(0, 5)).Equals("DPAPI", StringComparison.Ordinal))
            {
                decoded = decoded.AsSpan(5).ToArray();
            }

            var master = ProtectedData.Unprotect(decoded, null, DataProtectionScope.CurrentUser);
            if (master is { Length: 16 or 32 })
            {
                logger.LogInformation("Cookie export: decrypted Chromium os_crypt AES key ({KeyBits} bits) for v11 cookies", master.Length * 8);
                return master;
            }

            logger.LogWarning("Cookie export: unexpected os_crypt key length {Length} after DPAPI unwrap", master.Length);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cookie export: could not load Chromium AES master key from Local State");
            return null;
        }
    }

    /// <summary>v11 blob: "v11" + 12-byte nonce + ciphertext + 16-byte GCM tag.</summary>
    private static bool TryDecryptChromeV11(ReadOnlySpan<byte> encryptedValue, ReadOnlySpan<byte> aesKey, out string plaintext)
    {
        plaintext = "";
        if (encryptedValue.Length < 3 + 12 + 1 + 16)
            return false;
        if (encryptedValue[0] != (byte)'v' || encryptedValue[1] != (byte)'1' || encryptedValue[2] != (byte)'1')
            return false;

        var nonce = encryptedValue.Slice(3, 12);
        var ciphertext = encryptedValue.Slice(15, encryptedValue.Length - 15 - 16);
        var tag = encryptedValue.Slice(encryptedValue.Length - 16, 16);
        var plain = new byte[ciphertext.Length];
        try
        {
            using var aes = new AesGcm(aesKey, 16);
            aes.Decrypt(nonce, ciphertext, tag, plain);
            plaintext = Encoding.UTF8.GetString(plain);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<(List<CookieData> Cookies, string? CookieDbPath)> ExtractCookiesFromProfileAsync(string profilePath, string folderName)
    {
        var cookies = new List<CookieData>();
        var cookiePath = Path.Combine(profilePath, "Network", "Cookies");
        if (!File.Exists(cookiePath))
            cookiePath = Path.Combine(profilePath, "Cookies");

        if (!File.Exists(cookiePath))
            return (cookies, null);

        byte[]? masterKey = null;
        var userDataDir = Path.GetDirectoryName(profilePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            && !string.IsNullOrEmpty(userDataDir))
        {
            masterKey = TryLoadChromiumAesMasterKey(userDataDir, _logger);
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"cookies_{Guid.NewGuid()}.db");

        try
        {
            File.Copy(cookiePath, tempPath, true);
            _logger.LogDebug("Cookie export: copied cookie DB snapshot {Temp}", tempPath);

            await using var connection = new SqliteConnection($"Data Source={tempPath};Mode=ReadOnly");
            await connection.OpenAsync();

            try
            {
                await ReadChromiumCookiesAsync(connection, folderName, cookies, modernSchema: true, masterKey);
            }
            catch (SqliteException ex) when (ex.Message.Contains("no such column", StringComparison.OrdinalIgnoreCase))
            {
                cookies.Clear();
                await ReadChromiumCookiesAsync(connection, folderName, cookies, modernSchema: false, masterKey);
            }
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }

        return (cookies, cookiePath);
    }

    private async Task ReadChromiumCookiesAsync(
        SqliteConnection connection,
        string folderName,
        List<CookieData> cookies,
        bool modernSchema,
        byte[]? aesMasterKey)
    {
        var sql = modernSchema
            ? """
              SELECT host_key, path, name, value, encrypted_value, expires_utc, is_secure, is_httponly
              FROM cookies
              WHERE host_key LIKE '%youtube%'
                 OR host_key LIKE '%google%'
                 OR host_key LIKE '%youtu.be%'
              """
            : """
              SELECT host_key, path, name, value, expires_utc, secure, httponly
              FROM cookies
              WHERE host_key LIKE '%youtube%'
                 OR host_key LIKE '%google%'
                 OR host_key LIKE '%youtu.be%'
              """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();

        var v11Failed = 0;
        var v10Used = 0;
        var v11Used = 0;

        while (await reader.ReadAsync())
        {
            string cookieValue;
            if (modernSchema)
            {
                byte[]? enc = null;
                if (!reader.IsDBNull(4))
                    enc = reader.GetFieldValue<byte[]>(4);

                if (enc is { Length: > 0 } && UsesChromiumDpapiCookies(folderName))
                {
                    cookieValue = DecryptChromiumCookie(enc, aesMasterKey, ref v10Used, ref v11Used, ref v11Failed);
                }
                else if (!reader.IsDBNull(3))
                    cookieValue = reader.GetString(3);
                else
                    continue;
            }
            else
            {
                if (reader.IsDBNull(3))
                    continue;
                cookieValue = reader.GetString(3);
            }

            if (string.IsNullOrEmpty(cookieValue))
                continue;

            var expiresRaw = reader.IsDBNull(modernSchema ? 5 : 4) ? 0L : reader.GetInt64(modernSchema ? 5 : 4);
            var secureOrd = modernSchema ? 6 : 5;
            var httpOrd = modernSchema ? 7 : 6;
            var secure = ReadSqliteBool(reader, secureOrd);
            var httpOnly = ReadSqliteBool(reader, httpOrd);

            cookies.Add(new CookieData
            {
                Domain = reader.GetString(0),
                Path = reader.GetString(1),
                Name = reader.GetString(2),
                Value = cookieValue,
                Expires = ChromiumExpiresUtcToLocal(expiresRaw),
                Secure = secure,
                HttpOnly = httpOnly
            });
        }

        if (v11Failed > 0)
            _logger.LogWarning("Cookie export: {Count} encrypted cookie row(s) could not be decrypted (v11/key)", v11Failed);
        _logger.LogDebug("Cookie export: decrypt stats v10={V10} v11={V11} v11Failed={Fail}", v10Used, v11Used, v11Failed);
    }

    private string DecryptChromiumCookie(byte[] encryptedData, byte[]? aesMasterKey, ref int v10Count, ref int v11Count, ref int v11Fail)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return DecryptChromiumWindowsCookie(encryptedData, aesMasterKey, ref v10Count, ref v11Count, ref v11Fail);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return DecryptChromeLinuxPassword(encryptedData);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return DecryptChromeMacPassword(encryptedData);
        return Encoding.UTF8.GetString(encryptedData);
    }

    [SupportedOSPlatform("windows")]
    private string DecryptChromiumWindowsCookie(byte[] encryptedData, byte[]? aesMasterKey, ref int v10Count, ref int v11Count, ref int v11Fail)
    {
        if (encryptedData.Length >= 3
            && encryptedData[0] == (byte)'v'
            && encryptedData[1] == (byte)'1'
            && encryptedData[2] == (byte)'1')
        {
            if (aesMasterKey is not null && TryDecryptChromeV11(encryptedData, aesMasterKey, out var plain))
            {
                v11Count++;
                return plain;
            }

            v11Fail++;
            return "";
        }

        if (encryptedData.Length >= 3
            && encryptedData[0] == (byte)'v'
            && encryptedData[1] == (byte)'1'
            && encryptedData[2] == (byte)'0')
        {
            var encrypted = encryptedData.AsSpan(3).ToArray();
            try
            {
                var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                v10Count++;
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                v11Fail++;
                return "";
            }
        }

        return Encoding.UTF8.GetString(encryptedData);
    }

    private static bool ReadSqliteBool(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return false;

        return reader.GetFieldType(ordinal) == typeof(long)
            ? reader.GetInt64(ordinal) != 0
            : reader.GetBoolean(ordinal);
    }

    private string DecryptChromeLinuxPassword(byte[] encryptedData) =>
        Encoding.UTF8.GetString(encryptedData);

    private string DecryptChromeMacPassword(byte[] encryptedData) =>
        Encoding.UTF8.GetString(encryptedData);

    /// <summary>
    /// Netscape cookie files are line-oriented TAB-separated fields. CR/LF/TAB inside a field break parsers.
    /// yt-dlp passes Cookie headers through Python requests, which encodes header values as latin-1 — codepoints
    /// above U+00FF and U+FFFD (replacement char from bad UTF-8) must not appear.
    /// </summary>
    private static string SanitizeNetscapeField(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (c is '\r' or '\n' or '\t' or '\0' or '\ufffd' or '\ufeff')
                continue;
            if (char.IsControl(c))
                continue;
            if (c > '\u00ff')
                continue;
            sb.Append(c);
        }

        return sb.ToString();
    }

    private bool WriteCookiesToFile(List<CookieData> cookies, string outputPath)
    {
        try
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var writer = new StreamWriter(outputPath, false, Utf8NoBom))
            {
                writer.WriteLine("# Netscape HTTP Cookie File");
                writer.WriteLine("# This is a generated file. Do not edit.");
                writer.WriteLine("");

                foreach (var cookie in cookies.OrderBy(c => c.Domain).ThenBy(c => c.Path))
                {
                    var domain = SanitizeNetscapeField(cookie.Domain);
                    var path = SanitizeNetscapeField(cookie.Path);
                    var name = SanitizeNetscapeField(cookie.Name);
                    var value = SanitizeNetscapeField(cookie.Value);
                    if (string.IsNullOrEmpty(domain) || string.IsNullOrEmpty(name))
                        continue;

                    var flag = domain.StartsWith('.') ? "TRUE" : "FALSE";
                    var secure = cookie.Secure ? "TRUE" : "FALSE";
                    var expiration = (long)(cookie.Expires - DateTime.UnixEpoch).TotalSeconds;

                    writer.WriteLine($"{domain}\t{flag}\t{path}\t{secure}\t{expiration}\t{name}\t{value}");
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task ReopenBrowserAsync(string processName)
    {
        try
        {
            if (processName == "msedge" && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                foreach (var candidate in GetEdgeExecutableCandidates())
                {
                    if (!File.Exists(candidate))
                        continue;
                    _logger.LogInformation("Cookie export: starting Edge from {Exe}", candidate);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = candidate,
                        UseShellExecute = true
                    });
                    await Task.Delay(3000);
                    return;
                }

                _logger.LogInformation("Cookie export: starting Edge via shell executable name msedge");
                Process.Start(new ProcessStartInfo
                {
                    FileName = "msedge",
                    UseShellExecute = true
                });
                await Task.Delay(3000);
                return;
            }

            var executablePath = processName switch
            {
                "chrome" => GetChromeExecutablePath(),
                "chromium" => GetChromiumExecutablePath(),
                _ => null
            };

            if (!string.IsNullOrEmpty(executablePath) && File.Exists(executablePath))
            {
                _logger.LogInformation("Cookie export: starting browser from {Exe}", executablePath);
                Process.Start(new ProcessStartInfo
                {
                    FileName = executablePath,
                    UseShellExecute = true
                });

                await Task.Delay(3000);
            }
            else
                _logger.LogWarning("Cookie export: could not find executable to reopen for {Process}", processName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cookie export: reopen browser failed for {Process}", processName);
        }
    }

    private static IEnumerable<string> GetEdgeExecutableCandidates()
    {
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Microsoft", "Edge", "Application", "msedge.exe");
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft", "Edge", "Application", "msedge.exe");
    }

    private string? GetChromeExecutablePath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return @"C:\Program Files\Google\Chrome\Application\chrome.exe";
        }
        return null;
    }

    private string? GetChromiumExecutablePath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return @"C:\Program Files\Chromium\Application\chromium.exe";
        }
        return null;
    }

    private class BrowserInfo
    {
        public string ProcessName { get; set; } = string.Empty;
        public string FolderName { get; set; } = string.Empty;
    }
}

public class CookieData
{
    public string Domain { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime Expires { get; set; }
    public bool Secure { get; set; }
    public bool HttpOnly { get; set; }
}

public class BrowserCookieExportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? CookiesPath { get; set; }
    public int CookieCount { get; set; }
}
