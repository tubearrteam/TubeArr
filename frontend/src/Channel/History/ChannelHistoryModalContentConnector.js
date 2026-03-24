import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { channelHistoryMarkAsFailed, clearChannelHistory, fetchChannelHistory } from 'Store/Actions/channelHistoryActions';
import ChannelHistoryModalContent from './ChannelHistoryModalContent';

function createMapStateToProps() {
  return createSelector(
    (state) => state.channelHistory,
    (channelHistory) => {
      return channelHistory;
    }
  );
}

const mapDispatchToProps = {
  fetchChannelHistory,
  clearChannelHistory,
  channelHistoryMarkAsFailed
};

class ChannelHistoryModalContentConnector extends Component {

  //
  // Lifecycle

  componentDidMount() {
    const {
      channelId,
      playlistNumber
    } = this.props;

    this.props.fetchChannelHistory({
      channelId,
      playlistNumber
    });
  }

  componentWillUnmount() {
    this.props.clearChannelHistory();
  }

  //
  // Listeners

  onMarkAsFailedPress = (historyId) => {
    const {
      channelId,
      playlistNumber
    } = this.props;

    this.props.channelHistoryMarkAsFailed({
      historyId,
      channelId,
      playlistNumber
    });
  };

  //
  // Render

  render() {
    return (
      <ChannelHistoryModalContent
        {...this.props}
        onMarkAsFailedPress={this.onMarkAsFailedPress}
      />
    );
  }
}

ChannelHistoryModalContentConnector.propTypes = {
  channelId: PropTypes.number.isRequired,
  playlistNumber: PropTypes.number,
  fetchChannelHistory: PropTypes.func.isRequired,
  clearChannelHistory: PropTypes.func.isRequired,
  channelHistoryMarkAsFailed: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(ChannelHistoryModalContentConnector);
