import PropTypes from 'prop-types';
import React from 'react';
import FormInputGroup from 'Components/Form/FormInputGroup';
import VirtualTableRowCell from 'Components/Table/Cells/VirtualTableRowCell';
import VirtualTableSelectCell from 'Components/Table/Cells/VirtualTableSelectCell';
import { inputTypes } from 'Helpers/Props';
import ImportChannelSelectChannelConnector from './SelectChannel/ImportChannelSelectChannelConnector';
import styles from './ImportChannelRow.css';

function ImportChannelRow(props) {
  const {
    id,
    relativePath,
    monitor,
    qualityProfileId,
    playlistFolder,
    channelType,
    selectedChannel,
    isExistingChannel,
    isSelected,
    onSelectedChange,
    onInputChange
  } = props;

  return (
    <>
      <VirtualTableSelectCell
        inputClassName={styles.selectInput}
        id={id}
        isSelected={isSelected}
        isDisabled={!selectedChannel || isExistingChannel}
        onSelectedChange={onSelectedChange}
      />

      <VirtualTableRowCell className={styles.folder}>
        {relativePath}
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.monitor}>
        <FormInputGroup
          type={inputTypes.MONITOR_VIDEOS_SELECT}
          name="monitor"
          value={monitor}
          onChange={onInputChange}
        />
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.qualityProfile}>
        <FormInputGroup
          type={inputTypes.QUALITY_PROFILE_SELECT}
          name="qualityProfileId"
          value={qualityProfileId}
          onChange={onInputChange}
        />
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.channelType}>
        <FormInputGroup
          type={inputTypes.CHANNEL_TYPE_SELECT}
          name="channelType"
          value={channelType}
          onChange={onInputChange}
        />
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.playlistFolder}>
        <FormInputGroup
          type={inputTypes.CHECK}
          name="playlistFolder"
          value={playlistFolder}
          onChange={onInputChange}
        />
      </VirtualTableRowCell>

      <VirtualTableRowCell className={styles.channels}>
        <ImportChannelSelectChannelConnector
          id={id}
          isExistingChannel={isExistingChannel}
          onInputChange={onInputChange}
        />
      </VirtualTableRowCell>
    </>
  );
}

ImportChannelRow.propTypes = {
  id: PropTypes.string.isRequired,
  relativePath: PropTypes.string.isRequired,
  monitor: PropTypes.string.isRequired,
  qualityProfileId: PropTypes.number.isRequired,
  channelType: PropTypes.string.isRequired,
  playlistFolder: PropTypes.bool.isRequired,
  selectedChannel: PropTypes.object,
  isExistingChannel: PropTypes.bool.isRequired,
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  isSelected: PropTypes.bool,
  onSelectedChange: PropTypes.func.isRequired,
  onInputChange: PropTypes.func.isRequired
};

ImportChannelRow.defaultsProps = {
  items: []
};

export default ImportChannelRow;
