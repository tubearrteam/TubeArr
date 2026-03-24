import { useSelector } from 'react-redux';
import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';

export function createChannelSelector(channelId?: number) {
  return createSelector(
    (state: AppState) => state.channels.itemMap,
    (state: AppState) => state.channels.items,
    (itemMap, allChannels) => {
      return channelId ? allChannels[itemMap[channelId]] : undefined;
    }
  );
}

function useChannel(channelId?: number) {
  return useSelector(createChannelSelector(channelId));
}

export default useChannel;
