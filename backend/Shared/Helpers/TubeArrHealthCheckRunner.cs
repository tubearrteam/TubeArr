using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class TubeArrHealthCheckRunner
{
	public static async Task<List<Dictionary<string, object?>>> CollectAsync(
		TubeArrDbContext db,
		YouTubeDataApiMetadataService youTubeDataApi,
		CancellationToken ct = default,
		string? contentRoot = null)
	{
		var results = new List<Dictionary<string, object?>>();
		var root = string.IsNullOrWhiteSpace(contentRoot) ? TubeArrAppPaths.ContentRoot : contentRoot;

		try
		{
			await db.Database.CanConnectAsync(ct);
			results.Add(new Dictionary<string, object?>
			{
				["type"] = "Database",
				["status"] = "ok",
				["message"] = "Database connection succeeded."
			});
		}
		catch (Exception ex)
		{
			results.Add(new Dictionary<string, object?>
			{
				["type"] = "Database",
				["status"] = "error",
				["message"] = ex.Message
			});
		}

		var ytPath = await YtDlpMetadataService.GetExecutablePathAsync(db, ct);
		if (string.IsNullOrWhiteSpace(ytPath))
		{
			results.Add(new Dictionary<string, object?>
			{
				["type"] = "YtDlp",
				["status"] = "warn",
				["message"] = "yt-dlp is not configured or disabled; downloads may not work."
			});
		}
		else if (!File.Exists(ytPath))
		{
			results.Add(new Dictionary<string, object?>
			{
				["type"] = "YtDlp",
				["status"] = "error",
				["message"] = $"yt-dlp executable path does not exist: {ytPath}"
			});
		}
		else
		{
			results.Add(new Dictionary<string, object?>
			{
				["type"] = "YtDlp",
				["status"] = "ok",
				["message"] = ytPath
			});

			var version = await TryGetYtDlpVersionLineAsync(ytPath, ct);
			if (version is not null)
			{
				results.Add(new Dictionary<string, object?>
				{
					["type"] = "YtDlpVersion",
					["status"] = "ok",
					["message"] = version
				});
			}
			else
			{
				results.Add(new Dictionary<string, object?>
				{
					["type"] = "YtDlpVersion",
					["status"] = "warn",
					["message"] = "Could not read yt-dlp version (process failed or timed out)."
				});
			}
		}

		var ffmpeg = await db.FFmpegConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		var ffmpegPath = (ffmpeg?.ExecutablePath ?? "").Trim();
		if (string.IsNullOrWhiteSpace(ffmpegPath))
		{
			results.Add(new Dictionary<string, object?>
			{
				["type"] = "FFmpeg",
				["status"] = "warn",
				["message"] = "FFmpeg path is not configured."
			});
		}
		else if (!File.Exists(ffmpegPath))
		{
			results.Add(new Dictionary<string, object?>
			{
				["type"] = "FFmpeg",
				["status"] = "warn",
				["message"] = $"FFmpeg path does not exist: {ffmpegPath}"
			});
		}
		else
		{
			results.Add(new Dictionary<string, object?>
			{
				["type"] = "FFmpeg",
				["status"] = "ok",
				["message"] = ffmpegPath
			});
		}

		var cookiesPath = await YtDlpMetadataService.GetCookiesPathAsync(db, ct, root);
		if (string.IsNullOrWhiteSpace(cookiesPath))
		{
			results.Add(new Dictionary<string, object?>
			{
				["type"] = "YtDlpCookies",
				["status"] = "warn",
				["message"] = "No Netscape cookies file configured; some videos may require cookies."
			});
		}
		else
		{
			try
			{
				var full = Path.GetFullPath(cookiesPath.Trim());
				if (!File.Exists(full))
				{
					results.Add(new Dictionary<string, object?>
					{
						["type"] = "YtDlpCookies",
						["status"] = "warn",
						["message"] = $"Cookies file not found: {full}"
					});
				}
				else
				{
					await using var fs = File.OpenRead(full);
					var buf = new byte[1];
					var read = await fs.ReadAsync(buf.AsMemory(0, 1), ct);
					results.Add(new Dictionary<string, object?>
					{
						["type"] = "YtDlpCookies",
						["status"] = read > 0 ? "ok" : "warn",
						["message"] = read > 0 ? full : $"Cookies file is empty: {full}"
					});
				}
			}
			catch (Exception ex)
			{
				results.Add(new Dictionary<string, object?>
				{
					["type"] = "YtDlpCookies",
					["status"] = "warn",
					["message"] = $"Cannot read cookies file: {ex.Message}"
				});
			}
		}

		var rootFolders = await db.RootFolders.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
		if (rootFolders.Count == 0)
		{
			results.Add(new Dictionary<string, object?>
			{
				["type"] = "RootFolders",
				["status"] = "warn",
				["message"] = "No root folders configured."
			});
		}
		else
		{
			foreach (var rf in rootFolders)
			{
				var p = (rf.Path ?? "").Trim();
				if (string.IsNullOrWhiteSpace(p))
				{
					results.Add(new Dictionary<string, object?>
					{
						["type"] = "RootFolder",
						["status"] = "warn",
						["message"] = $"Root folder id={rf.Id} has an empty path."
					});
					continue;
				}

				try
				{
					var full = Path.GetFullPath(p);
					if (!Directory.Exists(full))
					{
						results.Add(new Dictionary<string, object?>
						{
							["type"] = "RootFolder",
							["status"] = "error",
							["message"] = $"Root folder missing on disk: {full}"
						});
						continue;
					}

					var probe = Path.Combine(full, ".tubearr-write-test");
					try
					{
						await File.WriteAllTextAsync(probe, "ok", ct);
						File.Delete(probe);
						results.Add(new Dictionary<string, object?>
						{
							["type"] = "RootFolder",
							["status"] = "ok",
							["message"] = full
						});
					}
					catch (Exception ex)
					{
						results.Add(new Dictionary<string, object?>
						{
							["type"] = "RootFolder",
							["status"] = "warn",
							["message"] = $"Root folder not writable: {full} ({ex.Message})"
						});
					}
				}
				catch (Exception ex)
				{
					results.Add(new Dictionary<string, object?>
					{
						["type"] = "RootFolder",
						["status"] = "warn",
						["message"] = $"Root folder path invalid id={rf.Id}: {ex.Message}"
					});
				}
			}
		}

		var youTubeCheck = await youTubeDataApi.TryBuildHealthCheckAsync(db, ct);
		if (youTubeCheck is not null)
			results.Add(youTubeCheck);

		return results;
	}

	static async Task<string?> TryGetYtDlpVersionLineAsync(string executablePath, CancellationToken ct)
	{
		try
		{
			using var proc = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = executablePath,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true
				}
			};
			YtDlpProcessRunner.ApplyArguments(proc.StartInfo, new[] { "--version" });
			if (!proc.Start())
				return null;
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(TimeSpan.FromSeconds(15));
			try
			{
				var stdout = await proc.StandardOutput.ReadToEndAsync(cts.Token);
				await proc.WaitForExitAsync(cts.Token);
				var line = (stdout ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
				return string.IsNullOrWhiteSpace(line) ? null : line.Trim();
			}
			catch (OperationCanceledException) when (!ct.IsCancellationRequested)
			{
				try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
				return null;
			}
		}
		catch
		{
			return null;
		}
	}

	public static string Summarize(IReadOnlyList<Dictionary<string, object?>> checks)
	{
		static string? StatusOf(Dictionary<string, object?> c) =>
			c.TryGetValue("status", out var s) ? s?.ToString() : null;

		var errors = checks.Count(c => string.Equals(StatusOf(c), "error", StringComparison.OrdinalIgnoreCase));
		var warns = checks.Count(c => string.Equals(StatusOf(c), "warn", StringComparison.OrdinalIgnoreCase));
		if (errors > 0)
			return $"{errors} error(s), {warns} warning(s). See command body or GET /health.";
		if (warns > 0)
			return $"Healthy with {warns} warning(s).";
		return "All checks passed.";
	}
}
