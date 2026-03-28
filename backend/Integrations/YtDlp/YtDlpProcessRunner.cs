using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace TubeArr.Backend;

/// <summary>
/// Runs yt-dlp process with given arguments. Single place for process spawning and output capture.
/// </summary>
public static class YtDlpProcessRunner
{
	/// <summary>Populate argv via <see cref="ProcessStartInfo.ArgumentList"/> so paths with spaces and non-ASCII
	/// are not broken by manual quoting (a common reason <c>--cookies</c> appears ignored on Windows).</summary>
	public static void ApplyArguments(ProcessStartInfo psi, IReadOnlyList<string> args)
	{
		psi.ArgumentList.Clear();
		foreach (var a in args)
			psi.ArgumentList.Add(a);
	}

	public sealed record DownloadProgressInfo(double? Progress, int? EstimatedSecondsRemaining);

	public const int DefaultTimeoutMs = 60_000;
	/// <summary>Bounded parallelism for concurrent yt-dlp download processes (different channels/queue items).</summary>
	public const int DownloadConcurrencySlots = 3;
	/// <summary>Bounded parallelism for yt-dlp metadata / playlist-json style calls so RSS fallback, HTML, and API paths can overlap.</summary>
	public const int MetadataConcurrencySlots = 6;
	static readonly SemaphoreSlim DownloadStyleSlots = new(DownloadConcurrencySlots, DownloadConcurrencySlots);
	static readonly SemaphoreSlim MetadataStyleSlots = new(MetadataConcurrencySlots, MetadataConcurrencySlots);

	public enum YtDlpProcessStyle
	{
		Download = 0,
		Metadata = 1
	}

	static SemaphoreSlim GetStyleSemaphore(YtDlpProcessStyle processStyle)
	{
		return processStyle == YtDlpProcessStyle.Download ? DownloadStyleSlots : MetadataStyleSlots;
	}

	public static async Task<T> RunInProcessStyleAsync<T>(
		YtDlpProcessStyle processStyle,
		Func<CancellationToken, Task<T>> run,
		CancellationToken ct = default,
		ILogger? logger = null)
	{
		var sem = GetStyleSemaphore(processStyle);
		await sem.WaitAsync(ct);
		try
		{
			return await run(ct);
		}
		finally
		{
			sem.Release();
			logger?.LogDebug("yt-dlp style slot released style={ProcessStyle}", processStyle);
		}
	}

	/// <summary>Run yt-dlp with the given argument list. Returns (stdout, stderr, exitCode).</summary>
	public static async Task<(string Stdout, string Stderr, int ExitCode)> RunAsync(
		string executablePath,
		IReadOnlyList<string> args,
		CancellationToken ct = default,
		int timeoutMs = DefaultTimeoutMs,
		ILogger? logger = null,
		YtDlpProcessStyle processStyle = YtDlpProcessStyle.Metadata)
	{
		return await RunWithProgressAsync(executablePath, args, null, ct, timeoutMs, logger, processStyle);
	}

	/// <summary>
	/// Run yt-dlp and report download progress by parsing stderr lines like "[download] 45.2% of ...".
	/// onProgress is called with progress 0.0â€“1.0; callbacks are throttled and may be awaited for persistence.
	/// </summary>
	public static async Task<(string Stdout, string Stderr, int ExitCode)> RunWithProgressAsync(
		string executablePath,
		IReadOnlyList<string> args,
		Func<DownloadProgressInfo, ValueTask>? onProgress,
		CancellationToken ct = default,
		int timeoutMs = DefaultTimeoutMs,
		ILogger? logger = null,
		YtDlpProcessStyle processStyle = YtDlpProcessStyle.Metadata)
	{
		return await RunInProcessStyleAsync(
			processStyle,
			async _ =>
		{
			logger?.LogDebug("yt-dlp start: style={ProcessStyle} exe={Exe} argCount={ArgCount} timeoutMs={TimeoutMs}", processStyle, executablePath, args.Count, timeoutMs);
			using var process = new Process();
			process.StartInfo.FileName = executablePath;
			ApplyArguments(process.StartInfo, args);
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.RedirectStandardError = true;
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.CreateNoWindow = true;
			process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
			process.StartInfo.StandardErrorEncoding = Encoding.UTF8;

			process.Start();
			var stdoutSb = new StringBuilder();
			// Some yt-dlp builds emit download progress on stdout instead of stderr.
			var progressRegex = new Regex(@"(\d+(?:\.\d+)?)\s*%", RegexOptions.Compiled);
			var etaRegex = new Regex(@"ETA\s+(?<eta>\d{1,2}:\d{2}(?::\d{2})?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
			var lastReportedProgress = -1.0;
			var lastReportTime = 0L;
			const int ProgressReportIntervalMs = 400;
			using var reportLock = new SemaphoreSlim(1, 1);

			async ValueTask TryReportProgressAsync(string line)
			{
				if (line.Length == 0 || !line.Contains("[download]", StringComparison.OrdinalIgnoreCase))
					return;

				var progressMatch = progressRegex.Match(line);
				if (!progressMatch.Success ||
					!double.TryParse(progressMatch.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var pct))
					return;

				var progress = Math.Clamp(pct / 100.0, 0.0, 1.0);
				var etaSeconds = TryParseEtaSeconds(line, etaRegex);

				await reportLock.WaitAsync(ct);
				try
				{
					var now = Environment.TickCount64;
					var shouldReport = progress >= 1.0 || (progress > lastReportedProgress && (now - lastReportTime) >= ProgressReportIntervalMs);
					if (!shouldReport || onProgress == null)
						return;

					lastReportedProgress = progress;
					lastReportTime = now;
				}
				finally
				{
					reportLock.Release();
				}

				await onProgress(new DownloadProgressInfo(progress, etaSeconds));
			}

			async Task<string> ReadProgressStreamAsync(StreamReader reader, string streamName)
			{
				var buffer = new char[4096];
				var lineBuf = new StringBuilder();
				while (true)
				{
					var read = await reader.ReadAsync(buffer.AsMemory(), ct);
					if (read == 0) break;
					for (var i = 0; i < read; i++)
					{
						var c = buffer[i];
						stdoutSb.Append(c);
						if (c == '\n' || c == '\r')
						{
							var line = lineBuf.ToString().Trim();
							lineBuf.Clear();
							if (line.Length > 0)
							{
								logger?.LogDebug("yt-dlp {StreamName}: {Line}", streamName, line);
								await TryReportProgressAsync(line);
							}
						}
						else
							lineBuf.Append(c);
					}
				}

				var rest = lineBuf.ToString().Trim();
				if (rest.Length > 0)
				{
					logger?.LogDebug("yt-dlp {StreamName}: {Line}", streamName, rest);
					await TryReportProgressAsync(rest);
				}

				return stdoutSb.ToString();
			}

			Task<string> stdoutReadTask;
			Task<string> stderrReadTask;

			if (processStyle == YtDlpProcessStyle.Download && onProgress != null)
			{
				stdoutReadTask = Task.Run(() => ReadProgressStreamAsync(process.StandardOutput, "stdout"), ct);
				stderrReadTask = process.StandardError.ReadToEndAsync(ct);
			}
			else
			{
				stdoutReadTask = process.StandardOutput.ReadToEndAsync(ct);
				stderrReadTask = process.StandardError.ReadToEndAsync(ct);
			}

			using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(timeoutMs);
			try
			{
				await process.WaitForExitAsync(cts.Token);
			}
			catch (OperationCanceledException)
			{
				try { process.Kill(entireProcessTree: true); } catch { }
				logger?.LogWarning("yt-dlp timeout: style={ProcessStyle} exe={Exe} timeoutMs={TimeoutMs}", processStyle, executablePath, timeoutMs);
				throw;
			}

			var stdout = await stdoutReadTask;
			var stderr = await stderrReadTask;
			logger?.LogDebug("yt-dlp exit: style={ProcessStyle} code={ExitCode}", processStyle, process.ExitCode);
			return (stdout ?? "", stderr, process.ExitCode);
		},
			ct,
			logger);
	}

	static int? TryParseEtaSeconds(string line, Regex etaRegex)
	{
		var match = etaRegex.Match(line);
		if (!match.Success)
			return null;

		var etaValue = match.Groups["eta"].Value;
		if (string.IsNullOrWhiteSpace(etaValue))
			return null;

		var parts = etaValue
			.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (parts.Length is < 2 or > 3)
			return null;
		if (!parts.All(part => int.TryParse(part, out _)))
			return null;

		return parts.Length switch
		{
			2 => (int.Parse(parts[0]) * 60) + int.Parse(parts[1]),
			3 => (int.Parse(parts[0]) * 3600) + (int.Parse(parts[1]) * 60) + int.Parse(parts[2]),
			_ => null
		};
	}
}
