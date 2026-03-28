import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import * as commandNames from 'Commands/commandNames';
import { executeCommand } from 'Store/Actions/commandActions';
import { setVideosSort, setVideosTableOption, toggleVideosMonitored } from 'Store/Actions/videoActions';
import { togglePlaylistMonitored } from 'Store/Actions/channelActions';
import createClientSideCollectionSelector from 'Store/Selectors/createClientSideCollectionSelector';
import createCommandsSelector from 'Store/Selectors/createCommandsSelector';
import createDimensionsSelector from 'Store/Selectors/createDimensionsSelector';
import createChannelSelector from 'Store/Selectors/createChannelSelector';
import { CHANNEL_ID_REGEX } from 'Utilities/Channel/channelInputClassifier';
import { findCommand, isCommandExecuting } from 'Utilities/Command';
import ChannelDetailsPlaylist from './ChannelDetailsPlaylist';

function createMapStateToProps() {
  return createSelector(
    (state, { playlistNumber }) => playlistNumber,
    createClientSideCollectionSelector('videos'),
    createChannelSelector(),
    createCommandsSelector(),
    createDimensionsSelector(),
    (playlistNumber, videos, channel, commands, dimensions) => {
      const addedAtMs = channel?.added ? new Date(channel.added).getTime() : NaN;
      const isRecentlyAdded = Number.isFinite(addedAtMs) && (Date.now() - addedAtMs) < (10 * 60 * 1000);
      const isChannelRefreshing = isCommandExecuting(findCommand(commands, {
        name: commandNames.REFRESH_CHANNEL,
        channelId: channel.id
      }));
      const channelRefreshingCommand = findCommand(commands, {
        name: commandNames.REFRESH_CHANNEL
      });
      const allChannelRefreshing = (
        isCommandExecuting(channelRefreshingCommand) &&
        !channelRefreshingCommand?.body?.channelId
      );
      const isDownloading = isCommandExecuting(findCommand(commands, {
        name: commandNames.DOWNLOAD_MONITORED,
        channelId: channel.id
      }));
      const isRssSyncExecuting = isCommandExecuting(findCommand(commands, {
        name: commandNames.RSS_SYNC,
        channelId: channel.id
      }));
      const isGettingVideoDetails = isCommandExecuting(findCommand(commands, {
        name: commandNames.GET_VIDEO_DETAILS,
        channelId: channel.id
      }));
      const isGettingPlaylists = isCommandExecuting(findCommand(commands, {
        name: commandNames.GET_CHANNEL_PLAYLISTS,
        channelId: channel.id
      }));
      const isIdentifyingShorts = isCommandExecuting(findCommand(commands, {
        name: 'RefreshChannelShortsParsing',
        channelId: channel.id
      }));
      const isMetadataOperationExecuting = isChannelRefreshing || allChannelRefreshing || isRssSyncExecuting || isGettingVideoDetails || isGettingPlaylists;
      const youtubeChannelId = channel.youtubeChannelId ?? '';
      const canIdentifyShorts = CHANNEL_ID_REGEX.test(youtubeChannelId.trim());

      // playlistNumber 1 = "Videos" (hide Shorts from main list only when channel has a YouTube Shorts tab).
      const videosInPlaylist = videos.items.filter((video) => {
        if (video.channelId !== channel.id) return false;
        if (playlistNumber === 1) {
          if (channel.filterOutShorts && channel.hasShortsTab === true && video.isShort === true) return false;
          if (channel.filterOutLivestreams && video.isLivestream === true) return false;
          return true;
        }
        if (playlistNumber === -1) return video.isShort === true;
        if (playlistNumber === -2) return video.isLivestream === true;
        return video.playlistNumber === playlistNumber;
      });

      const allVideosMonitored = videosInPlaylist.length > 0 && videosInPlaylist.every((v) => v.monitored);
      return {
        items: videosInPlaylist,
        columns: videos.columns,
        sortKey: videos.sortKey,
        sortDirection: videos.sortDirection,
        isSearching: isDownloading,
        isMetadataOperationExecuting,
        isPopulatingVideos: playlistNumber === 1 && videosInPlaylist.length === 0 && (isChannelRefreshing || allChannelRefreshing || isRecentlyAdded),
        channelMonitored: channel.monitored,
        channelType: channel.channelType,
        path: channel.path,
        isSmallScreen: dimensions.isSmallScreen,
        allVideosMonitored,
        youtubeChannelId,
        canIdentifyShorts,
        isIdentifyingShorts
      };
    }
  );
}

const mapDispatchToProps = {
  togglePlaylistMonitored,
  toggleVideosMonitored,
  setVideosTableOption,
  setVideosSort,
  executeCommand
};

class ChannelDetailsPlaylistConnector extends Component {

  //
  // Listeners
  //

  onTableOptionChange = (payload) => {
    this.props.setVideosTableOption(payload);
  };

  onMonitorPlaylistPress = (monitored) => {
    const {
      channelId: channelId,
      playlistNumber
    } = this.props;

    this.props.togglePlaylistMonitored({
      channelId: channelId,
      playlistNumber,
      monitored
    });
  };

  onSearchPress = (scope = 'playlist') => {
    const { channelId, playlistNumber, isMetadataOperationExecuting } = this.props;
    if (isMetadataOperationExecuting) {
      return;
    }
    const playlistScoped = scope === 'playlist';
    const shouldSendPlaylistNumber = playlistScoped && playlistNumber > 1;
    const payload = {
      name: commandNames.DOWNLOAD_MONITORED,
      channelId
    };
    if (shouldSendPlaylistNumber) {
      payload.playlistNumber = playlistNumber;
    }

    this.props.executeCommand(payload);
  };

  onDownloadVideoPress = (videoId) => {
    const { channelId, isMetadataOperationExecuting } = this.props;
    if (isMetadataOperationExecuting) {
      return;
    }
    this.props.executeCommand({
      name: commandNames.DOWNLOAD_MONITORED,
      channelId,
      videoIds: [videoId]
    });
  };

  onIdentifyShortsPress = () => {
    const { channelId } = this.props;
    this.props.executeCommand({
      name: commandNames.REFRESH_CHANNEL,
      channelId,
      phase: 'shortsParsing'
    });
  };

  onMonitorVideoPress = (videoIds, monitored) => {
    this.props.toggleVideosMonitored({
      videoIds,
      monitored
    });
  };

  onInvertMonitoredPress = () => {
    const { items, toggleVideosMonitored } = this.props;
    if (!items.length) return;
    const allMonitored = items.every((v) => v.monitored);
    const videoIds = items.map((v) => v.id);
    toggleVideosMonitored({ videoIds, monitored: !allMonitored });
  };

  onSortPress = (sortKey, sortDirection) => {
    this.props.setVideosSort({
      sortKey,
      sortDirection
    });
  };

  //
  // Render
  //

  render() {
    return (
      <ChannelDetailsPlaylist
        {...this.props}
        onTableOptionChange={this.onTableOptionChange}
        onSortPress={this.onSortPress}
        onMonitorPlaylistPress={this.onMonitorPlaylistPress}
        onInvertMonitoredPress={this.onInvertMonitoredPress}
        onSearchPress={this.onSearchPress}
        onDownloadVideoPress={this.onDownloadVideoPress}
        onIdentifyShortsPress={this.onIdentifyShortsPress}
        onMonitorVideoPress={this.onMonitorVideoPress}
      />
    );
  }
}

ChannelDetailsPlaylistConnector.propTypes = {
  channelId: PropTypes.number.isRequired,
  playlistNumber: PropTypes.number.isRequired,
  togglePlaylistMonitored: PropTypes.func.isRequired,
  toggleVideosMonitored: PropTypes.func.isRequired,
  setVideosTableOption: PropTypes.func.isRequired,
  setVideosSort: PropTypes.func.isRequired,
  executeCommand: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(ChannelDetailsPlaylistConnector);

