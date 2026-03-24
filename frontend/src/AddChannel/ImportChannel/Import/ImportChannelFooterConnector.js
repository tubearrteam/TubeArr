import _ from 'lodash';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { cancelLookupChannel, lookupUnsearchedChannels } from 'Store/Actions/importChannelActions';
import ImportChannelFooter from './ImportChannelFooter';

function isMixed(items, selectedIds, defaultValue, key) {
  return _.some(items, (channel) => {
    return selectedIds.indexOf(channel.id) > -1 && channel[key] !== defaultValue;
  });
}

function createMapStateToProps() {
  return createSelector(
    (state) => state.addChannel,
    (state) => state.importChannel,
    (state, { selectedIds }) => selectedIds,
    (addChannel, importChannel, selectedIds) => {
      const {
        monitor: defaultMonitor,
        qualityProfileId: defaultQualityProfileId,
        channelType: defaultChannelType,
        playlistFolder: defaultPlaylistFolder
      } = addChannel.defaults;

      const {
        isLookingUpChannel,
        isImporting,
        items,
        importError
      } = importChannel;

      const isMonitorMixed = isMixed(items, selectedIds, defaultMonitor, 'monitor');
      const isQualityProfileIdMixed = isMixed(items, selectedIds, defaultQualityProfileId, 'qualityProfileId');
      const isChannelTypeMixed = isMixed(items, selectedIds, defaultChannelType, 'channelType');
      const isPlaylistFolderMixed = isMixed(items, selectedIds, defaultPlaylistFolder, 'playlistFolder');
      const hasUnsearchedItems = !isLookingUpChannel && items.some((item) => !item.isPopulated);

      return {
        selectedCount: selectedIds.length,
        isLookingUpChannel,
        isImporting,
        defaultMonitor,
        defaultQualityProfileId,
        defaultChannelType,
        defaultPlaylistFolder,
        isMonitorMixed,
        isQualityProfileIdMixed,
        isChannelTypeMixed,
        isPlaylistFolderMixed,
        importError,
        hasUnsearchedItems
      };
    }
  );
}

const mapDispatchToProps = {
  onLookupPress: lookupUnsearchedChannels,
  onCancelLookupPress: cancelLookupChannel
};

export default connect(createMapStateToProps, mapDispatchToProps)(ImportChannelFooter);
