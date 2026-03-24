import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { queueLookupChannel, setImportChannelValue } from 'Store/Actions/importChannelActions';
import createImportChannelItemSelector from 'Store/Selectors/createImportChannelItemSelector';
import * as channelTypes from 'Utilities/Channel/channelTypes';
import ImportChannelSelectChannel from './ImportChannelSelectChannel';

function createMapStateToProps() {
  return createSelector(
    (state) => state.importChannel.isLookingUpChannel,
    createImportChannelItemSelector(),
    (isLookingUpChannel, item) => {
      return {
        isLookingUpChannel,
        ...item
      };
    }
  );
}

const mapDispatchToProps = {
  queueLookupChannel,
  setImportChannelValue
};

class ImportChannelSelectChannelConnector extends Component {

  //
  // Listeners

  onSearchInputChange = (term) => {
    this.props.queueLookupChannel({
      name: this.props.id,
      term,
      topOfQueue: true
    });
  };

  onChannelSelect = (key) => {
    const {
      id,
      items,
      onInputChange
    } = this.props;

    const selectedChannel = items.find((item) => item.youtubeChannelId === key);

    this.props.setImportChannelValue({
      id,
      selectedChannel
    });

    if (selectedChannel.channelType !== channelTypes.STANDARD) {
      onInputChange({
        name: 'channelType',
        value: selectedChannel.channelType
      });
    }
  };

  //
  // Render

  render() {
    return (
      <ImportChannelSelectChannel
        {...this.props}
        onSearchInputChange={this.onSearchInputChange}
        onChannelSelect={this.onChannelSelect}
      />
    );
  }
}

ImportChannelSelectChannelConnector.propTypes = {
  id: PropTypes.string.isRequired,
  items: PropTypes.arrayOf(PropTypes.object),
  selectedChannel: PropTypes.object,
  isSelected: PropTypes.bool,
  onInputChange: PropTypes.func.isRequired,
  queueLookupChannel: PropTypes.func.isRequired,
  setImportChannelValue: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(ImportChannelSelectChannelConnector);
