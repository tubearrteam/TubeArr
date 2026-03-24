import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { queueLookupChannel, setImportChannelValue } from 'Store/Actions/importChannelActions';
import createAllChannelsSelector from 'Store/Selectors/createAllChannelSelector';
import ImportChannelTable from './ImportChannelTable';

function createMapStateToProps() {
  return createSelector(
    (state) => state.addChannel,
    (state) => state.importChannel,
    (state) => state.app.dimensions,
    createAllChannelsSelector(),
    (addChannel, importChannel, dimensions, allChannels) => {
      return {
        defaultMonitor: addChannel.defaults.monitor,
        defaultQualityProfileId: addChannel.defaults.qualityProfileId,
        defaultChannelType: addChannel.defaults.channelType,
        defaultPlaylistFolder: addChannel.defaults.playlistFolder,
        items: importChannel.items,
        isSmallScreen: dimensions.isSmallScreen,
        allChannels
      };
    }
  );
}

function createMapDispatchToProps(dispatch, props) {
  return {
    onChannelLookup(name, path, relativePath) {
      dispatch(queueLookupChannel({
        name,
        path,
        relativePath,
        term: name
      }));
    },

    onSetImportChannelValue(values) {
      dispatch(setImportChannelValue(values));
    }
  };
}

export default connect(createMapStateToProps, createMapDispatchToProps)(ImportChannelTable);
