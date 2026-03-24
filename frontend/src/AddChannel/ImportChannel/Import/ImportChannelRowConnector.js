import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { setImportChannelValue } from 'Store/Actions/importChannelActions';
import createAllChannelsSelector from 'Store/Selectors/createAllChannelSelector';
import ImportChannelRow from './ImportChannelRow';

function createImportChannelItemSelector() {
  return createSelector(
    (state, { id }) => id,
    (state) => state.importChannel.items,
    (id, items) => {
      return _.find(items, { id }) || {};
    }
  );
}

function createMapStateToProps() {
  return createSelector(
    createImportChannelItemSelector(),
    createAllChannelsSelector(),
    (item, channels) => {
      const selectedChannel = item && item.selectedChannel;
      const isExistingChannel = !!selectedChannel && (
        (selectedChannel.youtubeChannelId && _.some(channels, { youtubeChannelId: selectedChannel.youtubeChannelId }))
      );

      return {
        ...item,
        isExistingChannel
      };
    }
  );
}

const mapDispatchToProps = {
  setImportChannelValue
};

class ImportChannelRowConnector extends Component {

  //
  // Listeners

  onInputChange = ({ name, value }) => {
    this.props.setImportChannelValue({
      id: this.props.id,
      [name]: value
    });
  };

  //
  // Render

  render() {
    // Don't show the row until we have the information we require for it.

    const {
      items,
      monitor,
      channelType,
      playlistFolder
    } = this.props;

    if (!items || !monitor || !channelType || !playlistFolder == null) {
      return null;
    }

    return (
      <ImportChannelRow
        {...this.props}
        onInputChange={this.onInputChange}
      />
    );
  }
}

ImportChannelRowConnector.propTypes = {
  rootFolderId: PropTypes.number.isRequired,
  id: PropTypes.string.isRequired,
  monitor: PropTypes.string,
  channelType: PropTypes.string,
  playlistFolder: PropTypes.bool,
  items: PropTypes.arrayOf(PropTypes.object),
  setImportChannelValue: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(ImportChannelRowConnector);
