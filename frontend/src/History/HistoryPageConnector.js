import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import * as historyActions from 'Store/Actions/historyActions';
import HistoryPage from './HistoryPage';

function createMapStateToProps() {
  return createSelector(
    (state) => state.history,
    (history) => {
      if (!history) {
        return {
          isFetching: false,
          isPopulated: false,
          error: null,
          items: [],
          totalRecords: 0,
          totalPages: 0,
          pageSize: 20,
          sortKey: 'date',
          sortDirection: 'desc',
          columns: [],
          selectedFilterKey: 'all',
          filters: []
        };
      }
      return { ...history };
    }
  );
}

const mapDispatchToProps = {
  ...historyActions
};

class HistoryPageConnector extends Component {
  componentDidMount() {
    this.props.gotoHistoryFirstPage();
  }

  onRefreshPress = () => {
    this.props.gotoHistoryFirstPage();
  };

  onFirstPagePress = () => {
    this.props.gotoHistoryFirstPage();
  };

  onPreviousPagePress = () => {
    this.props.gotoHistoryPreviousPage();
  };

  onNextPagePress = () => {
    this.props.gotoHistoryNextPage();
  };

  onLastPagePress = () => {
    this.props.gotoHistoryLastPage();
  };

  onPageSelect = (page) => {
    this.props.gotoHistoryPage({ page });
  };

  onSortPress = (sortKey) => {
    this.props.setHistorySort({ sortKey });
  };

  onFilterSelect = (selectedFilterKey) => {
    this.props.setHistoryFilter({ selectedFilterKey });
  };

  render() {
    return (
      <HistoryPage
        onFirstPagePress={this.onFirstPagePress}
        onPreviousPagePress={this.onPreviousPagePress}
        onNextPagePress={this.onNextPagePress}
        onLastPagePress={this.onLastPagePress}
        onPageSelect={this.onPageSelect}
        onSortPress={this.onSortPress}
        onFilterSelect={this.onFilterSelect}
        onRefreshPress={this.onRefreshPress}
        {...this.props}
      />
    );
  }
}

HistoryPageConnector.propTypes = {
  gotoHistoryFirstPage: PropTypes.func.isRequired,
  gotoHistoryPreviousPage: PropTypes.func.isRequired,
  gotoHistoryNextPage: PropTypes.func.isRequired,
  gotoHistoryLastPage: PropTypes.func.isRequired,
  gotoHistoryPage: PropTypes.func.isRequired,
  setHistorySort: PropTypes.func.isRequired,
  setHistoryFilter: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(HistoryPageConnector);
