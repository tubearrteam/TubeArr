import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';

export interface ChannelQueueDetails {
  count: number;
  videosWithFiles: number;
}

function createChannelQueueDetailsSelector(
  channelId: number,
  playlistNumber?: number
) {
  return createSelector(
    (state: AppState) => state.queue.details.items,
    (queueItems) => {
      const items = Array.isArray(queueItems) ? queueItems : [];
      return items.reduce(
        (acc: ChannelQueueDetails, item) => {
          if (
            item.trackedDownloadState === 'imported' ||
            item.channelId !== channelId
          ) {
            return acc;
          }

          if (playlistNumber != null && item.playlistNumber !== playlistNumber) {
            return acc;
          }

          acc.count++;

          if (item.videoHasFile) {
            acc.videosWithFiles++;
          }

          return acc;
        },
        {
          count: 0,
          videosWithFiles: 0,
        }
      );
    }
  );
}

export default createChannelQueueDetailsSelector;
