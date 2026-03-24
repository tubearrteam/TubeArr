import React, { useCallback } from 'react';
import { useDispatch } from 'react-redux';
import Icon from 'Components/Icon';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import VirtualTableRowCell from 'Components/Table/Cells/TableRowCell';
import { icons } from 'Helpers/Props';
import { ChannelStatus } from 'Channel/Channel';
import { getChannelStatusDetails } from 'Channel/ChannelStatus';
import { toggleChannelMonitored } from 'Store/Actions/channelActions';
import translate from 'Utilities/String/translate';
import styles from './ChannelStatusCell.css';

interface ChannelStatusCellProps {
  className: string;
  channelId: number;
  monitored: boolean;
  status: ChannelStatus;
  isSelectMode: boolean;
  isSaving: boolean;
  component?: React.ElementType;
}

function ChannelStatusCell(props: ChannelStatusCellProps) {
  const {
    className,
    channelId: channelId,
    monitored,
    status,
    isSelectMode,
    isSaving,
    component: Component = VirtualTableRowCell,
    ...otherProps
  } = props;

  const statusDetails = getChannelStatusDetails(status);
  const dispatch = useDispatch();

  const onMonitoredPress = useCallback(() => {
    dispatch(toggleChannelMonitored({ channelId: channelId, monitored: !monitored }));
  }, [channelId, monitored, dispatch]);

  return (
    <Component className={className} {...otherProps}>
      {isSelectMode ? (
        <MonitorToggleButton
          className={styles.statusIcon}
          monitored={monitored}
          isSaving={isSaving}
          onPress={onMonitoredPress}
        />
      ) : (
        <Icon
          className={styles.statusIcon}
          name={monitored ? icons.MONITORED : icons.UNMONITORED}
          title={
            monitored
              ? translate('ChannelIsMonitored')
              : translate('ChannelIsUnmonitored')
          }
        />
      )}

      <Icon
        className={styles.statusIcon}
        name={statusDetails.icon}
        title={`${statusDetails.title}: ${statusDetails.message}`}
      />
    </Component>
  );
}

export default ChannelStatusCell;
