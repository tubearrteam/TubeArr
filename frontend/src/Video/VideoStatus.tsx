import React from 'react';
import Icon from 'Components/Icon';
import Video from 'Video/Video';
import useVideo, { VideoEntities } from 'Video/useVideo';
import useVideoFile from 'VideoFile/useVideoFile';
import { icons, kinds } from 'Helpers/Props';
import isBefore from 'Utilities/Date/isBefore';
import translate from 'Utilities/String/translate';
import {
  parseVideoStreamWidth,
  statusLabelFromVideoWidth,
} from 'Utilities/Video/statusLabelFromStreamWidth';
import VideoQuality from './VideoQuality';
import styles from './VideoStatus.css';

interface VideoStatusProps {
  videoId: number;
  videoEntity?: VideoEntities;
  videoFileId: number;
}

function VideoStatus(props: VideoStatusProps) {
  const { videoId, videoEntity = 'videos', videoFileId } = props;

  const {
    airDateUtc,
    monitored,
    grabbed = false,
  } = useVideo(videoId, videoEntity) as Video;


  const videoFile = useVideoFile(videoFileId);

  const hasVideoFile = !!videoFile;
  const hasAired = isBefore(airDateUtc);

  if (grabbed) {
    return (
      <div className={styles.center}>
        <Icon
          name={icons.DOWNLOADING}
          title={translate('VideoIsDownloading')}
        />
      </div>
    );
  }

  if (hasVideoFile) {
    const quality = videoFile.quality;
    const isCutoffNotMet = videoFile.qualityCutoffNotMet;
    const width = parseVideoStreamWidth(videoFile.mediaInfo?.resolution);
    const fromWidth =
      width != null ? statusLabelFromVideoWidth(width) : '';
    const statusLabelOverride = fromWidth || undefined;

    return (
      <div className={styles.center}>
        <VideoQuality
          quality={quality}
          size={videoFile.size}
          isCutoffNotMet={isCutoffNotMet}
          title={translate('VideoDownloaded')}
          statusLabelOverride={statusLabelOverride}
        />
      </div>
    );
  }

  if (!airDateUtc) {
    return (
      <div className={styles.center}>
        <Icon name={icons.TBA} title={translate('Tba')} />
      </div>
    );
  }

  if (!monitored) {
    return (
      <div className={styles.center}>
        <Icon
          name={icons.UNMONITORED}
          kind={kinds.DISABLED}
          title={translate('VideoIsNotMonitored')}
        />
      </div>
    );
  }

  if (hasAired) {
    return (
      <div className={styles.center}>
        <Icon
          name={icons.MISSING}
          title={translate('VideoMissingFromDisk')}
        />
      </div>
    );
  }

  return (
    <div className={styles.center}>
      <Icon name={icons.NOT_AIRED} title={translate('VideoHasNotAired')} />
    </div>
  );
}

export default VideoStatus;
