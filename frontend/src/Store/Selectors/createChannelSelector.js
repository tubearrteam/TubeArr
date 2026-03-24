import { createSelector } from 'reselect';

export function createChannelSelectorForHook(channelId) {
  return createSelector(
      (state) => state.channels.itemMap,
      (state) => state.channels.items,
    (itemMap, allChannels) => {
      return channelId ? allChannels[itemMap[channelId]] : undefined;
    }
  );
}

function createChannelSelector() {
  return createSelector(
    (state, { channelId }) => channelId,
    (state) => state.channels.itemMap,
    (state) => state.channels.items,
    (channelId, itemMap, allChannels) => {
      return allChannels[itemMap[channelId]];
    }
  );
}

export default createChannelSelector;
