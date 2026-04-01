import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import keyboardShortcuts from 'Components/keyboardShortcuts';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import { kinds, sizes } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import { clearPendingChanges, set } from 'Store/Actions/baseActions';
import {
  fetchMediaManagementSettings,
  fetchPlexProviderSettings,
  removeManagedNfosFromLibrary,
  saveMediaManagementSettings,
  saveNamingSettings,
  savePlexProviderSettings,
  setMediaManagementSettingsValue,
  setPlexProviderSettingsValue
} from 'Store/Actions/settingsActions';
import createSettingsSectionSelector from 'Store/Selectors/createSettingsSectionSelector';
import createSystemStatusSelector from 'Store/Selectors/createSystemStatusSelector';
import MediaManagement from './MediaManagement';

const SECTION = 'mediaManagement';
const PLEX_SECTION = 'plexProvider';

function createMapStateToProps() {
  return createSelector(
    (state) => state.settings.advancedSettings,
    (state) => state.settings.naming,
    (state) => state.settings.mediaManagement?.item ?? {},
    createSettingsSectionSelector(SECTION),
    createSettingsSectionSelector(PLEX_SECTION),
    createSystemStatusSelector(),
    (advancedSettings, namingSettings, mediaItem, sectionSettings, plexSettings, systemStatus) => {
      return {
        advancedSettings,
        mediaManagementSavedItem: mediaItem,
        ...sectionSettings,
        plexProvider: plexSettings,
        plexHasPendingChanges: plexSettings.hasPendingChanges,
        hasPendingChanges: !_.isEmpty(namingSettings.pendingChanges) || sectionSettings.hasPendingChanges || plexSettings.hasPendingChanges,
        isWindows: systemStatus.isWindows,
        customNfosSavedOff: mediaItem.useCustomNfos === false
      };
    }
  );
}

function mapDispatchToProps(dispatch) {
  return {
    fetchMediaManagementSettings: (...args) => dispatch(fetchMediaManagementSettings(...args)),
    fetchPlexProviderSettings: (...args) => dispatch(fetchPlexProviderSettings(...args)),
    setMediaManagementSettingsValue: (...args) => dispatch(setMediaManagementSettingsValue(...args)),
    setPlexProviderSettingsValue: (...args) => dispatch(setPlexProviderSettingsValue(...args)),
    saveMediaManagementSettings: (...args) => dispatch(saveMediaManagementSettings(...args)),
    savePlexProviderSettings: (...args) => dispatch(savePlexProviderSettings(...args)),
    saveNamingSettings: (...args) => dispatch(saveNamingSettings(...args)),
    clearPendingChanges: (...args) => dispatch(clearPendingChanges(...args)),
    removeManagedNfosFromLibrary: (...args) => dispatch(removeManagedNfosFromLibrary(...args)),
    clearNfoRemovalFeedback: () => dispatch(set({
      section: 'settings.mediaManagement',
      lastManagedNfoRemoval: null,
      managedNfoRemovalError: null
    }))
  };
}

class MediaManagementConnector extends Component {

  //
  // Lifecycle

  constructor(props) {
    super(props);
    this.state = {
      disableNfoConfirmIsOpen: false,
      removeNfoOnlyIsOpen: false
    };
  }

  componentDidMount() {
    this.props.fetchMediaManagementSettings();
    this.props.fetchPlexProviderSettings();
  }

  componentWillUnmount() {
    this.props.clearPendingChanges({ section: `settings.${SECTION}` });
    this.props.clearPendingChanges({ section: `settings.${PLEX_SECTION}` });
  }

  //
  // Listeners

  onInputChange = ({ name, value }) => {
    if (name && name.startsWith('plexProvider.')) {
      this.props.setPlexProviderSettingsValue({ name: name.substring('plexProvider.'.length), value });
      return;
    }

    this.props.setMediaManagementSettingsValue({ name, value });
  };

  flushSave = (removeManagedNfosAfterSave) => {
    this.props.saveMediaManagementSettings(
      removeManagedNfosAfterSave ? { removeManagedNfosAfterSave: true } : {}
    );
    this.props.savePlexProviderSettings();
    this.props.saveNamingSettings();
  };

  onSavePress = () => {
    const { mediaManagementSavedItem, pendingChanges } = this.props;
    const turningOff =
      mediaManagementSavedItem?.useCustomNfos !== false &&
      Object.prototype.hasOwnProperty.call(pendingChanges, 'useCustomNfos') &&
      pendingChanges.useCustomNfos === false;

    if (turningOff) {
      this.setState({ disableNfoConfirmIsOpen: true });
      return;
    }

    this.flushSave(false);
  };

  onDisableNfoModalConfirm = () => {
    this.setState({ disableNfoConfirmIsOpen: false });
    this.flushSave(true);
  };

  onDisableNfoModalDecline = () => {
    this.setState({ disableNfoConfirmIsOpen: false });
    this.flushSave(false);
  };

  onRemoveManagedNfosPress = () => {
    this.setState({ removeNfoOnlyIsOpen: true });
  };

  onRemoveNfoOnlyConfirm = () => {
    this.setState({ removeNfoOnlyIsOpen: false });
    this.props.removeManagedNfosFromLibrary();
  };

  onRemoveNfoOnlyCancel = () => {
    this.setState({ removeNfoOnlyIsOpen: false });
  };

  //
  // Render

  render() {
    const {
      bindShortcut,
      unbindShortcut,
      clearNfoRemovalFeedback,
      ...mediaProps
    } = this.props;

    return (
      <>
        <ConfirmModal
          isOpen={this.state.disableNfoConfirmIsOpen}
          kind={kinds.WARNING}
          size={sizes.MEDIUM}
          title={translate('DisableCustomNfosRemoveNfosTitle')}
          message={translate('DisableCustomNfosRemoveNfosMessage')}
          confirmLabel={translate('RemoveManagedNfosAndSave')}
          cancelLabel={translate('SaveWithoutRemovingNfos')}
          isSpinning={false}
          onConfirm={this.onDisableNfoModalConfirm}
          onCancel={this.onDisableNfoModalDecline}
          bindShortcut={bindShortcut}
          unbindShortcut={unbindShortcut}
        />

        <ConfirmModal
          isOpen={this.state.removeNfoOnlyIsOpen}
          kind={kinds.DANGER}
          size={sizes.MEDIUM}
          title={translate('RemoveManagedNfosConfirmTitle')}
          message={translate('RemoveManagedNfosConfirmMessage')}
          confirmLabel={translate('RemoveManagedNfosFromLibraryButton')}
          cancelLabel={translate('Cancel')}
          isSpinning={this.props.isRemovingManagedNfos}
          onConfirm={this.onRemoveNfoOnlyConfirm}
          onCancel={this.onRemoveNfoOnlyCancel}
          bindShortcut={bindShortcut}
          unbindShortcut={unbindShortcut}
        />

        <MediaManagement
          onInputChange={this.onInputChange}
          onSavePress={this.onSavePress}
          onRemoveManagedNfosPress={this.onRemoveManagedNfosPress}
          onClearNfoRemovalFeedback={clearNfoRemovalFeedback}
          {...mediaProps}
        />
      </>
    );
  }
}

MediaManagementConnector.propTypes = {
  bindShortcut: PropTypes.func.isRequired,
  unbindShortcut: PropTypes.func.isRequired,
  fetchMediaManagementSettings: PropTypes.func.isRequired,
  fetchPlexProviderSettings: PropTypes.func.isRequired,
  setMediaManagementSettingsValue: PropTypes.func.isRequired,
  setPlexProviderSettingsValue: PropTypes.func.isRequired,
  saveMediaManagementSettings: PropTypes.func.isRequired,
  savePlexProviderSettings: PropTypes.func.isRequired,
  saveNamingSettings: PropTypes.func.isRequired,
  clearPendingChanges: PropTypes.func.isRequired,
  removeManagedNfosFromLibrary: PropTypes.func.isRequired,
  clearNfoRemovalFeedback: PropTypes.func.isRequired,
  pendingChanges: PropTypes.object,
  mediaManagementSavedItem: PropTypes.object,
  isRemovingManagedNfos: PropTypes.bool
};

export default connect(
  createMapStateToProps,
  mapDispatchToProps
)(keyboardShortcuts(MediaManagementConnector));
