type ExistingChannelLike = {
  title?: string;
  description?: string;
  overview?: string;
} | null | undefined;

export interface NewChannelPayload {
  youtubeChannelId: string;
  title?: string;
  description?: string;
  monitored?: boolean;
  /** UI sends monitor option key (e.g. 'all', 'none', 'roundRobin'); mapped to monitored boolean */
  monitor?: string;
  qualityProfileId?: number;
  rootFolderPath?: string;
  channelType?: string;
  playlistFolder?: boolean;
  tags?: number[];
  monitorNewItems?: number;
  roundRobinLatestVideoCount?: number | string | null;
}

export interface CreateChannelRequest {
  youtubeChannelId: string;
  title?: string;
  description?: string;
  monitored: boolean;
  qualityProfileId?: number;
  rootFolderPath?: string;
  channelType?: string;
  playlistFolder?: boolean;
  tags?: number[];
  monitorNewItems?: number;
  roundRobinLatestVideoCount?: number;
}

/**
 * Build the create-channel request from existing search result and modal payload.
 * Passes through all backend-supported create options (do not drop qualityProfileId or other supported fields).
 */
function getNewChannel(existing: ExistingChannelLike, payload: NewChannelPayload): CreateChannelRequest {
  const youtubeChannelId = (payload.youtubeChannelId ?? '').trim();
  const monitorKey = payload.monitor ?? 'all';
  const isNone = monitorKey === 'none';
  const isRoundRobin = monitorKey === 'roundRobin';

  const monitored = typeof payload.monitored === 'boolean'
    ? payload.monitored
    : !isNone;

  const qualityProfileId = payload.qualityProfileId ?? 0;
  const rootFolderPath = payload.rootFolderPath;
  const channelType = payload.channelType;
  const playlistFolder = payload.playlistFolder;
  const tags = payload.tags;

  const existingTitle = (existing?.title ?? '').trim();
  const existingDescription = existing?.description ?? existing?.overview;

  const title = (payload.title ?? existingTitle).trim();
  const description = payload.description ?? existingDescription;

  let monitorNewItems: number | undefined = payload.monitorNewItems;
  let roundRobinLatestVideoCount: number | undefined;

  if (isNone) {
    monitorNewItems = 0;
  } else if (isRoundRobin) {
    const raw = payload.roundRobinLatestVideoCount;
    const n = typeof raw === 'number' ? raw : parseInt(String(raw ?? '').trim(), 10);
    monitorNewItems = 1;
    if (Number.isFinite(n) && n > 0) {
      roundRobinLatestVideoCount = n;
    }
  } else if (monitorNewItems === undefined) {
    monitorNewItems = monitored ? 1 : 0;
  }

  const result: CreateChannelRequest = {
    youtubeChannelId,
    title: title || undefined,
    description: description || undefined,
    monitored,
    qualityProfileId,
    rootFolderPath: rootFolderPath || undefined,
    channelType: channelType || undefined,
    playlistFolder,
    tags,
    monitorNewItems
  };

  if (roundRobinLatestVideoCount != null) {
    result.roundRobinLatestVideoCount = roundRobinLatestVideoCount;
  }

  return result;
}

export default getNewChannel;
