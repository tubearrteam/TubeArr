import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { push } from 'redux-first-history';
import { createSelector } from 'reselect';
import * as commandNames from 'Commands/commandNames';
import { showMessage } from 'Store/Actions/appActions';
import { executeCommand } from 'Store/Actions/commandActions';
import { clearVideos, fetchVideos } from 'Store/Actions/videoActions';
import { clearVideoFiles, fetchVideoFiles } from 'Store/Actions/videoFileActions';
import { clearQueueDetails, fetchQueueDetails } from 'Store/Actions/queueActions';
import { toggleChannelMonitored } from 'Store/Actions/channelActions';
import createAllChannelSelector from 'Store/Selectors/createAllChannelSelector';
import createCommandsSelector from 'Store/Selectors/createCommandsSelector';
import { findCommand, isCommandExecuting } from 'Utilities/Command';
import { registerPagePopulator, unregisterPagePopulator } from 'Utilities/pagePopulator';
import getPathWithUrlBase from 'Utilities/getPathWithUrlBase';
import ChannelDetails from './ChannelDetails';

function normalizeEscapedNewlines(value) {
  if (typeof value !== 'string') {
    return '';
  }

  return value.replace(/\\r\\n|\\n|\\r/g, '\n');
}

const selectVideos = createSelector(
  (state) => state.videos,
  (videos) => {
    const {
      items,
      isFetching,
      isPopulated,
      error
    } = videos;

    const hasVideos = !!items.length;
    const hasMonitoredVideos = items.some((v) => v.monitored);

    return {
      isVideosFetching: isFetching,
      isVideosPopulated: isPopulated,
      videosError: error,
      hasVideos,
      hasMonitoredVideos
    };
  }
);

const selectVideoFiles = createSelector(
  (state) => state.videoFiles,
  (videoFiles) => {
    const {
      items,
      isFetching,
      isPopulated,
      error
    } = videoFiles;

    const hasVideoFiles = !!items.length;

    return {
      isVideoFilesFetching: isFetching,
      isVideoFilesPopulated: isPopulated,
      videoFilesError: error,
      hasVideoFiles
    };
  }
);

function createMapStateToProps() {
  return createSelector(
    (state, { titleSlug }) => titleSlug,
    selectVideos,
    selectVideoFiles,
    createAllChannelSelector(),
    createCommandsSelector(),
    (titleSlug, videos, videoFiles, allChannels, commands) => {
      const sortedChannels = _.orderBy(allChannels, 'sortTitle');
      const channelIndex = _.findIndex(sortedChannels, { titleSlug });
      const channel = sortedChannels[channelIndex];

      if (!channel) {
        return {};
      }

      const {
        isVideosFetching,
        isVideosPopulated,
        videosError,
        hasVideos,
        hasMonitoredVideos
      } = videos;

      const {
        isVideoFilesFetching,
        isVideoFilesPopulated,
        videoFilesError,
        hasVideoFiles
      } = videoFiles;

      const previousChannel = sortedChannels[channelIndex - 1] || _.last(sortedChannels);
      const nextChannel = sortedChannels[channelIndex + 1] || _.first(sortedChannels);
      const isChannelRefreshing = isCommandExecuting(findCommand(commands, { name: commandNames.REFRESH_CHANNEL, channelId: channel.id }));
      const channelRefreshingCommand = findCommand(commands, { name: commandNames.REFRESH_CHANNEL });
      const allChannelRefreshing = (
        isCommandExecuting(channelRefreshingCommand) &&
        !channelRefreshingCommand.body.channelId
      );
      const isRefreshing = isChannelRefreshing || allChannelRefreshing;
      const isSearching = isCommandExecuting(findCommand(commands, { name: commandNames.DOWNLOAD_MONITORED, channelId: channel.id }));
      const isRssSyncExecuting = isCommandExecuting(findCommand(commands, { name: commandNames.RSS_SYNC, channelId: channel.id }));
      const isGettingVideoDetails = isCommandExecuting(findCommand(commands, { name: commandNames.GET_VIDEO_DETAILS, channelId: channel.id }));
      const isMetadataOperationExecuting = isRefreshing || isRssSyncExecuting || isGettingVideoDetails;
      const isRenamingFiles = isCommandExecuting(findCommand(commands, { name: commandNames.RENAME_FILES, channelId: channel.id }));
      const isRenamingChannelCommand = findCommand(commands, { name: commandNames.RENAME_CHANNEL });
      const isRenamingChannel = (
        isCommandExecuting(isRenamingChannelCommand) &&
        isRenamingChannelCommand.body.channelIds.indexOf(channel.id) > -1
      );

      const isFetching = isVideosFetching || isVideoFilesFetching;
      const isPopulated = isVideosPopulated && isVideoFilesPopulated;
      const alternateTitles = channel.alternateTitles || [];

      const overview = normalizeEscapedNewlines(channel.description ?? '');
      const images = [];
      if (channel.thumbnailUrl) {
        images.push({ coverType: 'poster', url: channel.thumbnailUrl });
      }
      if (channel.bannerUrl) {
        images.push({ coverType: 'banner', url: channel.bannerUrl });
        images.push({ coverType: 'fanart', url: channel.bannerUrl });
      } else if (channel.thumbnailUrl) {
        images.push({ coverType: 'fanart', url: channel.thumbnailUrl });
      }

      return {
        ...channel,
        overview,
        images,
        alternateTitles,
        isChannelRefreshing,
        allChannelRefreshing,
        isRefreshing,
        isSearching,
        isRssSyncExecuting,
        isGettingVideoDetails,
        isMetadataOperationExecuting,
        isRenamingFiles,
        isRenamingChannel,
        isFetching,
        isPopulated,
        videosError,
        videoFilesError,
        hasVideos,
        hasMonitoredVideos,
        hasVideoFiles,
        previousChannel,
        nextChannel
      };
    }
  );
}

const mapDispatchToProps = {
  push,
  showMessage,
  fetchVideos,
  clearVideos,
  fetchVideoFiles,
  clearVideoFiles,
  toggleChannelMonitored,
  fetchQueueDetails,
  clearQueueDetails,
  executeCommand
};

class ChannelDetailsConnector extends Component {

  state = {
    isRefreshRequested: false
  };

  //
  // Lifecycle

  componentDidMount() {
    registerPagePopulator(this.populate, ['channelUpdated', 'videoUpdated']);
    this.populate();
  }

  componentDidUpdate(prevProps) {
    const {
      id: channelId,
      isChannelRefreshing,
      allChannelRefreshing,
      isRenamingFiles,
      isRenamingChannel,
      isRssSyncExecuting,
      isGettingVideoDetails
    } = this.props;

    if (
      (prevProps.isChannelRefreshing && !isChannelRefreshing) ||
      (prevProps.allChannelRefreshing && !allChannelRefreshing) ||
      (prevProps.isRenamingFiles && !isRenamingFiles) ||
      (prevProps.isRenamingChannel && !isRenamingChannel) ||
      (prevProps.isRssSyncExecuting && !isRssSyncExecuting) ||
      (prevProps.isGettingVideoDetails && !isGettingVideoDetails)
    ) {
      this.populate();
    }

    // If the id has changed, fetch the new channel's data (don't clear first —
    // the new fetch will replace the list and avoids briefly showing 0/0).
    if (prevProps.id !== channelId) {
      this.populate();
    }
  }

  componentWillUnmount() {
    unregisterPagePopulator(this.populate);
    this.unpopulate();
  }

  //
  // Control

  populate = () => {
    const channelId = this.props.id;
    // Only fetch when we have a channel id (avoids fetch-all then clear when id loads)
    if (channelId != null) {
      this.props.fetchVideos({ channelId });
    }
    this.props.fetchVideoFiles({ channelId: channelId });
    this.props.fetchQueueDetails({ channelId: channelId });
  };

  unpopulate = () => {
    this.props.clearVideos();
    this.props.clearVideoFiles();
    this.props.clearQueueDetails();
  };

  //
  // Listeners

  onMonitorTogglePress = (monitored) => {
    this.props.toggleChannelMonitored({
      channelId: this.props.id,
      monitored
    });
  };

  onRefreshPress = () => {
    this.setState({ isRefreshRequested: true });
    this.props.executeCommand({
      name: commandNames.REFRESH_CHANNEL,
      channelId: this.props.id
    }).then((command) => {
      if (command && command.message) {
        this.props.showMessage({
          message: command.message,
          type: 'warning',
          hideAfter: 10
        });
      }
    }).finally(() => {
      this.setState({ isRefreshRequested: false });
    });
  };

  onSearchPress = () => {
    this.props.executeCommand({
      name: commandNames.DOWNLOAD_MONITORED,
      channelId: this.props.id
    });
  };

  onGetVideoDetailsPress = () => {
    this.props.executeCommand({
      name: commandNames.GET_VIDEO_DETAILS,
      channelId: this.props.id
    });
  };

  onRssSyncPress = () => {
    this.props.executeCommand({
      name: commandNames.RSS_SYNC,
      channelId: this.props.id
    });
  };

  onChannelDeleteComplete = () => {
    this.props.push(getPathWithUrlBase('/channels'));
  };

  //
  // Render

  render() {
    const isRefreshing = this.state.isRefreshRequested || this.props.isRefreshing;
    return (
      <ChannelDetails
        {...this.props}
        isRefreshing={isRefreshing}
        onMonitorTogglePress={this.onMonitorTogglePress}
        onRefreshPress={this.onRefreshPress}
        onSearchPress={this.onSearchPress}
        onRssSyncPress={this.onRssSyncPress}
        onGetVideoDetailsPress={this.onGetVideoDetailsPress}
        onChannelDeleteComplete={this.onChannelDeleteComplete}
      />
    );
  }
}

ChannelDetailsConnector.propTypes = {
  id: PropTypes.number.isRequired,
  titleSlug: PropTypes.string.isRequired,
  isChannelRefreshing: PropTypes.bool.isRequired,
  allChannelRefreshing: PropTypes.bool.isRequired,
  isRefreshing: PropTypes.bool.isRequired,
  isGettingVideoDetails: PropTypes.bool.isRequired,
  isMetadataOperationExecuting: PropTypes.bool.isRequired,
  isRenamingFiles: PropTypes.bool.isRequired,
  isRenamingChannel: PropTypes.bool.isRequired,
  showMessage: PropTypes.func.isRequired,
  fetchVideos: PropTypes.func.isRequired,
  clearVideos: PropTypes.func.isRequired,
  fetchVideoFiles: PropTypes.func.isRequired,
  clearVideoFiles: PropTypes.func.isRequired,
  toggleChannelMonitored: PropTypes.func.isRequired,
  fetchQueueDetails: PropTypes.func.isRequired,
  clearQueueDetails: PropTypes.func.isRequired,
  executeCommand: PropTypes.func.isRequired,
  push: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(ChannelDetailsConnector);
