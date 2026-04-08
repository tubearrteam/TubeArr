export default interface SlskdSettings {
  enabled: boolean;
  baseUrl: string;
  apiKey: string;
  localDownloadsPath: string;
  searchTimeoutSeconds: number;
  maxCandidatesStored: number;
  autoPickMinScore: number;
  manualReviewOnly: boolean;
  retryAttempts: number;
  acquisitionOrder: string;
  fallbackToSlskdOnYtDlpFailure: boolean;
  fallbackToYtDlpOnSlskdFailure: boolean;
  higherQualityHandling: number;
  requireManualReviewOnTranscode: boolean;
  keepOriginalAfterTranscode: boolean;
}
