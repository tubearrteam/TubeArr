import ModelBase from 'App/ModelBase';
import Video from 'Video/Video';
import Language from 'Language/Language';
import { QualityModel } from 'Quality/Quality';
import CustomFormat from 'typings/CustomFormat';

export type QueueTrackedDownloadStatus = 'ok' | 'warning' | 'error';

export type QueueTrackedDownloadState =
  | 'downloading'
  | 'importBlocked'
  | 'importPending'
  | 'importing'
  | 'imported'
  | 'failedPending'
  | 'failed'
  | 'ignored';

export interface StatusMessage {
  title: string;
  messages: string[];
}

interface Queue extends ModelBase {
  languages: Language[];
  quality: QualityModel;
  customFormats: CustomFormat[];
  /** yt-dlp selected format id(s), e.g. 137+140, from API `formatSummary`. */
  formatSummary?: string;
  downloadedBytes?: number;
  totalBytes?: number;
  speedBytesPerSecond?: number;
  customFormatScore: number;
  size: number;
  title: string;
  sizeleft: number;
  timeleft: string;
  estimatedCompletionTime?: number;
  estimatedSecondsRemaining?: number;
  added?: string;
  status: string;
  trackedDownloadStatus: QueueTrackedDownloadStatus;
  trackedDownloadState: QueueTrackedDownloadState;
  statusMessages: StatusMessage[];
  errorMessage: string;
  downloadId: string;
  protocol: string;
  downloadClient: string;
  outputPath: string;
  videoHasFile: boolean;
  channelId?: number;
  videoId?: number;
  playlistNumber?: number;
  downloadClientHasPostImportCategory: boolean;
  video?: Video;
}

export default Queue;
