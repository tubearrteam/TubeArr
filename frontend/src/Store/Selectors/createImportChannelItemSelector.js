import _ from 'lodash';
import { createSelector } from 'reselect';
import createAllChannelsSelector from './createAllChannelSelector';

function createImportChannelItemSelector() {
  return createSelector(
    (state, { id }) => id,
    (state) => state.addChannel,
    (state) => state.importChannel,
    createAllChannelsSelector(),
    (id, addChannel, importChannel, channels) => {
      const item = _.find(importChannel.items, { id }) || {};
      const selectedChannel = item && item.selectedChannel;
      const isExistingChannel = !!selectedChannel && (
        (selectedChannel.youtubeChannelId && _.some(channels, { youtubeChannelId: selectedChannel.youtubeChannelId }))
      );

      return {
        defaultMonitor: addChannel.defaults.monitor,
        defaultQualityProfileId: addChannel.defaults.qualityProfileId,
        defaultChannelType: addChannel.defaults.channelType,
        defaultPlaylistFolder: addChannel.defaults.playlistFolder,
        ...item,
        isExistingChannel
      };
    }
  );
}

export default createImportChannelItemSelector;
