import ModelBase from 'App/ModelBase';
import Channel from 'Channel/Channel';

interface Video extends ModelBase {
  channelId: number;
  videoFileId: number;
  playlistNumber: number;
  videoNumber: number;
  airDate: string;
  airDateUtc?: string;
  lastSearchTime?: string;
  runtime: number;
  absoluteVideoNumber?: number;
  overview: string;
  title: string;
  youtubeVideoId?: string;
  thumbnailUrl?: string;
  description?: string;
  videoFile?: object;
  hasFile: boolean;
  monitored: boolean;
  /** UI playlist numbers for curated YouTube playlists (2+). */
  curatedPlaylistNumbers?: number[];
  /** UI playlist numbers for channel custom filter playlists. */
  customPlaylistNumbers?: number[];
  /** True when the video is a YouTube Short (metadata and/or channel Shorts tab). */
  isShort?: boolean;
  /** True when metadata marks this as livestream/live-archive content. */
  isLivestream?: boolean;
  /** Serialized YouTube Data API `videos.list` fragments (snippet, contentDetails, statistics, status, liveStreamingDetails) when populated. */
  youTubeDataApiVideoResourceJson?: string;
  grabbed?: boolean;
  endTime?: string;
  grabDate?: string;
  channelTitle?: string;
  queued?: boolean;
  channel?: Channel;
  finaleType?: string;
}

export default Video;
