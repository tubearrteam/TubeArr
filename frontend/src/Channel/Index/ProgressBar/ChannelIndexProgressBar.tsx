import React from 'react';
import { useSelector } from 'react-redux';
import ProgressBar from 'Components/ProgressBar';
import { sizes } from 'Helpers/Props';
import createChannelQueueDetailsSelector, {
  ChannelQueueDetails,
} from 'Channel/Index/createChannelQueueDetailsSelector';
import { ChannelStatus } from 'Channel/Channel';
import getProgressBarKind from 'Utilities/Channel/getProgressBarKind';
import translate from 'Utilities/String/translate';
import styles from './ChannelIndexProgressBar.css';

interface ChannelIndexProgressBarProps {
  channelId: number;
  playlistNumber?: number;
  monitored: boolean;
  status: ChannelStatus;
  videoCount: number;
  videoFileCount: number;
  totalVideoCount: number;
  width: number;
  detailedProgressBar: boolean;
  isStandalone: boolean;
}

function ChannelIndexProgressBar(props: ChannelIndexProgressBarProps) {
  const {
    channelId: channelId,
    playlistNumber,
    monitored,
    status,
    videoCount,
    videoFileCount,
    totalVideoCount,
    width,
    detailedProgressBar,
    isStandalone,
  } = props;

  const queueDetails: ChannelQueueDetails = useSelector(
    createChannelQueueDetailsSelector(channelId, playlistNumber)
  );

  const newDownloads = queueDetails.count - queueDetails.videosWithFiles;
  const progress = videoCount ? (videoFileCount / videoCount) * 100 : 100;
  const text = newDownloads
    ? `${videoFileCount} + ${newDownloads} / ${videoCount}`
    : `${videoFileCount} / ${videoCount}`;

  return (
    <ProgressBar
      className={styles.progressBar}
      containerClassName={isStandalone ? undefined : styles.progress}
      progress={progress}
      kind={getProgressBarKind(
        status,
        monitored,
        progress,
        queueDetails.count > 0
      )}
      size={detailedProgressBar ? sizes.MEDIUM : sizes.SMALL}
      showText={detailedProgressBar}
      text={text}
      title={translate('ChannelProgressBarText', {
        videoFileCount,
        videoCount,
        totalVideoCount,
        downloadingCount: queueDetails.count,
      })}
      width={width}
    />
  );
}

export default ChannelIndexProgressBar;
