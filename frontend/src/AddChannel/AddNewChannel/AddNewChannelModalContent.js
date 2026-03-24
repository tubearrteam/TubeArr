import PropTypes from 'prop-types';
import React, { Component } from 'react';
import ChannelMonitoringOptionsPopoverContent from 'AddChannel/ChannelMonitoringOptionsPopoverContent';
import ChannelTypePopoverContent from 'AddChannel/ChannelTypePopoverContent';
import CheckInput from 'Components/Form/CheckInput';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Icon from 'Components/Icon';
import SpinnerButton from 'Components/Link/SpinnerButton';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import Popover from 'Components/Tooltip/Popover';
import { icons, inputTypes, kinds, tooltipPositions } from 'Helpers/Props';
import * as channelTypes from 'Utilities/Channel/channelTypes';
import translate from 'Utilities/String/translate';
import styles from './AddNewChannelModalContent.css';

class AddNewChannelModalContent extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    const initialChannelType = props.initialChannelType || channelTypes.STANDARD;

    this.state = {
      channelType: initialChannelType === channelTypes.STANDARD ?
        props.channelType.value :
        initialChannelType
    };
  }

  componentDidUpdate(prevProps) {
    if (this.props.channelType.value !== prevProps.channelType.value) {
      this.setState({ channelType: this.props.channelType.value });
    }
  }

  //
  // Listeners

  onQualityProfileIdChange = ({ value }) => {
    this.props.onInputChange({ name: 'qualityProfileId', value: parseInt(value) });
  };

  onAddChannelPress = () => {
    const {
      channelType
    } = this.state;

    this.props.onAddChannelPress(
      channelType
    );
  };

  //
  // Render

  render() {
    const {
      title,
      description,
      thumbnailUrl,
      isAdding,
      rootFolderPath,
      monitor,
      roundRobinLatestVideoCount,
      qualityProfileId,
      channelType,
      playlistFolder,
      searchForMissingVideos,
      searchForCutoffUnmetVideos,
      tags,
      diskFolderName,
      titleSlug,
      isSmallScreen,
      isWindows,
      onModalClose,
      onInputChange,
      ...otherProps
    } = this.props;

    return (
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>
          {title}
        </ModalHeader>

        <ModalBody>
          <div className={styles.container}>
            {
              isSmallScreen ?
                null :
                <div className={styles.poster}>
                  {
                    thumbnailUrl ?
                      <img
                        className={styles.poster}
                        alt={title}
                        src={thumbnailUrl}
                      /> :
                      null
                  }
                </div>
            }

            <div className={styles.info}>
              {
                description ?
                  <div className={styles.overview}>
                    {description}
                  </div> :
                  null
              }

              <Form {...otherProps}>
                <FormGroup>
                  <FormLabel>{translate('RootFolder')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.ROOT_FOLDER_SELECT}
                    name="rootFolderPath"
                    valueOptions={{
                      channelFolder:
                        (diskFolderName && diskFolderName.trim()) ||
                        (titleSlug && titleSlug.trim()) ||
                        undefined,
                      isWindows
                    }}
                    selectedValueOptions={{
                      channelFolder:
                        (diskFolderName && diskFolderName.trim()) ||
                        (titleSlug && titleSlug.trim()) ||
                        undefined,
                      isWindows
                    }}
                    onChange={onInputChange}
                    {...rootFolderPath}
                  />
                </FormGroup>

                <FormGroup>
                  <FormLabel>
                    {translate('Monitor')}

                    <Popover
                      anchor={
                        <Icon
                          className={styles.labelIcon}
                          name={icons.INFO}
                        />
                      }
                      title={translate('MonitoringOptions')}
                      body={<ChannelMonitoringOptionsPopoverContent />}
                      position={tooltipPositions.RIGHT}
                    />
                  </FormLabel>

                  <FormInputGroup
                    type={inputTypes.MONITOR_VIDEOS_SELECT}
                    name="monitor"
                    onChange={onInputChange}
                    {...monitor}
                  />
                </FormGroup>

                {
                  monitor.value === 'roundRobin' ?
                    <FormGroup>
                      <FormLabel>
                        {translate('RoundRobinMonitoringLatestCount')}
                      </FormLabel>

                      <FormInputGroup
                        type={inputTypes.NUMBER}
                        name="roundRobinLatestVideoCount"
                        min={1}
                        onChange={onInputChange}
                        {...roundRobinLatestVideoCount}
                        helpText={translate('RoundRobinMonitoringLatestCountHelp')}
                      />
                    </FormGroup> :
                    null
                }

                <FormGroup>
                  <FormLabel>{translate('QualityProfile')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.QUALITY_PROFILE_SELECT}
                    name="qualityProfileId"
                    onChange={this.onQualityProfileIdChange}
                    {...qualityProfileId}
                  />
                </FormGroup>

                <FormGroup>
                  <FormLabel>
                    {translate('ChannelType')}

                    <Popover
                      anchor={
                        <Icon
                          className={styles.labelIcon}
                          name={icons.INFO}
                        />
                      }
                      title={translate('ChannelTypes')}
                      body={<ChannelTypePopoverContent />}
                      position={tooltipPositions.RIGHT}
                    />
                  </FormLabel>

                  <FormInputGroup
                    type={inputTypes.CHANNEL_TYPE_SELECT}
                    name="channelType"
                    onChange={onInputChange}
                    {...channelType}
                    value={this.state.channelType}
                    helpText={translate('ChannelTypesHelpText')}
                  />
                </FormGroup>

                <FormGroup>
                  <FormLabel>{translate('PlaylistFolder')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.CHECK}
                    name="playlistFolder"
                    onChange={onInputChange}
                    {...playlistFolder}
                  />
                </FormGroup>

                <FormGroup>
                  <FormLabel>{translate('Tags')}</FormLabel>

                  <FormInputGroup
                    type={inputTypes.TAG}
                    name="tags"
                    onChange={onInputChange}
                    {...tags}
                  />
                </FormGroup>
              </Form>
            </div>
          </div>
        </ModalBody>

        <ModalFooter className={styles.modalFooter}>
          <div>
            <label className={styles.searchLabelContainer}>
              <span className={styles.searchLabel}>
                {translate('AddNewChannelSearchForMissingVideos')}
              </span>

              <CheckInput
                containerClassName={styles.searchInputContainer}
                className={styles.searchInput}
                name="searchForMissingVideos"
                onChange={onInputChange}
                {...searchForMissingVideos}
              />
            </label>

            <label className={styles.searchLabelContainer}>
              <span className={styles.searchLabel}>
                {translate('AddNewChannelSearchForCutoffUnmetVideos')}
              </span>

              <CheckInput
                containerClassName={styles.searchInputContainer}
                className={styles.searchInput}
                name="searchForCutoffUnmetVideos"
                onChange={onInputChange}
                {...searchForCutoffUnmetVideos}
              />
            </label>
          </div>

          <SpinnerButton
            className={styles.addButton}
            kind={kinds.SUCCESS}
            isSpinning={isAdding}
            onPress={this.onAddChannelPress}
          >
            {translate('AddChannelWithTitle', { title })}
          </SpinnerButton>
        </ModalFooter>
      </ModalContent>
    );
  }
}

AddNewChannelModalContent.propTypes = {
  title: PropTypes.string.isRequired,
  description: PropTypes.string,
  thumbnailUrl: PropTypes.string,
  initialChannelType: PropTypes.string,
  isAdding: PropTypes.bool.isRequired,
  addError: PropTypes.object,
  rootFolderPath: PropTypes.object,
  monitor: PropTypes.object.isRequired,
  roundRobinLatestVideoCount: PropTypes.object,
  qualityProfileId: PropTypes.object,
  channelType: PropTypes.object.isRequired,
  playlistFolder: PropTypes.object.isRequired,
  searchForMissingVideos: PropTypes.object.isRequired,
  searchForCutoffUnmetVideos: PropTypes.object.isRequired,
  tags: PropTypes.object.isRequired,
  diskFolderName: PropTypes.string,
  titleSlug: PropTypes.string,
  isSmallScreen: PropTypes.bool.isRequired,
  isWindows: PropTypes.bool.isRequired,
  onModalClose: PropTypes.func.isRequired,
  onInputChange: PropTypes.func.isRequired,
  onAddChannelPress: PropTypes.func.isRequired
};

export default AddNewChannelModalContent;
