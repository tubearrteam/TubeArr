import React, { useCallback } from 'react';
import { useDispatch } from 'react-redux';
import * as commandNames from 'Commands/commandNames';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import { icons, sizes } from 'Helpers/Props';
import { executeCommand } from 'Store/Actions/commandActions';
import translate from 'Utilities/String/translate';
import styles from './VideoSearch.css';

interface VideoSearchProps {
  videoId: number;
  onModalClose: () => void;
}

function VideoSearch({
  videoId,
  onModalClose,
}: VideoSearchProps) {
  const dispatch = useDispatch();

  const handleQuickSearchPress = useCallback(() => {
    dispatch(
      executeCommand({
        name: commandNames.VIDEO_SEARCH,
        videoIds: [videoId],
      })
    );

    onModalClose();
  }, [videoId, dispatch, onModalClose]);

  return (
    <div>
      <div className={styles.buttonContainer}>
        <Button
          className={styles.button}
          size={sizes.LARGE}
          onPress={handleQuickSearchPress}
        >
          <Icon className={styles.buttonIcon} name={icons.QUICK} />

          {translate('QuickSearch')}
        </Button>
      </div>
    </div>
  );
}

export default VideoSearch;
