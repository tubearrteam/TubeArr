import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { clearPendingChanges } from 'Store/Actions/baseActions';
import {
  fetchYouTubeSettings,
  saveYouTubeSettings,
  setYouTubeSettingsValue
} from 'Store/Actions/settingsActions';
import createSettingsSectionSelector from 'Store/Selectors/createSettingsSectionSelector';
import YouTubeSettings from './YouTubeSettings';

const SECTION = 'youtube';

function createMapStateToProps() {
  return createSelector(
    createSettingsSectionSelector(SECTION),
    (sectionSettings) => sectionSettings
  );
}

const mapDispatchToProps = {
  dispatchFetchYouTubeSettings: fetchYouTubeSettings,
  dispatchSaveYouTubeSettings: saveYouTubeSettings,
  dispatchSetYouTubeSettingsValue: setYouTubeSettingsValue,
  dispatchClearPendingChanges: clearPendingChanges
};

class YouTubeSettingsConnector extends Component {

  componentDidMount() {
    this.props.dispatchFetchYouTubeSettings();
  }

  componentWillUnmount() {
    this.props.dispatchClearPendingChanges({ section: `settings.${SECTION}` });
  }

  onInputChange = ({ name, value }) => {
    this.props.dispatchSetYouTubeSettingsValue({ name, value });
  };

  onSavePress = () => {
    this.props.dispatchSaveYouTubeSettings();
  };

  render() {
    return (
      <YouTubeSettings
        onInputChange={this.onInputChange}
        onSavePress={this.onSavePress}
        {...this.props}
      />
    );
  }
}

YouTubeSettingsConnector.propTypes = {
  dispatchFetchYouTubeSettings: PropTypes.func.isRequired,
  dispatchSaveYouTubeSettings: PropTypes.func.isRequired,
  dispatchSetYouTubeSettingsValue: PropTypes.func.isRequired,
  dispatchClearPendingChanges: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(YouTubeSettingsConnector);
