import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { fetchDelayProfiles, fetchNotifications } from 'Store/Actions/settingsActions';
import { fetchTagDetails, fetchTags } from 'Store/Actions/tagActions';
import createSortedSectionSelector from 'Store/Selectors/createSortedSectionSelector';
import sortByProp from 'Utilities/Array/sortByProp';
import Tags from './Tags';

function createMapStateToProps() {
  return createSelector(
    createSortedSectionSelector('tags', sortByProp('label')),
    (tags) => {
      const isFetching = tags.isFetching || tags.details.isFetching;
      const error = tags.error || tags.details.error;
      const isPopulated = tags.isPopulated && tags.details.isPopulated;

      return {
        ...tags,
        isFetching,
        error,
        isPopulated
      };
    }
  );
}

const mapDispatchToProps = {
  dispatchFetchTags: fetchTags,
  dispatchFetchTagDetails: fetchTagDetails,
  dispatchFetchDelayProfiles: fetchDelayProfiles,
  dispatchFetchNotifications: fetchNotifications
};

class MetadatasConnector extends Component {

  //
  // Lifecycle

  componentDidMount() {
    const {
      dispatchFetchTags,
      dispatchFetchTagDetails,
      dispatchFetchDelayProfiles,
      dispatchFetchNotifications
    } = this.props;

    dispatchFetchTags();
    dispatchFetchTagDetails();
    dispatchFetchDelayProfiles();
    dispatchFetchNotifications();
  }

  //
  // Render

  render() {
    return (
      <Tags
        {...this.props}
      />
    );
  }
}

MetadatasConnector.propTypes = {
  dispatchFetchTags: PropTypes.func.isRequired,
  dispatchFetchTagDetails: PropTypes.func.isRequired,
  dispatchFetchDelayProfiles: PropTypes.func.isRequired,
  dispatchFetchNotifications: PropTypes.func.isRequired,
};

export default connect(createMapStateToProps, mapDispatchToProps)(MetadatasConnector);
