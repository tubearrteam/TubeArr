import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { clearPendingChanges } from 'Store/Actions/baseActions';
import {
  fetchFfmpegSettings,
  saveFfmpegSettings,
  setFfmpegSettingsValue,
  setFfmpegDownloadSelection,
  testFfmpeg,
  fetchFfmpegReleases,
  downloadFfmpeg
} from 'Store/Actions/settingsActions';
import createSettingsSectionSelector from 'Store/Selectors/createSettingsSectionSelector';
import FFmpegSettings from './FFmpegSettings';

const SECTION = 'ffmpeg';

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
  dispatchFetchFfmpegSettings: fetchFfmpegSettings,
  dispatchSaveFfmpegSettings: saveFfmpegSettings,
  dispatchSetFfmpegSettingsValue: setFfmpegSettingsValue,
  dispatchSetFfmpegDownloadSelection: setFfmpegDownloadSelection,
  dispatchTestFfmpeg: testFfmpeg,
  dispatchFetchFfmpegReleases: fetchFfmpegReleases,
  dispatchDownloadFfmpeg: downloadFfmpeg,
  dispatchClearPendingChanges: clearPendingChanges
};

class FFmpegSettingsConnector extends Component {

  componentDidMount() {
    this.props.dispatchFetchFfmpegSettings();
  }

  componentWillUnmount() {
    this.props.dispatchClearPendingChanges({ section: `settings.${SECTION}` });
  }

  onInputChange = ({ name, value }) => {
    this.props.dispatchSetFfmpegSettingsValue({ name, value });
  };

  onSavePress = () => {
    this.props.dispatchSaveFfmpegSettings();
  };

  onTestPress = () => {
    this.props.dispatchTestFfmpeg();
  };

  onFetchReleases = () => {
    this.props.dispatchFetchFfmpegReleases();
  };

  onDownloadSelectionChange = (payload) => {
    this.props.dispatchSetFfmpegDownloadSelection(payload);
  };

  onDownloadPress = () => {
    const { selectedAsset, selectedReleaseTag } = this.props;
    if (selectedAsset?.browser_download_url) {
      this.props.dispatchDownloadFfmpeg({
        downloadUrl: selectedAsset.browser_download_url,
        assetName: selectedAsset.name,
        releaseTag: selectedReleaseTag || undefined
      });
    }
  };

  render() {
    return (
      <FFmpegSettings
        onInputChange={this.onInputChange}
        onSavePress={this.onSavePress}
        onTestPress={this.onTestPress}
        onFetchReleases={this.onFetchReleases}
        onDownloadSelectionChange={this.onDownloadSelectionChange}
        onDownloadPress={this.onDownloadPress}
        {...this.props}
      />
    );
  }
}

FFmpegSettingsConnector.propTypes = {
  dispatchFetchFfmpegSettings: PropTypes.func.isRequired,
  dispatchSaveFfmpegSettings: PropTypes.func.isRequired,
  dispatchSetFfmpegSettingsValue: PropTypes.func.isRequired,
  dispatchSetFfmpegDownloadSelection: PropTypes.func.isRequired,
  dispatchTestFfmpeg: PropTypes.func.isRequired,
  dispatchFetchFfmpegReleases: PropTypes.func.isRequired,
  dispatchDownloadFfmpeg: PropTypes.func.isRequired,
  dispatchClearPendingChanges: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(FFmpegSettingsConnector);
