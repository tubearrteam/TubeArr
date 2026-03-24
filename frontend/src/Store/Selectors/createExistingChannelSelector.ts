import { some } from 'lodash';
import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';
import createAllChannelsSelector from './createAllChannelSelector';

function createExistingChannelSelector() {
  return createSelector(
    (_: AppState, { youtubeChannelId }: { youtubeChannelId?: string }) => ({ youtubeChannelId }),
    createAllChannelsSelector(),
    ({ youtubeChannelId }, channels) => {
      if (youtubeChannelId) {
        return some(channels, { youtubeChannelId });
      }

      return false;
    }
  );
}

export default createExistingChannelSelector;
