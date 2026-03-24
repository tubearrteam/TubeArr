import React from 'react';
import Icon from 'Components/Icon';
import { icons, kinds } from 'Helpers/Props';
import { ChannelType } from 'Channel/Channel';
import padNumber from 'Utilities/Number/padNumber';
import translate from 'Utilities/String/translate';
import styles from './VideoNumber.css';

function getWarningMessage(
  channelType: ChannelType | undefined,
  absoluteVideoNumber: number | undefined
) {
  const messages = [];

  if (channelType === 'episodic' && !absoluteVideoNumber) {
    messages.push(translate('VideoMissingAbsoluteNumber'));
  }

  return messages.join('\n');
}

export interface VideoNumberProps {
  playlistNumber: number;
  videoNumber: number;
  absoluteVideoNumber?: number;
  channelType?: ChannelType;
  showPlaylistNumber?: boolean;
}

function VideoNumber(props: VideoNumberProps) {
  const {
    playlistNumber,
    videoNumber,
    absoluteVideoNumber,
    channelType,
    showPlaylistNumber = false,
  } = props;

  const warningMessage = getWarningMessage(channelType, absoluteVideoNumber);

  return (
    <span>
      <span>
        {showPlaylistNumber && playlistNumber != null && <>{playlistNumber}x</>}

        {showPlaylistNumber ? padNumber(videoNumber, 2) : videoNumber}

        {channelType === 'episodic' && !!absoluteVideoNumber && (
          <span className={styles.absoluteVideoNumber}>
            ({absoluteVideoNumber})
          </span>
        )}
      </span>

      {warningMessage ? (
        <Icon
          className={styles.warning}
          name={icons.WARNING}
          kind={kinds.WARNING}
          title={warningMessage}
        />
      ) : null}
    </span>
  );
}

export default VideoNumber;
