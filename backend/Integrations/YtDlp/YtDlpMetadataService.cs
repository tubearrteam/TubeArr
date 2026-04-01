using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

/// <summary>
/// Fetches channel and video metadata by running yt-dlp (--skip-download -j).
/// All metadata responsibility is delegated to yt-dlp; results are parsed and returned for import into the database.
/// </summary>
public static class YtDlpMetadataService
{
	const int DefaultTimeoutMs = 60000;

	/// <summary>Resolve yt-dlp executable path from config. Returns null if disabled or not set.</summary>
	public static async Task<string?> GetExecutablePathAsync(TubeArrDbContext db, CancellationToken ct = default)
	{
		var config = await db.YtDlpConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		if (config is null || !config.Enabled)
			return null;
		var path = (config.ExecutablePath ?? "").Trim();
		return string.IsNullOrWhiteSpace(path) ? null : path;
	}

	/// <summary>
	/// Resolves the Netscape cookies file for yt-dlp: explicit path in config, or default file next to the yt-dlp exe /
	/// &lt;contentRoot&gt;/yt-dlp/cookies.txt when unset. Relative paths resolve against the exe folder and content root.
	/// Uses <paramref name="contentRoot"/> when provided; otherwise <see cref="TubeArrAppPaths.ContentRoot"/> (set at startup).
	/// </summary>
	public static async Task<string?> GetCookiesPathAsync(TubeArrDbContext db, CancellationToken ct = default, string? contentRoot = null)
	{
		var config = await db.YtDlpConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		if (config is null)
			return null;
		var root = !string.IsNullOrWhiteSpace(contentRoot) ? contentRoot : TubeArrAppPaths.ContentRoot;
		return YtDlpCookiesPathResolver.GetEffectiveCookiesFilePath(config, root);
	}

	/// <summary>Run yt-dlp -j (and optional args), return parsed JSON lines from stdout. Returns empty if process fails or times out.
	/// Waits for the process to exit and buffers full stdout in memory before parsing (caller owns returned documents).
	/// When flatPlaylist is true, adds --flat-playlist for faster listing with minimal metadata per entry.</summary>
	public static async Task<List<JsonDocument>> RunYtDlpJsonAsync(
		string executablePath,
		string url,
		CancellationToken ct = default,
		int? playlistItems = null,
		int timeoutMs = DefaultTimeoutMs,
		bool flatPlaylist = false,
		int? playlistEnd = null,
		string? cookiesPath = null)
	{
		return await YtDlpProcessRunner.RunInProcessStyleAsync(
			YtDlpProcessRunner.YtDlpProcessStyle.Metadata,
			async _ =>
			{
				var args = BuildYtDlpJsonArgs(playlistItems, flatPlaylist, playlistEnd, cookiesPath);
				args.Add(url);
				using var process = new Process();
				process.StartInfo.FileName = executablePath;
				YtDlpProcessRunner.ApplyArguments(process.StartInfo, args);
				process.StartInfo.RedirectStandardOutput = true;
				process.StartInfo.RedirectStandardError = true;
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.CreateNoWindow = true;
				process.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
				process.StartInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;

				process.Start();
				using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
				cts.CancelAfter(timeoutMs);
				var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
				try
				{
					await process.WaitForExitAsync(cts.Token);
				}
				catch (OperationCanceledException oce) when (!ct.IsCancellationRequested)
				{
					try { process.Kill(entireProcessTree: true); } catch { }
					throw new TimeoutException($"yt-dlp metadata request timed out after {timeoutMs}ms.", oce);
				}
				catch (OperationCanceledException)
				{
					try { process.Kill(entireProcessTree: true); } catch { }
					throw;
				}

				var stdout = await stdoutTask;
				if (process.ExitCode != 0)
					return new List<JsonDocument>();

				var list = new List<JsonDocument>();
				using var reader = new StringReader(stdout ?? "");
				string? line;
				while ((line = await reader.ReadLineAsync(ct)) != null)
				{
					line = line.Trim();
					if (string.IsNullOrEmpty(line)) continue;
					try
					{
						list.Add(JsonDocument.Parse(line));
					}
					catch
					{
						// Skip malformed lines
					}
				}
				return list;
			},
			ct);
	}

	static List<string> BuildYtDlpJsonArgs(int? playlistItems, bool flatPlaylist, int? playlistEnd = null, string? cookiesPath = null)
	{
		// --skip-download: do not download; -j/--dump-json: one JSON object per video to stdout; --no-warnings: suppress warnings; --no-progress: no progress bar (silent)
		var args = new List<string> { "--skip-download", "-j", "--no-warnings", "--no-progress" };
		YtDlpCommandBuilder.AppendYoutubeAuthMitigations(args, cookiesPath);
		if (flatPlaylist)
			args.Add("--flat-playlist");
		if (playlistEnd.HasValue)
		{
			args.Add("--playlist-end");
			args.Add(playlistEnd.Value.ToString(CultureInfo.InvariantCulture));
		}
		if (playlistItems.HasValue)
		{
			// --playlist-items: use range 1:N for "first N items" (single number would mean "only item at index N")
			args.Add("--playlist-items");
			args.Add($"1:{playlistItems.Value}");
		}
		return args;
	}

	/// <summary>Run yt-dlp -j and invoke the callback for each JSON line as it is read from stdout (streaming).
	/// Does not wait for the full channel output; inserts can happen incrementally. Process is run until exit or timeout.
	/// When flatPlaylist is true, adds --flat-playlist for faster listing with minimal metadata per entry.
	/// When dumpOutputPath is set, each line is also appended to that file (JSONL dump).</summary>
	public static async Task RunYtDlpJsonLinesAsync(
		string executablePath,
		string url,
		CancellationToken ct,
		int? playlistItems,
		int timeoutMs,
		bool flatPlaylist = false,
		string? dumpOutputPath = null,
		Func<JsonDocument, ValueTask>? onLineAsync = null,
		Action<JsonDocument>? onLine = null,
		int? playlistEnd = null,
		string? cookiesPath = null)
	{
		await YtDlpProcessRunner.RunInProcessStyleAsync(
			YtDlpProcessRunner.YtDlpProcessStyle.Metadata,
			async _ =>
			{
				var args = BuildYtDlpJsonArgs(playlistItems, flatPlaylist, playlistEnd, cookiesPath);
				args.Add(url);

				using var process = new Process();
				process.StartInfo.FileName = executablePath;
				YtDlpProcessRunner.ApplyArguments(process.StartInfo, args);
				process.StartInfo.RedirectStandardOutput = true;
				process.StartInfo.RedirectStandardError = true;
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.CreateNoWindow = true;
				process.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
				process.StartInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;

				process.Start();
				using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
				cts.CancelAfter(timeoutMs);
				using var reader = new StreamReader(process.StandardOutput.BaseStream, System.Text.Encoding.UTF8);
				using var dumpWriter = !string.IsNullOrWhiteSpace(dumpOutputPath)
					? new StreamWriter(dumpOutputPath, append: false, System.Text.Encoding.UTF8)
					: null;
				string? line;
				try
				{
					while ((line = await reader.ReadLineAsync(cts.Token)) != null)
					{
						if (dumpWriter != null && !string.IsNullOrEmpty(line))
							await dumpWriter.WriteLineAsync(line);
						line = line.Trim();
						if (string.IsNullOrEmpty(line)) continue;
						try
						{
							using var doc = JsonDocument.Parse(line);
							if (onLineAsync != null)
								await onLineAsync(doc);
							else
								onLine!(doc);
						}
						catch
						{
							// Skip malformed lines
						}
					}
				}
				catch (OperationCanceledException) when (!ct.IsCancellationRequested)
				{
					try { process.Kill(entireProcessTree: true); } catch { }
					throw new TimeoutException($"yt-dlp metadata request timed out after {timeoutMs}ms.");
				}

				try
				{
					await process.WaitForExitAsync(cts.Token);
				}
				catch (OperationCanceledException) when (!ct.IsCancellationRequested)
				{
					try { process.Kill(entireProcessTree: true); } catch { }
					throw new TimeoutException($"yt-dlp metadata request timed out after {timeoutMs}ms.");
				}
				return 0;
			},
			ct);
	}

	/// <summary>Extract channel metadata from a single yt-dlp JSON object (video or channel entry).</summary>
	public static (string? ChannelId, string? Title, string? Description, string? ThumbnailUrl) ParseChannelFromEntry(JsonElement root)
	{
		string? channelId = null;
		string? title = null;
		string? description = null;
		string? thumbnailUrl = null;

		if (root.TryGetProperty("channel_id", out var cid) && cid.ValueKind == JsonValueKind.String)
			channelId = cid.GetString();
		if (string.IsNullOrWhiteSpace(channelId) && root.TryGetProperty("uploader_id", out var uid) && uid.ValueKind == JsonValueKind.String)
			channelId = uid.GetString();

		if (root.TryGetProperty("channel", out var ch) && ch.ValueKind == JsonValueKind.String)
			title = ch.GetString()?.Trim();
		if (string.IsNullOrWhiteSpace(title) && root.TryGetProperty("uploader", out var up) && up.ValueKind == JsonValueKind.String)
			title = up.GetString()?.Trim();

		if (root.TryGetProperty("description", out var desc) && desc.ValueKind == JsonValueKind.String)
			description = desc.GetString();

		if (root.TryGetProperty("thumbnail", out var thumb) && thumb.ValueKind == JsonValueKind.String)
			thumbnailUrl = thumb.GetString()?.Trim();
		if (string.IsNullOrWhiteSpace(thumbnailUrl) && root.TryGetProperty("thumbnails", out var thumbs) && thumbs.ValueKind == JsonValueKind.Array && thumbs.GetArrayLength() > 0)
		{
			var first = thumbs[0];
			if (first.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String)
				thumbnailUrl = u.GetString()?.Trim();
		}

		return (channelId, title, description, thumbnailUrl);
	}

	/// <summary>Fetch channel metadata for a known channel ID using yt-dlp (first video from channel).</summary>
	public static async Task<(string? Title, string? Description, string? ThumbnailUrl)?> GetChannelMetadataAsync(
		string executablePath,
		string channelId,
		CancellationToken ct = default,
		string? cookiesPath = null)
	{
		var url = $"https://www.youtube.com/channel/{channelId}/videos";
		var docs = await RunYtDlpJsonAsync(executablePath, url, ct, playlistItems: 1, cookiesPath: cookiesPath);
		try
		{
			if (docs.Count == 0) return null;
			var (_, title, description, thumbnailUrl) = ParseChannelFromEntry(docs[0].RootElement);
			return (title, description, thumbnailUrl);
		}
		finally
		{
			foreach (var d in docs)
				d.Dispose();
		}
	}

	/// <summary>Search channels by term using yt-dlp (ytsearch), return unique channels with metadata.</summary>
	public static async Task<List<(string ChannelId, string Title, string? Description, string? ThumbnailUrl)>> SearchChannelsAsync(
		string executablePath,
		string term,
		int maxResults = 20,
		CancellationToken ct = default,
		string? cookiesPath = null)
	{
		var url = $"ytsearch{maxResults}: {term}";
		var docs = await RunYtDlpJsonAsync(executablePath, url, ct, cookiesPath: cookiesPath);
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var list = new List<(string, string, string?, string?)>();
		try
		{
			foreach (var doc in docs)
			{
				var (channelId, title, description, thumbnailUrl) = ParseChannelFromEntry(doc.RootElement);
				if (string.IsNullOrWhiteSpace(channelId) || string.IsNullOrWhiteSpace(title)) continue;
				if (seen.Contains(channelId)) continue;
				seen.Add(channelId);
				list.Add((channelId, title, description, thumbnailUrl));
			}
		}
		finally
		{
			foreach (var d in docs)
				d.Dispose();
		}
		return list;
	}
}
