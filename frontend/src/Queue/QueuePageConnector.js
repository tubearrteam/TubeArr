import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { messageTypes } from 'Helpers/Props';
import { showMessage } from 'Store/Actions/appActions';
import * as queueActions from 'Store/Actions/queueActions';
import QueuePage from './QueuePage';

function createMapStateToProps() {
  return createSelector(
    (state) => state.queue.paged,
    (state) => state.queue.status?.item,
    (paged, status) => {
      if (!paged) {
        return {
          isFetching: false,
          isPopulated: false,
          error: null,
          items: [],
          totalRecords: 0,
          totalPages: 0,
          page: 1,
          pageSize: 20,
          sortKey: 'timeleft',
          sortDirection: 'asc',
          columns: [],
          selectedFilterKey: 'all',
          filters: []
        };
      }
      const columns = Array.isArray(paged.columns) ? paged.columns.filter(Boolean) : [];
      return {
        ...paged,
        columns,
        queueStatus: status
      };
    }
  );
}

const mapDispatchToProps = {
  ...queueActions,
  showMessage
};

const QUEUE_POLL_INTERVAL_MS = 15000;

class QueuePageConnector extends Component {
  componentDidMount() {
    this.props.gotoQueueFirstPage();
    this.props.fetchQueueStatus();
    this._pollTimer = setInterval(() => {
      this.props.fetchQueueStatus();
      this.props.fetchQueue();
    }, QUEUE_POLL_INTERVAL_MS);
  }

  componentDidUpdate(prevProps) {
    if (
      this.props.isPopulated &&
      this.props.totalRecords > 0 &&
      !this._startProcessingTriggered
    ) {
      this._startProcessingTriggered = true;
      this.props.startQueueProcessing({
        onError: (message) => {
          this.props.showMessage({ message, type: messageTypes.ERROR });
        }
      });
    }
  }

  componentWillUnmount() {
    if (this._pollTimer) {
      clearInterval(this._pollTimer);
      this._pollTimer = null;
    }
  }

  onRefreshPress = () => {
    this.props.gotoQueueFirstPage();
  };

  onFirstPagePress = () => {
    this.props.gotoQueueFirstPage();
  };

  onPreviousPagePress = () => {
    this.props.gotoQueuePreviousPage();
  };

  onNextPagePress = () => {
    this.props.gotoQueueNextPage();
  };

  onLastPagePress = () => {
    this.props.gotoQueueLastPage();
  };

  onPageSelect = (page) => {
    this.props.gotoQueuePage({ page });
  };

  onQueuePageSizeChange = ({ value }) => {
    const pageSize = parseInt(value, 10);
    if (!Number.isFinite(pageSize)) {
      return;
    }
    this.props.setQueueTableOption({ pageSize });
    this.props.gotoQueueFirstPage();
  };

  onSortPress = (sortKey) => {
    this.props.setQueueSort({ sortKey });
  };

  onFilterSelect = (selectedFilterKey) => {
    this.props.setQueueFilter({ selectedFilterKey });
  };

  onClearQueuePress = () => {
    this.props.clearDownloadQueue();
  };

  onStartDownloadsPress = () => {
    this.props.startQueueProcessing({
      onError: (message) => {
        this.props.showMessage({ message, type: messageTypes.ERROR });
      }
    });
    this.props.fetchQueueStatus();
    this.props.gotoQueueFirstPage();
  };

  onRemoveQueueItemPress = (id) => {
    this.props.removeQueueItem({
      id,
      remove: false,
      blocklist: false,
      skipRedownload: false,
      changeCategory: false
    });
  };

  render() {
    return (
      <QueuePage
        {...this.props}
        onFirstPagePress={this.onFirstPagePress}
        onPreviousPagePress={this.onPreviousPagePress}
        onNextPagePress={this.onNextPagePress}
        onLastPagePress={this.onLastPagePress}
        onPageSelect={this.onPageSelect}
        onSortPress={this.onSortPress}
        onFilterSelect={this.onFilterSelect}
        onRefreshPress={this.onRefreshPress}
        onClearQueuePress={this.onClearQueuePress}
        onStartDownloadsPress={this.onStartDownloadsPress}
        onRemoveQueueItemPress={this.onRemoveQueueItemPress}
        onQueuePageSizeChange={this.onQueuePageSizeChange}
      />
    );
  }
}

QueuePageConnector.propTypes = {
  fetchQueue: PropTypes.func.isRequired,
  setQueueTableOption: PropTypes.func.isRequired,
  gotoQueueFirstPage: PropTypes.func.isRequired,
  gotoQueuePreviousPage: PropTypes.func.isRequired,
  gotoQueueNextPage: PropTypes.func.isRequired,
  gotoQueueLastPage: PropTypes.func.isRequired,
  gotoQueuePage: PropTypes.func.isRequired,
  setQueueSort: PropTypes.func.isRequired,
  setQueueFilter: PropTypes.func.isRequired,
  clearDownloadQueue: PropTypes.func.isRequired,
  startQueueProcessing: PropTypes.func.isRequired,
  removeQueueItem: PropTypes.func.isRequired,
  showMessage: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(QueuePageConnector);
