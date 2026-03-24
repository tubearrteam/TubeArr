import Language from 'Language/Language';
import { QualityModel } from 'Quality/Quality';
import CustomFormat from 'typings/CustomFormat';

export interface ReleaseVideo {
  id: number;
  videoFileId: number;
  playlistNumber: number;
  videoNumber: number;
  absoluteVideoNumber?: number;
  title: string;
}

interface Release {
  guid: string;
  protocol: string;
  age: number;
  ageHours: number;
  ageMinutes: number;
  publishDate: string;
  title: string;
  infoUrl: string;
  indexerId: number;
  indexer: string;
  size: number;
  seeders?: number;
  leechers?: number;
  quality: QualityModel;
  languages: Language[];
  customFormats: CustomFormat[];
  customFormatScore: number;
  playlistNumber?: number;
  videoNumbers?: number[];
  absoluteVideoNumbers?: number[];
  mappedChannelId?: number;
  mappedPlaylistNumber?: number;
  mappedVideoNumbers?: number[];
  mappedAbsoluteVideoNumbers?: number[];
  mappedVideoInfo: ReleaseVideo[];
  indexerFlags: number;
  rejections: string[];
  videoRequested: boolean;
  downloadAllowed: boolean;
  isDaily: boolean;

  isGrabbing?: boolean;
  isGrabbed?: boolean;
  grabError?: string;
}

export default Release;
