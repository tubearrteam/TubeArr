import PropTypes from 'prop-types';
import React, { useCallback } from 'react';
import Link from 'Components/Link/Link';
import ImportChannelTitle from './ImportChannelTitle';
import styles from './ImportChannelSearchResult.css';

function ImportChannelSearchResult(props) {
  const {
    youtubeChannelId,
    title,
    year,
    network,
    isExistingChannel,
    onPress
  } = props;

  const onPressCallback = useCallback(() => onPress(youtubeChannelId), [youtubeChannelId, onPress]);

  return (
    <div className={styles.container}>
      <Link
        className={styles.channel}
        onPress={onPressCallback}
        >
        <ImportChannelTitle
          title={title}
          year={year}
          network={network}
          isExistingChannel={isExistingChannel}
        />
      </Link>
    </div>
  );
}

ImportChannelSearchResult.propTypes = {
  youtubeChannelId: PropTypes.string,
  title: PropTypes.string.isRequired,
  year: PropTypes.number.isRequired,
  network: PropTypes.string,
  isExistingChannel: PropTypes.bool.isRequired,
  onPress: PropTypes.func.isRequired
};

export default ImportChannelSearchResult;
