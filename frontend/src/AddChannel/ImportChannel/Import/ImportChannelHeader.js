import PropTypes from 'prop-types';
import React from 'react';
import ChannelMonitoringOptionsPopoverContent from 'AddChannel/ChannelMonitoringOptionsPopoverContent';
import ChannelTypePopoverContent from 'AddChannel/ChannelTypePopoverContent';
import Icon from 'Components/Icon';
import VirtualTableHeader from 'Components/Table/VirtualTableHeader';
import VirtualTableHeaderCell from 'Components/Table/VirtualTableHeaderCell';
import VirtualTableSelectAllHeaderCell from 'Components/Table/VirtualTableSelectAllHeaderCell';
import Popover from 'Components/Tooltip/Popover';
import { icons, tooltipPositions } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import styles from './ImportChannelHeader.css';

function ImportChannelHeader(props) {
  const {
    allSelected,
    allUnselected,
    onSelectAllChange
  } = props;

  return (
    <VirtualTableHeader>
      <VirtualTableSelectAllHeaderCell
        allSelected={allSelected}
        allUnselected={allUnselected}
        onSelectAllChange={onSelectAllChange}
      />

      <VirtualTableHeaderCell
        className={styles.folder}
        name="folder"
      >
        {translate('Folder')}
      </VirtualTableHeaderCell>

      <VirtualTableHeaderCell
        className={styles.monitor}
        name="monitor"
      >
        {translate('Monitor')}

        <Popover
          anchor={
            <Icon
              className={styles.detailsIcon}
              name={icons.INFO}
            />
          }
          title={translate('MonitoringOptions')}
          body={<ChannelMonitoringOptionsPopoverContent />}
          position={tooltipPositions.RIGHT}
        />
      </VirtualTableHeaderCell>

      <VirtualTableHeaderCell
        className={styles.qualityProfile}
        name="qualityProfileId"
      >
        {translate('QualityProfile')}
      </VirtualTableHeaderCell>

      <VirtualTableHeaderCell
        className={styles.channelType}
        name="channelType"
      >
        {translate('ChannelType')}

        <Popover
          anchor={
            <Icon
              className={styles.detailsIcon}
              name={icons.INFO}
            />
          }
          title={translate('ChannelType')}
          body={<ChannelTypePopoverContent />}
          position={tooltipPositions.RIGHT}
        />
      </VirtualTableHeaderCell>

      <VirtualTableHeaderCell
        className={styles.playlistFolder}
        name="playlistFolder"
      >
        {translate('PlaylistFolder')}
      </VirtualTableHeaderCell>

      <VirtualTableHeaderCell
        className={styles.channels}
        name="channels"
      >
        {translate('Channels')}
      </VirtualTableHeaderCell>
    </VirtualTableHeader>
  );
}

ImportChannelHeader.propTypes = {
  allSelected: PropTypes.bool.isRequired,
  allUnselected: PropTypes.bool.isRequired,
  onSelectAllChange: PropTypes.func.isRequired
};

export default ImportChannelHeader;
