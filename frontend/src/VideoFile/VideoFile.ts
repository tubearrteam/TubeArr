import ModelBase from 'App/ModelBase';
import Language from 'Language/Language';
import { QualityModel } from 'Quality/Quality';
import CustomFormat from 'typings/CustomFormat';
import MediaInfo from 'typings/MediaInfo';

export interface VideoFile extends ModelBase {
  channelId: number;
  playlistNumber: number;
  relativePath: string;
  path: string;
  size: number;
  /** File duration in seconds from ffprobe (when probed). */
  fileDurationSeconds?: number;
  dateAdded: string;
  releaseGroup: string;
  languages: Language[];
  quality: QualityModel;
  customFormats: CustomFormat[];
  customFormatScore: number;
  indexerFlags: number;
  releaseType: string;
  mediaInfo: MediaInfo;
  qualityCutoffNotMet: boolean;
}
