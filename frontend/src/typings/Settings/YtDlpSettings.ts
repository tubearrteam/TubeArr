export default interface YtDlpSettings {
  executablePath: string;
  enabled: boolean;
  cookiesPath: string;
  cookiesExportBrowser: string;
  /** Parallel download queue workers (1–10). Omitted on older API responses. */
  downloadQueueParallelWorkers?: number;
  downloadTransientMaxRetries?: number;
  downloadRetryDelaysSecondsJson?: string;
}
