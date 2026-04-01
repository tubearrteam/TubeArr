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
  | 'specificVideos'
  | 'specificPlaylists'
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
  lastUploadUtc?: string;
  /** Oldest air date among videos with a non-blank air date (API: statistics.firstUploadUtc). */
  firstUploadUtc?: string;
  releaseGroups: string[];
  sizeOnDisk: number;
  totalVideoCount: number;
}

export interface ChannelCustomPlaylistRule {
  field: string;
  operator: string;
  value?: unknown;
}

export interface ChannelCustomPlaylist {
  id: number;
  channelId: number;
  name: string;
  enabled: boolean;
  priority: number;
  /** 0 = All rules, 1 = Any rule */
  matchType: number;
  rules: ChannelCustomPlaylistRule[];
  playlistNumber: number;
}

export interface Playlist {
  title?: string;
  monitored: boolean;
  playlistNumber: number;
  statistics: Statistics;
  isSaving?: boolean;
  isCustom?: boolean;
  customPlaylistId?: number | null;
  /** Internal DB id for curated YouTube playlists (not the synthetic "Videos" row). */
  playlistId?: number | null;
  /** Sort order vs other curated playlists; lower first. */
  priority?: number;
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
  /** Server: which specific monitoring preset is active (optional). */
  monitorPreset?: string | null;
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
  /** When a video is in multiple curated playlists, which wins for on-disk paths (0–3). */
  playlistMultiMatchStrategy?: number;
  /** Permutation of 0–3: tie-break order (matches API). */
  playlistMultiMatchStrategyOrder?: string;
  /** When true, YouTube Shorts are excluded (server support may be added separately). */
  filterOutShorts?: boolean;
  /** When true, livestream videos are excluded from monitored/main listing. */
  filterOutLivestreams?: boolean;
  /** Heuristic from channel page (Shorts tab). Undefined/null when unknown. */
  hasShortsTab?: boolean | null;
  /** Heuristic from channel page (Streams / Live tab). Undefined/null when unknown. */
  hasStreamsTab?: boolean | null;
  playlists: Playlist[];
  /** Per-channel filter-based playlists (API: customPlaylists). */
  customPlaylists?: ChannelCustomPlaylist[];
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
