import Video from 'Video/Video';

export interface CalendarItem extends Omit<Video, 'airDateUtc'> {
  airDateUtc: string;
}

export interface CalendarEvent extends CalendarItem {
  isGroup: false;
}

export interface CalendarEventGroup {
  isGroup: true;
  channelId: number;
  playlistNumber: number;
  videoIds: number[];
  events: CalendarItem[];
}

export type CalendarStatus =
  | 'downloaded'
  | 'downloading'
  | 'unmonitored'
  | 'onAir'
  | 'missing'
  | 'unaired';
