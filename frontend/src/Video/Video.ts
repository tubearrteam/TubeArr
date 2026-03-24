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
  grabbed?: boolean;
  endTime?: string;
  grabDate?: string;
  channelTitle?: string;
  queued?: boolean;
  channel?: Channel;
  finaleType?: string;
}

export default Video;
