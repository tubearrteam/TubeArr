import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { saveChannel, setChannelValue } from 'Store/Actions/channelActions';
import createChannelSelector from 'Store/Selectors/createChannelSelector';
import selectSettings from 'Store/Selectors/selectSettings';
import EditChannelModalContent from './EditChannelModalContent';

function createChannelSelectorFromChannelId() {
  const selectChannelByChannelId = createChannelSelector();

  return (state, { channelId }) => {
    return selectChannelByChannelId(state, { channelId: channelId });
  };
}

function createIsPathChangingSelector() {
  return createSelector(
    (state) => state.channels.pendingChanges,
    createChannelSelectorFromChannelId(),
    (state, props) => props.channelId,
    (pendingChanges, channel, channelId) => {
      const scoped = (channelId != null && pendingChanges != null && typeof pendingChanges[channelId] === 'object')
        ? pendingChanges[channelId]
        : pendingChanges;
      const path = scoped && scoped.path;

      if (path == null) {
        return false;
      }

      return channel && channel.path !== path;
    }
  );
}

function createMapStateToProps() {
  return createSelector(
    (state) => state.channels,
    createChannelSelectorFromChannelId(),
    createIsPathChangingSelector(),
    (state, props) => props.channelId,
    (channelsState, channel, isPathChanging, channelIdFromProps) => {
      const {
        isSaving,
        saveError,
        pendingChanges: sectionPendingChanges
      } = channelsState;

      const channelId = channelIdFromProps ?? channel?.id;
      const pendingChanges = (channelId != null && sectionPendingChanges != null && typeof sectionPendingChanges[channelId] === 'object')
        ? sectionPendingChanges[channelId]
        : (sectionPendingChanges ?? {});

      const channelSettings = channel ? _.pick(channel, [
        'monitored',
        'monitorNewItems',
        'monitorPreset',
        'playlistFolder',
        'filterOutShorts',
        'filterOutLivestreams',
        'qualityProfileId',
        'channelType',
        'path',
        'tags'
      ]) : {};

      if (channelSettings.monitorNewItems === 0 || channelSettings.monitorNewItems === 1) {
        channelSettings.monitorNewItems = channelSettings.monitorNewItems === 1 ? 'all' : 'none';
      }
      if (channelSettings.tags != null && typeof channelSettings.tags === 'string') {
        channelSettings.tags = channelSettings.tags.split(',')
          .map((s) => parseInt(s.trim(), 10))
          .filter((n) => !Number.isNaN(n));
      }

      const settings = selectSettings(channelSettings, pendingChanges, saveError);

      return {
        title: channel ? channel.title : '',
        isSaving,
        saveError,
        isPathChanging,
        originalPath: channel ? channel.path : '',
        item: settings.settings,
        ...settings
      };
    }
  );
}

const mapDispatchToProps = {
  dispatchSetChannelValue: setChannelValue,
  dispatchSaveChannel: saveChannel
};

class EditChannelModalContentConnector extends Component {

  //
  // Lifecycle

  componentDidUpdate(prevProps, prevState) {
    if (prevProps.isSaving && !this.props.isSaving && !this.props.saveError) {
      this.props.onModalClose();
    }
  }

  //
  // Listeners

  onInputChange = ({ name, value }) => {
    this.props.dispatchSetChannelValue({ name, value, id: this.props.channelId });
  };

  onSavePress = (moveFiles) => {
    this.props.dispatchSaveChannel({
      id: this.props.channelId,
      moveFiles
    });
  };

  //
  // Render

  render() {
    return (
      <EditChannelModalContent
        {...this.props}
        onInputChange={this.onInputChange}
        onSavePress={this.onSavePress}
      />
    );
  }
}

EditChannelModalContentConnector.propTypes = {
  channelId: PropTypes.number.isRequired,
  isSaving: PropTypes.bool.isRequired,
  saveError: PropTypes.object,
  dispatchSetChannelValue: PropTypes.func.isRequired,
  dispatchSaveChannel: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(EditChannelModalContentConnector);
