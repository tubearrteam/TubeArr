import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';
import Channel from 'Channel/Channel';

function createMultiChannelSelector(channelIds: number[]) {
  return createSelector(
    (state: AppState) => state.channels.itemMap,
    (state: AppState) => state.channels.items,
    (itemMap, allChannels) => {
      const ids = Array.isArray(channelIds) ? channelIds : [];
      return ids.reduce((acc: Channel[], channelId) => {
        const channel = allChannels[itemMap[channelId]];

        if (channel) {
          acc.push(channel);
        }

        return acc;
      }, []);
    }
  );
}

export default createMultiChannelSelector;
