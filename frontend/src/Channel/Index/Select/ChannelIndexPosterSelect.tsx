import React, { SyntheticEvent, useCallback } from 'react';
import { useSelect } from 'App/SelectContext';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import { icons } from 'Helpers/Props';
import styles from './ChannelIndexPosterSelect.css';

interface ChannelIndexPosterSelectProps {
  channelId: number;
}

function ChannelIndexPosterSelect(props: ChannelIndexPosterSelectProps) {
  const { channelId: channelId } = props;
  const [selectState, selectDispatch] = useSelect();
  const isSelected = selectState.selectedState[channelId];

  const onSelectPress = useCallback(
    (event: SyntheticEvent<HTMLElement, PointerEvent>) => {
      const shiftKey = event.nativeEvent.shiftKey;

      selectDispatch({
        type: 'toggleSelected',
        id: channelId,
        isSelected: !isSelected,
        shiftKey,
      });
    },
    [channelId, isSelected, selectDispatch]
  );

  return (
    <Link className={styles.checkButton} onPress={onSelectPress}>
      <span className={styles.checkContainer}>
        <Icon
          className={isSelected ? styles.selected : styles.unselected}
          name={isSelected ? icons.CHECK_CIRCLE : icons.CIRCLE_OUTLINE}
          size={20}
        />
      </span>
    </Link>
  );
}

export default ChannelIndexPosterSelect;
