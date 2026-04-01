import PropTypes from 'prop-types';
import React, { useContext, useEffect, useRef, useState } from 'react';
import { connect } from 'react-redux';
import { AppHistoryContext } from 'App/AppHistoryContext';
import { toggleAdvancedSettings } from 'Store/Actions/settingsActions';
import SettingsToolbar from './SettingsToolbar';

function mapStateToProps(state) {
  return {
    advancedSettings: state.settings.advancedSettings
  };
}

const mapDispatchToProps = {
  toggleAdvancedSettings
};

function SettingsToolbarConnector(props) {
  const { hasPendingChanges, toggleAdvancedSettings, ...rest } = props;
  const history = useContext(AppHistoryContext);
  const [blockedTx, setBlockedTx] = useState(null);
  const unblockRef = useRef(null);

  useEffect(() => {
    if (!history || typeof history.block !== 'function' || !hasPendingChanges) {
      return undefined;
    }

    const unblock = history.block((tx) => {
      const currentPath = history.location.pathname;
      const nextPath = tx.location.pathname;

      if (currentPath !== nextPath) {
        setBlockedTx(tx);
        return;
      }

      unblock();
      tx.retry();
    });

    unblockRef.current = unblock;

    return () => {
      unblockRef.current = null;
      unblock();
    };
  }, [history, hasPendingChanges]);

  const hasPendingLocation = blockedTx !== null;

  const onConfirmNavigation = () => {
    if (blockedTx) {
      unblockRef.current?.();
      blockedTx.retry();
      setBlockedTx(null);
    }
  };

  const onCancelNavigation = () => {
    setBlockedTx(null);
  };

  return (
    <SettingsToolbar
      {...rest}
      hasPendingChanges={hasPendingChanges}
      hasPendingLocation={hasPendingLocation}
      onAdvancedSettingsPress={toggleAdvancedSettings}
      onConfirmNavigation={onConfirmNavigation}
      onCancelNavigation={onCancelNavigation}
    />
  );
}

SettingsToolbarConnector.propTypes = {
  showSave: PropTypes.bool,
  hasPendingChanges: PropTypes.bool,
  onSavePress: PropTypes.func,
  toggleAdvancedSettings: PropTypes.func.isRequired,
  additionalButtons: PropTypes.node,
  isSaving: PropTypes.bool
};

SettingsToolbarConnector.defaultProps = {
  hasPendingChanges: false
};

export default connect(mapStateToProps, mapDispatchToProps)(SettingsToolbarConnector);
