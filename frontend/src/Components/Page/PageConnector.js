import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { useLocation } from 'react-router-dom';
import { createSelector } from 'reselect';
import { fetchTranslations, saveDimensions, setIsSidebarVisible } from 'Store/Actions/appActions';
import { fetchCustomFilters } from 'Store/Actions/customFilterActions';
import { fetchChannels } from 'Store/Actions/channelActions';
import {
  fetchLanguages,
  fetchQualityProfiles,
  fetchUISettings
} from 'Store/Actions/settingsActions';
import { fetchStatus } from 'Store/Actions/systemActions';
import { fetchTags } from 'Store/Actions/tagActions';
import createDimensionsSelector from 'Store/Selectors/createDimensionsSelector';
import createSystemStatusSelector from 'Store/Selectors/createSystemStatusSelector';
import ErrorPage from './ErrorPage';
import LoadingPage from './LoadingPage';
import Page from './Page';

function testLocalStorage() {
  const key = 'tubearrTest';

  try {
    localStorage.setItem(key, key);
    localStorage.removeItem(key);

    return true;
  } catch (e) {
    return false;
  }
}

const selectAppProps = createSelector(
  (state) => state.app.isSidebarVisible,
  (state) => state.app.version,
  (state) => state.app.isUpdated,
  (state) => state.app.isDisconnected,
  (isSidebarVisible, version, isUpdated, isDisconnected) => {
    return {
      isSidebarVisible,
      version,
      isUpdated,
      isDisconnected
    };
  }
);

const selectIsPopulated = createSelector(
  (state) => state.channels.isPopulated,
  (state) => state.customFilters.isPopulated,
  (state) => state.tags.isPopulated,
  (state) => state.settings.ui.isPopulated,
  (state) => state.settings.qualityProfiles.isPopulated,
  (state) => state.settings.languages.isPopulated,
  (state) => state.system.status.isPopulated,
  (state) => state.app.translations.isPopulated,
  (
    channelsIsPopulated,
    customFiltersIsPopulated,
    tagsIsPopulated,
    uiSettingsIsPopulated,
    qualityProfilesIsPopulated,
    languagesIsPopulated,
    systemStatusIsPopulated,
    translationsIsPopulated
  ) => {
    return (
      channelsIsPopulated &&
      customFiltersIsPopulated &&
      tagsIsPopulated &&
      uiSettingsIsPopulated &&
      qualityProfilesIsPopulated &&
      languagesIsPopulated &&
      systemStatusIsPopulated &&
      translationsIsPopulated
    );
  }
);

const selectErrors = createSelector(
  (state) => state.channels.error,
  (state) => state.customFilters.error,
  (state) => state.tags.error,
  (state) => state.settings.ui.error,
  (state) => state.settings.qualityProfiles.error,
  (state) => state.settings.languages.error,
  (state) => state.system.status.error,
  (state) => state.app.translations.error,
  (
    channelError,
    customFiltersError,
    tagsError,
    uiSettingsError,
    qualityProfilesError,
    languagesError,
    systemStatusError,
    translationsError
  ) => {
    const hasError = !!(
      channelError ||
      customFiltersError ||
      tagsError ||
      uiSettingsError ||
      qualityProfilesError ||
      languagesError ||
      systemStatusError ||
      translationsError
    );

    return {
      hasError,
      channelError,
      customFiltersError,
      tagsError,
      uiSettingsError,
      qualityProfilesError,
      languagesError,
      systemStatusError,
      translationsError
    };
  }
);

function createMapStateToProps() {
  return createSelector(
    (state) => state.settings.ui.item.enableColorImpairedMode,
    selectIsPopulated,
    selectErrors,
    selectAppProps,
    createDimensionsSelector(),
    createSystemStatusSelector(),
    (
      enableColorImpairedMode,
      isPopulated,
      errors,
      app,
      dimensions,
      systemStatus
    ) => {
      return {
        ...app,
        ...errors,
        isPopulated,
        isSmallScreen: dimensions.isSmallScreen,
        authenticationEnabled: systemStatus.authentication !== 'none',
        enableColorImpairedMode
      };
    }
  );
}

function createMapDispatchToProps(dispatch, props) {
  return {
    dispatchFetchChannels() {
      dispatch(fetchChannels());
    },
    dispatchFetchCustomFilters() {
      dispatch(fetchCustomFilters());
    },
    dispatchFetchTags() {
      dispatch(fetchTags());
    },
    dispatchFetchQualityProfiles() {
      dispatch(fetchQualityProfiles());
    },
    dispatchFetchLanguages() {
      dispatch(fetchLanguages());
    },
    dispatchFetchUISettings() {
      dispatch(fetchUISettings());
    },
    dispatchFetchStatus() {
      dispatch(fetchStatus());
    },
    dispatchFetchTranslations() {
      dispatch(fetchTranslations());
    },
    onResize(dimensions) {
      dispatch(saveDimensions(dimensions));
    },
    onSidebarVisibleChange(isSidebarVisible) {
      dispatch(setIsSidebarVisible({ isSidebarVisible }));
    }
  };
}

class PageConnector extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      isLocalStorageSupported: testLocalStorage()
    };
  }

  componentDidMount() {
    if (!this.props.isPopulated) {
      this.props.dispatchFetchChannels();
      this.props.dispatchFetchCustomFilters();
      this.props.dispatchFetchTags();
      this.props.dispatchFetchQualityProfiles();
      this.props.dispatchFetchLanguages();
      this.props.dispatchFetchUISettings();
      this.props.dispatchFetchStatus();
      this.props.dispatchFetchTranslations();
    }
  }

  //
  // Listeners

  onSidebarToggle = () => {
    this.props.onSidebarVisibleChange(!this.props.isSidebarVisible);
  };

  //
  // Render

  render() {
    const {
      isPopulated,
      hasError,
      dispatchFetchChannels,
      dispatchFetchTags,
      dispatchFetchQualityProfiles,
      dispatchFetchLanguages,
      dispatchFetchUISettings,
      dispatchFetchStatus,
      dispatchFetchTranslations,
      ...otherProps
    } = this.props;

    if (hasError || !this.state.isLocalStorageSupported) {
      return (
        <ErrorPage
          {...this.state}
          {...otherProps}
        />
      );
    }

    if (isPopulated) {
      return (
        <Page
          {...otherProps}
          onSidebarToggle={this.onSidebarToggle}
        />
      );
    }

    return (
      <LoadingPage />
    );
  }
}

PageConnector.propTypes = {
  isPopulated: PropTypes.bool.isRequired,
  hasError: PropTypes.bool.isRequired,
  isSidebarVisible: PropTypes.bool.isRequired,
  dispatchFetchChannels: PropTypes.func.isRequired,
  dispatchFetchCustomFilters: PropTypes.func.isRequired,
  dispatchFetchTags: PropTypes.func.isRequired,
  dispatchFetchQualityProfiles: PropTypes.func.isRequired,
  dispatchFetchLanguages: PropTypes.func.isRequired,
  dispatchFetchUISettings: PropTypes.func.isRequired,
  dispatchFetchStatus: PropTypes.func.isRequired,
  dispatchFetchTranslations: PropTypes.func.isRequired,
  onSidebarVisibleChange: PropTypes.func.isRequired
};

const ConnectedPageConnector = connect(
  createMapStateToProps,
  createMapDispatchToProps
)(PageConnector);

function PageConnectorWithLocation(props) {
  const location = useLocation();

  return <ConnectedPageConnector {...props} location={location} />;
}

export default PageConnectorWithLocation;
