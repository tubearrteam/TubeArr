import ModelBase from 'App/ModelBase';
import Language from 'Language/Language';

export type ChannelType = 'standard' | 'episodic' | 'daily' | 'streaming';
export type ChannelMonitor =
  | 'all'
  | 'future'
  | 'missing'
  | 'existing'
  | 'recent'
  | 'roundRobin'
  | 'pilot'
  | 'firstPlaylist'
  | 'lastPlaylist'
  | 'monitorSpecials'
  | 'unmonitorSpecials'
  | 'none';

export type ChannelStatus = 'continuing' | 'ended' | 'upcoming' | 'deleted';

export type MonitorNewItems = 'all' | 'none';

export type CoverType = 'poster' | 'banner' | 'fanart' | 'playlist';

export interface Image {
  coverType: CoverType;
  url: string;
  remoteUrl: string;
}

export interface Statistics {
  playlistCount: number;
  videoCount: number;
  videoFileCount: number;
  percentOfVideos: number;
  previousAiring?: Date;
  releaseGroups: string[];
  sizeOnDisk: number;
  totalVideoCount: number;
}

export interface Playlist {
  monitored: boolean;
  playlistNumber: number;
  statistics: Statistics;
  isSaving?: boolean;
}

export interface Ratings {
  votes: number;
  value: number;
}

export interface AlternateTitle {
  title: string;
  comment?: string;
}

export interface ChannelAddOptions {
  monitor: ChannelMonitor;
  searchForMissingVideos: boolean;
  searchForCutoffUnmetVideos: boolean;
}

export interface Channel extends ModelBase {
  added: string;
  alternateTitles: AlternateTitle[];
  certification: string;
  cleanTitle: string;
  ended: boolean;
  firstAired: string;
  genres: string[];
  images: Image[];
  monitored: boolean;
  monitorNewItems: MonitorNewItems;
  /** When set, only this many newest videos stay monitored (round-robin cap). */
  roundRobinLatestVideoCount?: number | null;
  network: string;
  originalLanguage: Language;
  overview: string;
  path: string;
  previousAiring?: string;
  nextAiring?: string;
  qualityProfileId: number;
  ratings: Ratings;
  rootFolderPath: string;
  runtime: number;
  playlistFolder: boolean;
  /** When true, YouTube Shorts are excluded (server support may be added separately). */
  filterOutShorts?: boolean;
  /** When true, livestream videos are excluded from monitored/main listing. */
  filterOutLivestreams?: boolean;
  playlists: Playlist[];
  channelType: ChannelType;
  sortTitle: string;
  statistics: Statistics;
  status: ChannelStatus;
  tags: number[];
  title: string;
  titleSlug: string;
  youtubeChannelId: string;
  bannerUrl?: string;
  year: number;
  isSaving?: boolean;
  addOptions: ChannelAddOptions;
}

export default Channel;
