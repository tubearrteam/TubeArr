import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import createRouteMatchShape from 'Helpers/Props/Shapes/createRouteMatchShape';
import { setAddChannelDefault } from 'Store/Actions/addChannelActions';
import { clearImportChannel, importChannel, setImportChannelValue } from 'Store/Actions/importChannelActions';
import { fetchRootFolders } from 'Store/Actions/rootFolderActions';
import ImportChannel from './ImportChannel';

function createMapStateToProps() {
  return createSelector(
    (state, { match }) => match,
    (state) => state.rootFolders,
    (state) => state.addChannel,
    (state) => state.importChannel,
    (state) => state.settings.qualityProfiles,
    (
      match,
      rootFolders,
      addChannel,
      importChannelState,
      qualityProfiles
    ) => {
      const {
        isFetching: rootFoldersFetching,
        isPopulated: rootFoldersPopulated,
        error: rootFoldersError,
        items
      } = rootFolders;

      const rootFolderId = parseInt(match.params.rootFolderId);

      const result = {
        rootFolderId,
        rootFoldersFetching,
        rootFoldersPopulated,
        rootFoldersError,
        qualityProfiles: qualityProfiles.items,
        defaultQualityProfileId: addChannel.defaults.qualityProfileId
      };

      if (items.length) {
        const rootFolder = _.find(items, { id: rootFolderId });

        return {
          ...result,
          ...rootFolder,
          items: importChannelState.items
        };
      }

      return result;
    }
  );
}

const mapDispatchToProps = {
  dispatchSetImportChannelValue: setImportChannelValue,
  dispatchImportChannel: importChannel,
  dispatchClearImportChannel: clearImportChannel,
  dispatchFetchRootFolders: fetchRootFolders,
  dispatchSetAddChannelDefault: setAddChannelDefault
};

class ImportChannelConnector extends Component {

  //
  // Lifecycle

  /** After the first root-folder fetch on this screen finishes, further fetches are rescans (spinner on scan control only). */
  _importPageFetchCompletedOnce = false;

  componentDidMount() {
    const {
      rootFolderId,
      qualityProfiles,
      defaultQualityProfileId,
      dispatchFetchRootFolders,
      dispatchSetAddChannelDefault
    } = this.props;

    dispatchFetchRootFolders({ id: rootFolderId, timeout: false });

    let setDefaults = false;
    const setDefaultPayload = {};

    if (
      !defaultQualityProfileId ||
      !qualityProfiles.some((p) => p.id === defaultQualityProfileId)
    ) {
      setDefaults = true;
      setDefaultPayload.qualityProfileId = qualityProfiles[0].id;
    }

    if (setDefaults) {
      dispatchSetAddChannelDefault(setDefaultPayload);
    }
  }

  componentWillUnmount() {
    this.props.dispatchClearImportChannel();
  }

  componentDidUpdate(prevProps) {
    if (prevProps.rootFoldersFetching && !this.props.rootFoldersFetching) {
      this._importPageFetchCompletedOnce = true;
    }
  }

  //
  // Listeners

  onInputChange = (ids, name, value) => {
    this.props.dispatchSetAddChannelDefault({ [name]: value });

    ids.forEach((id) => {
      this.props.dispatchSetImportChannelValue({
        id,
        [name]: value
      });
    });
  };

  onImportPress = (ids) => {
    const { rootFolderId } = this.props;

    this.props.dispatchImportChannel({ ids, rootFolderId });
  };

  onScanUnmonitoredFoldersPress = () => {
    const { rootFolderId, dispatchFetchRootFolders } = this.props;

    dispatchFetchRootFolders({ id: rootFolderId, timeout: false });
  };

  //
  // Render

  render() {
    const {
      rootFoldersFetching,
      rootFoldersPopulated
    } = this.props;

    const isScanningUnmonitoredFolders =
      this._importPageFetchCompletedOnce &&
      rootFoldersFetching &&
      rootFoldersPopulated;

    return (
      <ImportChannel
        {...this.props}
        onInputChange={this.onInputChange}
        onImportPress={this.onImportPress}
        onScanUnmonitoredFoldersPress={this.onScanUnmonitoredFoldersPress}
        isScanningUnmonitoredFolders={isScanningUnmonitoredFolders}
      />
    );
  }
}

const routeMatchShape = createRouteMatchShape({
  rootFolderId: PropTypes.string.isRequired
});

ImportChannelConnector.propTypes = {
  match: routeMatchShape.isRequired,
  rootFolderId: PropTypes.number.isRequired,
  rootFoldersFetching: PropTypes.bool.isRequired,
  rootFoldersPopulated: PropTypes.bool.isRequired,
  qualityProfiles: PropTypes.arrayOf(PropTypes.object).isRequired,
  defaultQualityProfileId: PropTypes.number.isRequired,
  dispatchSetImportChannelValue: PropTypes.func.isRequired,
  dispatchImportChannel: PropTypes.func.isRequired,
  dispatchClearImportChannel: PropTypes.func.isRequired,
  dispatchFetchRootFolders: PropTypes.func.isRequired,
  dispatchSetAddChannelDefault: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(ImportChannelConnector);
