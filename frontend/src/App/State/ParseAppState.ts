import ModelBase from 'App/ModelBase';
import { AppSectionItemState } from 'App/State/AppSectionState';
import Video from 'Video/Video';
import Language from 'Language/Language';
import { QualityModel } from 'Quality/Quality';
import Channel from 'Channel/Channel';
import CustomFormat from 'typings/CustomFormat';

export interface ChannelTitleInfo {
  title: string;
  titleWithoutYear: string;
  year: number;
  allTitles: string[];
}

export interface ParsedVideoInfo {
  releaseTitle: string;
  channelTitle: string;
  channelTitleInfo: ChannelTitleInfo;
  quality: QualityModel;
  playlistNumber: number;
  videoNumbers: number[];
  absoluteVideoNumbers: number[];
  specialAbsoluteVideoNumbers: number[];
  languages: Language[];
  fullPlaylist: boolean;
  isPartialPlaylist: boolean;
  isMultiPlaylist: boolean;
  isPlaylistExtra: boolean;
  special: boolean;
  releaseHash: string;
  playlistPart: number;
  releaseGroup?: string;
  releaseTokens: string;
  airDate?: string;
  isDaily: boolean;
  isAbsoluteNumbering: boolean;
  isPossibleSpecialVideo: boolean;
}

export interface ParseModel extends ModelBase {
  title: string;
  parsedVideoInfo: ParsedVideoInfo;
  channel?: Channel;
  videos: Video[];
  languages?: Language[];
  customFormats?: CustomFormat[];
  customFormatScore?: number;
}

type ParseAppState = AppSectionItemState<ParseModel>;

export default ParseAppState;
