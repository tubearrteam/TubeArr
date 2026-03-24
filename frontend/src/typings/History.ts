import Language from 'Language/Language';
import { QualityModel } from 'Quality/Quality';
import CustomFormat from './CustomFormat';

export type HistoryEventType =
  | 'grabbed'
  | 'channelFolderImported'
  | 'downloadFolderImported'
  | 'downloadFailed'
  | 'videoFileDeleted'
  | 'videoFileRenamed'
  | 'downloadIgnored';

export interface GrabbedHistoryData {
  indexer: string;
  infoUrl: string;
  releaseGroup: string;
  age: string;
  ageHours: string;
  ageMinutes: string;
  publishedDate: string;
  downloadClient: string;
  downloadClientName: string;
  size: string;
  downloadUrl: string;
  guid: string;
  protocol: string;
  customFormatScore?: string;
  channelMatchType: string;
  releaseSource: string;
  indexerFlags: string;
  releaseType: string;
}

export interface DownloadFailedHistory {
  message: string;
}

export interface DownloadFolderImportedHistory {
  customFormatScore?: string;
  downloadClient: string;
  downloadClientName: string;
  droppedPath: string;
  importedPath: string;
}

export interface VideoFileDeletedHistory {
  customFormatScore?: string;
  reason: 'Manual' | 'MissingFromDisk' | 'Upgrade';
}

export interface VideoFileRenamedHistory {
  sourcePath: string;
  sourceRelativePath: string;
  path: string;
  relativePath: string;
}

export interface DownloadIgnoredHistory {
  message: string;
}

export type HistoryData =
  | GrabbedHistoryData
  | DownloadFailedHistory
  | DownloadFolderImportedHistory
  | VideoFileDeletedHistory
  | VideoFileRenamedHistory
  | DownloadIgnoredHistory;

export default interface History {
  videoId: number;
  channelId: number;
  sourceTitle: string;
  languages: Language[];
  quality: QualityModel;
  customFormats: CustomFormat[];
  customFormatScore: number;
  qualityCutoffNotMet: boolean;
  date: string;
  downloadId: string;
  eventType: HistoryEventType;
  data: HistoryData;
  id: number;
}
