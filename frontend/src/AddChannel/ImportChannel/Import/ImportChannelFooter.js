import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import CheckInput from 'Components/Form/CheckInput';
import FormInputGroup from 'Components/Form/FormInputGroup';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import SpinnerButton from 'Components/Link/SpinnerButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContentFooter from 'Components/Page/PageContentFooter';
import Popover from 'Components/Tooltip/Popover';
import { icons, inputTypes, kinds, tooltipPositions } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import styles from './ImportChannelFooter.css';

const MIXED = 'mixed';

class ImportChannelFooter extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    const {
      defaultMonitor,
      defaultQualityProfileId,
      defaultPlaylistFolder,
      defaultChannelType
    } = props;

    this.state = {
      monitor: defaultMonitor,
      qualityProfileId: defaultQualityProfileId,
      channelType: defaultChannelType,
      playlistFolder: defaultPlaylistFolder
    };
  }

  componentDidUpdate(prevProps, prevState) {
    const {
      defaultMonitor,
      defaultQualityProfileId,
      defaultChannelType,
      defaultPlaylistFolder,
      isMonitorMixed,
      isQualityProfileIdMixed,
      isChannelTypeMixed,
      isPlaylistFolderMixed
    } = this.props;

    const {
      monitor,
      qualityProfileId,
      channelType,
      playlistFolder
    } = this.state;

    const newState = {};

    if (isMonitorMixed && monitor !== MIXED) {
      newState.monitor = MIXED;
    } else if (!isMonitorMixed && monitor !== defaultMonitor) {
      newState.monitor = defaultMonitor;
    }

    if (isQualityProfileIdMixed && qualityProfileId !== MIXED) {
      newState.qualityProfileId = MIXED;
    } else if (!isQualityProfileIdMixed && qualityProfileId !== defaultQualityProfileId) {
      newState.qualityProfileId = defaultQualityProfileId;
    }

    if (isChannelTypeMixed && channelType !== MIXED) {
      newState.channelType = MIXED;
    } else if (!isChannelTypeMixed && channelType !== defaultChannelType) {
      newState.channelType = defaultChannelType;
    }

    if (isPlaylistFolderMixed && playlistFolder != null) {
      newState.playlistFolder = null;
    } else if (!isPlaylistFolderMixed && playlistFolder !== defaultPlaylistFolder) {
      newState.playlistFolder = defaultPlaylistFolder;
    }

    if (!_.isEmpty(newState)) {
      this.setState(newState);
    }
  }

  //
  // Listeners

  onInputChange = ({ name, value }) => {
    this.setState({ [name]: value });
    this.props.onInputChange({ name, value });
  };

  //
  // Render

  render() {
    const {
      selectedCount,
      isImporting,
      isLookingUpChannel,
      isMonitorMixed,
      isQualityProfileIdMixed,
      isChannelTypeMixed,
      hasUnsearchedItems,
      importError,
      onImportPress,
      onLookupPress,
      onCancelLookupPress
    } = this.props;

    const {
      monitor,
      qualityProfileId,
      channelType,
      playlistFolder
    } = this.state;

    return (
      <PageContentFooter>
        <div className={styles.inputContainer}>
          <div className={styles.label}>
            {translate('Monitor')}
          </div>

          <FormInputGroup
            type={inputTypes.MONITOR_VIDEOS_SELECT}
            name="monitor"
            value={monitor}
            isDisabled={!selectedCount}
            includeMixed={isMonitorMixed}
            onChange={this.onInputChange}
          />
        </div>

        <div className={styles.inputContainer}>
          <div className={styles.label}>
            {translate('QualityProfile')}
          </div>

          <FormInputGroup
            type={inputTypes.QUALITY_PROFILE_SELECT}
            name="qualityProfileId"
            value={qualityProfileId}
            isDisabled={!selectedCount}
            includeMixed={isQualityProfileIdMixed}
            onChange={this.onInputChange}
          />
        </div>

        <div className={styles.inputContainer}>
          <div className={styles.label}>
            {translate('ChannelType')}
          </div>

          <FormInputGroup
            type={inputTypes.CHANNEL_TYPE_SELECT}
            name="channelType"
            value={channelType}
            isDisabled={!selectedCount}
            includeMixed={isChannelTypeMixed}
            onChange={this.onInputChange}
          />
        </div>

        <div className={styles.inputContainer}>
          <div className={styles.label}>
            {translate('PlaylistFolder')}
          </div>

          <CheckInput
            name="playlistFolder"
            value={playlistFolder}
            isDisabled={!selectedCount}
            onChange={this.onInputChange}
          />
        </div>

        <div>
          <div className={styles.label}>
            &nbsp;
          </div>

          <div className={styles.importButtonContainer}>
            <SpinnerButton
              className={styles.importButton}
              kind={kinds.PRIMARY}
              isSpinning={isImporting}
              isDisabled={!selectedCount || isLookingUpChannel}
              onPress={onImportPress}
            >
              {translate('ImportCountChannel', { selectedCount })}
            </SpinnerButton>

            {
              isLookingUpChannel ?
                <Button
                  className={styles.loadingButton}
                  kind={kinds.WARNING}
                  onPress={onCancelLookupPress}
                >
                  {translate('CancelProcessing')}
                </Button> :
                null
            }

            {
              hasUnsearchedItems ?
                <Button
                  className={styles.loadingButton}
                  kind={kinds.SUCCESS}
                  onPress={onLookupPress}
                >
                  {translate('StartProcessing')}
                </Button> :
                null
            }

            {
              isLookingUpChannel ?
                <LoadingIndicator
                  className={styles.loading}
                  size={24}
                /> :
                null
            }

            {
              isLookingUpChannel ?
                translate('ProcessingFolders') :
                null
            }

            {
              importError ?
                <Popover
                  anchor={
                    <Icon
                      className={styles.importError}
                      name={icons.WARNING}
                      kind={kinds.WARNING}
                    />
                  }
                  title={translate('ImportErrors')}
                  body={
                    <ul>
                      {
                        Array.isArray(importError.responseJSON) ?
                          importError.responseJSON.map((error, index) => {
                            return (
                              <li key={index}>
                                {error.errorMessage}
                              </li>
                            );
                          }) :
                          <li>
                            {
                              JSON.stringify(importError.responseJSON)
                            }
                          </li>
                      }
                    </ul>
                  }
                  position={tooltipPositions.RIGHT}
                /> :
                null
            }
          </div>
        </div>
      </PageContentFooter>
    );
  }
}

ImportChannelFooter.propTypes = {
  selectedCount: PropTypes.number.isRequired,
  isImporting: PropTypes.bool.isRequired,
  isLookingUpChannel: PropTypes.bool.isRequired,
  defaultMonitor: PropTypes.string.isRequired,
  defaultQualityProfileId: PropTypes.number,
  defaultChannelType: PropTypes.string.isRequired,
  defaultPlaylistFolder: PropTypes.bool.isRequired,
  isMonitorMixed: PropTypes.bool.isRequired,
  isQualityProfileIdMixed: PropTypes.bool.isRequired,
  isChannelTypeMixed: PropTypes.bool.isRequired,
  isPlaylistFolderMixed: PropTypes.bool.isRequired,
  hasUnsearchedItems: PropTypes.bool.isRequired,
  importError: PropTypes.object,
  onInputChange: PropTypes.func.isRequired,
  onImportPress: PropTypes.func.isRequired,
  onLookupPress: PropTypes.func.isRequired,
  onCancelLookupPress: PropTypes.func.isRequired
};

export default ImportChannelFooter;
