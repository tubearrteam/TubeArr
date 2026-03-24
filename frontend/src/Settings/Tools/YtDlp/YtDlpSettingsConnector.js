import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { clearPendingChanges } from 'Store/Actions/baseActions';
import {
  fetchYtdlpSettings,
  saveYtdlpSettings,
  setYtdlpSettingsValue,
  setYtdlpDownloadSelection,
  testYtdlp,
  fetchYtdlpReleases,
  downloadYtdlp,
  updateYtdlp
} from 'Store/Actions/settingsActions';
import createSettingsSectionSelector from 'Store/Selectors/createSettingsSectionSelector';
import YtDlpSettings from './YtDlpSettings';

const SECTION = 'ytdlp';

function createMapStateToProps() {
  return createSelector(
    createSettingsSectionSelector(SECTION),
    (sectionSettings) => {
      return {
        ...sectionSettings,
        isTesting: sectionSettings.isTesting ?? false,
        testMessage: sectionSettings.testMessage ?? null,
        testSuccess: sectionSettings.testSuccess ?? null
      };
    }
  );
}

const mapDispatchToProps = {
  dispatchFetchYtdlpSettings: fetchYtdlpSettings,
  dispatchSaveYtdlpSettings: saveYtdlpSettings,
  dispatchSetYtdlpSettingsValue: setYtdlpSettingsValue,
  dispatchSetYtdlpDownloadSelection: setYtdlpDownloadSelection,
  dispatchTestYtdlp: testYtdlp,
  dispatchFetchYtdlpReleases: fetchYtdlpReleases,
  dispatchDownloadYtdlp: downloadYtdlp,
  dispatchUpdateYtdlp: updateYtdlp,
  dispatchClearPendingChanges: clearPendingChanges
};

class YtDlpSettingsConnector extends Component {

  componentDidMount() {
    this.props.dispatchFetchYtdlpSettings();
  }

  componentWillUnmount() {
    this.props.dispatchClearPendingChanges({ section: `settings.${SECTION}` });
  }

  onInputChange = ({ name, value }) => {
    this.props.dispatchSetYtdlpSettingsValue({ name, value });
  };

  onSavePress = () => {
    this.props.dispatchSaveYtdlpSettings();
  };

  onTestPress = () => {
    this.props.dispatchTestYtdlp();
  };

  onFetchReleases = () => {
    this.props.dispatchFetchYtdlpReleases();
  };

  onDownloadSelectionChange = (payload) => {
    this.props.dispatchSetYtdlpDownloadSelection(payload);
  };

  onDownloadPress = () => {
    const { selectedAsset } = this.props;
    if (selectedAsset?.browser_download_url) {
      this.props.dispatchDownloadYtdlp({
        downloadUrl: selectedAsset.browser_download_url,
        assetName: selectedAsset.name
      });
    }
  };

  onUpdatePress = () => {
    this.props.dispatchUpdateYtdlp();
  };

  render() {
    return (
      <YtDlpSettings
        onInputChange={this.onInputChange}
        onSavePress={this.onSavePress}
        onTestPress={this.onTestPress}
        onFetchReleases={this.onFetchReleases}
        onDownloadSelectionChange={this.onDownloadSelectionChange}
        onDownloadPress={this.onDownloadPress}
        onUpdatePress={this.onUpdatePress}
        {...this.props}
      />
    );
  }
}

YtDlpSettingsConnector.propTypes = {
  dispatchFetchYtdlpSettings: PropTypes.func.isRequired,
  dispatchSaveYtdlpSettings: PropTypes.func.isRequired,
  dispatchSetYtdlpSettingsValue: PropTypes.func.isRequired,
  dispatchSetYtdlpDownloadSelection: PropTypes.func.isRequired,
  dispatchTestYtdlp: PropTypes.func.isRequired,
  dispatchFetchYtdlpReleases: PropTypes.func.isRequired,
  dispatchDownloadYtdlp: PropTypes.func.isRequired,
  dispatchUpdateYtdlp: PropTypes.func.isRequired,
  dispatchClearPendingChanges: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(YtDlpSettingsConnector);
