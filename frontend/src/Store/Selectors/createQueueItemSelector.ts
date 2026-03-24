import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';

export function createQueueItemSelectorForHook(videoId: number) {
  return createSelector(
    (state: AppState) => state.queue.details.items,
    (details) => {
      if (!videoId || !details) {
        return null;
      }

      return details.find((item) => item.videoId === videoId);
    }
  );
}

function createQueueItemSelector() {
  return createSelector(
    (_: AppState, { videoId }: { videoId: number }) => videoId,
    (state: AppState) => state.queue.details.items,
    (videoId, details) => {
      if (!videoId || !details) {
        return null;
      }

      return details.find((item) => item.videoId === videoId);
    }
  );
}

export default createQueueItemSelector;
