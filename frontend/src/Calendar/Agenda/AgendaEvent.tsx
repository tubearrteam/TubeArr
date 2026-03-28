import classNames from 'classnames';
import moment from 'moment';
import React, { useCallback, useState } from 'react';
import { useSelector } from 'react-redux';
import AppState from 'App/State/AppState';
import CalendarEventQueueDetails from 'Calendar/Events/CalendarEventQueueDetails';
import getStatusStyle from 'Calendar/getStatusStyle';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import VideoDetailsModal from 'Video/VideoDetailsModal';
import videoEntities from 'Video/videoEntities';
import getFinaleTypeName from 'Video/getFinaleTypeName';
import useVideoFile from 'VideoFile/useVideoFile';
import { icons, kinds } from 'Helpers/Props';
import useChannel from 'Channel/useChannel';
import { createQueueItemSelectorForHook } from 'Store/Selectors/createQueueItemSelector';
import createUISettingsSelector from 'Store/Selectors/createUISettingsSelector';
import formatTime from 'Utilities/Date/formatTime';
import translate from 'Utilities/String/translate';
import styles from './AgendaEvent.css';

interface AgendaEventProps {
  id: number;
  channelId: number;
  videoFileId: number;
  title: string;
  playlistNumber: number;
  videoNumber: number;
  absoluteVideoNumber?: number;
  airDateUtc: string;
  monitored: boolean;
  finaleType?: string;
  hasFile: boolean;
  grabbed?: boolean;
  showDate: boolean;
}

function AgendaEvent(props: AgendaEventProps) {
  const {
    id,
    channelId,
    videoFileId,
    title,
    playlistNumber,
    videoNumber,
    absoluteVideoNumber,
    airDateUtc,
    monitored,
    finaleType,
    hasFile,
    grabbed,
    showDate,
  } = props;

  const channel = useChannel(channelId)!;
  const videoFile = useVideoFile(videoFileId);
  const queueItem = useSelector(createQueueItemSelectorForHook(id));
  const { timeFormat, longDateFormat, enableColorImpairedMode } = useSelector(
    createUISettingsSelector()
  );

  const {
    showVideoInformation,
    showFinaleIcon,
    showSpecialIcon,
    showCutoffUnmetIcon,
  } = useSelector((state: AppState) => state.calendar.options);

  const [isDetailsModalOpen, setIsDetailsModalOpen] = useState(false);

  const startTime = moment(airDateUtc);
  const endTime = moment(airDateUtc).add(channel.runtime, 'minutes');
  const downloading = !!(queueItem || grabbed);
  const isMonitored = channel.monitored && monitored;
  const statusStyle = getStatusStyle(
    hasFile,
    downloading,
    startTime,
    endTime,
    isMonitored
  );
  const missingAbsoluteNumber =
    channel.channelType === 'episodic' && playlistNumber > 0 && !absoluteVideoNumber;

  const handlePress = useCallback(() => {
    setIsDetailsModalOpen(true);
  }, []);

  const handleDetailsModalClose = useCallback(() => {
    setIsDetailsModalOpen(false);
  }, []);

  return (
    <div className={styles.event}>
      <Link className={styles.underlay} onPress={handlePress} />

      <div className={styles.overlay}>
        <div className={styles.date}>
          {showDate && startTime.format(longDateFormat)}
        </div>

        <div
          className={classNames(
            styles.eventWrapper,
            styles[statusStyle],
            enableColorImpairedMode && 'colorImpaired'
          )}
        >
          <div className={styles.time}>
            {formatTime(airDateUtc, timeFormat)} -{' '}
            {formatTime(endTime.toISOString(), timeFormat, {
              includeMinuteZero: true,
            })}
          </div>

          <div className={styles.channelTitle}>{channel.title}</div>

          <div className={styles.videoTitle}>
            {showVideoInformation ? title : null}
          </div>

          {missingAbsoluteNumber ? (
            <Icon
              className={styles.statusIcon}
              name={icons.WARNING}
              title={translate('VideoMissingAbsoluteNumber')}
            />
          ) : null}

          {queueItem ? (
            <span className={styles.statusIcon}>
              <CalendarEventQueueDetails
                playlistNumber={playlistNumber}
                {...queueItem}
              />
            </span>
          ) : null}

          {!queueItem && grabbed ? (
            <Icon
              className={styles.statusIcon}
              name={icons.DOWNLOADING}
              title={translate('VideoIsDownloading')}
            />
          ) : null}

          {showCutoffUnmetIcon &&
          videoFile &&
          videoFile.qualityCutoffNotMet ? (
            <Icon
              className={styles.statusIcon}
              name={icons.VIDEO_FILE}
              kind={kinds.WARNING}
              title={translate('QualityCutoffNotMet')}
            />
          ) : null}

          {videoNumber === 1 && playlistNumber > 0 && (
            <Icon
              className={styles.statusIcon}
              name={icons.INFO}
              kind={kinds.INFO}
              title={
                playlistNumber === 1
                  ? translate('ChannelPremiere')
                  : translate('PlaylistPremiere')
              }
            />
          )}

          {showFinaleIcon && finaleType ? (
            <Icon
              className={styles.statusIcon}
              name={icons.INFO}
              kind={kinds.WARNING}
              title={getFinaleTypeName(finaleType)}
            />
          ) : null}

          {showSpecialIcon && (videoNumber === 0 || playlistNumber === 0) ? (
            <Icon
              className={styles.statusIcon}
              name={icons.INFO}
              kind={kinds.PINK}
              title={translate('Special')}
            />
          ) : null}
        </div>
      </div>

      <VideoDetailsModal
        isOpen={isDetailsModalOpen}
        videoId={id}
        videoEntity={videoEntities.CALENDAR}
        channelId={channel.id}
        videoTitle={title}
        showOpenChannelButton={true}
        onModalClose={handleDetailsModalClose}
      />
    </div>
  );
}

export default AgendaEvent;
