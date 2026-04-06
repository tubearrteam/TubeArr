namespace TubeArr.Backend.Data;

public sealed class YtDlpConfigEntity
{
	public int Id { get; set; } = 1;

	public string ExecutablePath { get; set; } = "";
	public bool Enabled { get; set; } = true;
	public string CookiesPath { get; set; } = "";

	/// <summary>Browser key for in-app cookie export: chrome, edge, or chromium.</summary>
	public string CookiesExportBrowser { get; set; } = "chrome";

	/// <summary>Parallel download queue workers (each runs yt-dlp for one queue item). Clamped when applied.</summary>
	public int DownloadQueueParallelWorkers { get; set; } = 1;

	/// <summary>Extra download attempts after the first failure for retry-eligible errors (e.g. transient network). Clamped 0–10 when applied.</summary>
	public int DownloadTransientMaxRetries { get; set; } = 3;

	/// <summary>JSON array of positive delay seconds between retries, e.g. <c>[30,60,120]</c>. Empty or invalid uses built-in defaults.</summary>
	public string DownloadRetryDelaysSecondsJson { get; set; } = "[30,60,120]";
}
