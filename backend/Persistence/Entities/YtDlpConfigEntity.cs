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
}
