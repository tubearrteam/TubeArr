import classNames from 'classnames';
import moment from 'moment';
import React, { useCallback, useMemo, useState } from 'react';
import { useSelector } from 'react-redux';
import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';
import getStatusStyle from 'Calendar/getStatusStyle';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import getFinaleTypeName from 'Video/getFinaleTypeName';
import { icons, kinds } from 'Helpers/Props';
import useChannel from 'Channel/useChannel';
import createUISettingsSelector from 'Store/Selectors/createUISettingsSelector';
import { CalendarItem } from 'typings/Calendar';
import formatTime from 'Utilities/Date/formatTime';
import translate from 'Utilities/String/translate';
import CalendarEvent from './CalendarEvent';
import styles from './CalendarEventGroup.css';

function createIsDownloadingSelector(videoIds: number[]) {
  return createSelector(
    (state: AppState) => state.queue.details,
    (details) => {
      return details.items.some((item) => {
        return !!(item.videoId && videoIds.includes(item.videoId));
      });
    }
  );
}

interface CalendarEventGroupProps {
  videoIds: number[];
  channelId: number;
  events: CalendarItem[];
  onEventModalOpenToggle: (isOpen: boolean) => void;
}

function CalendarEventGroup({
  videoIds,
  channelId,
  events,
  onEventModalOpenToggle,
}: CalendarEventGroupProps) {
  const isDownloading = useSelector(createIsDownloadingSelector(videoIds));
  const channel = useChannel(channelId)!;

  const { timeFormat, enableColorImpairedMode } = useSelector(
    createUISettingsSelector()
  );

  const { showVideoInformation, showFinaleIcon, fullColorEvents } =
    useSelector((state: AppState) => state.calendar.options);

  const [isExpanded, setIsExpanded] = useState(false);

  const firstEvent = events[0];
  const lastEvent = events[events.length - 1];
  const airDateUtc = firstEvent.airDateUtc;
  const startTime = moment(airDateUtc);
  const endTime = moment(lastEvent.airDateUtc).add(channel.runtime, 'minutes');
  const playlistNumber = firstEvent.playlistNumber;

  const { allDownloaded, anyQueued, anyMonitored, allAbsoluteVideoNumbers } =
    useMemo(() => {
      let files = 0;
      let queued = 0;
      let monitored = 0;
      let absoluteVideoNumbers = 0;

      events.forEach((event) => {
        if (event.videoFileId) {
          files++;
        }

        if (event.queued) {
          queued++;
        }

        if (channel.monitored && event.monitored) {
          monitored++;
        }

        if (event.absoluteVideoNumber) {
          absoluteVideoNumbers++;
        }
      });

      return {
        allDownloaded: files === events.length,
        anyQueued: queued > 0,
        anyMonitored: monitored > 0,
        allAbsoluteVideoNumbers: absoluteVideoNumbers === events.length,
      };
    }, [channel, events]);

  const anyDownloading = isDownloading || anyQueued;

  const statusStyle = getStatusStyle(
    allDownloaded,
    anyDownloading,
    startTime,
    endTime,
    anyMonitored
  );
  const isMissingAbsoluteNumber =
    channel.channelType === 'episodic' &&
    playlistNumber > 0 &&
    !allAbsoluteVideoNumbers;

  const handleExpandPress = useCallback(() => {
    setIsExpanded((state) => !state);
  }, []);

  if (isExpanded) {
    return (
      <div>
        {events.map((event) => {
          return (
            <CalendarEvent
              key={event.id}
              videoId={event.id}
              {...event}
              onEventModalOpenToggle={onEventModalOpenToggle}
            />
          );
        })}

        <Link
          className={styles.collapseContainer}
          component="div"
          onPress={handleExpandPress}
        >
          <Icon name={icons.COLLAPSE} />
        </Link>
      </div>
    );
  }

  return (
    <div
      className={classNames(
        styles.eventGroup,
        styles[statusStyle],
        enableColorImpairedMode && 'colorImpaired',
        fullColorEvents && 'fullColor'
      )}
    >
      <div className={styles.info}>
        <div className={styles.channelTitle}>{channel.title}</div>

        <div
          className={classNames(
            styles.statusContainer,
            fullColorEvents && 'fullColor'
          )}
        >
          {isMissingAbsoluteNumber ? (
            <Icon
              containerClassName={styles.statusIcon}
              name={icons.WARNING}
              title={translate('VideoMissingAbsoluteNumber')}
            />
          ) : null}

        {anyDownloading ? (
            <Icon
              containerClassName={styles.statusIcon}
              name={icons.DOWNLOADING}
              title={translate('AnVideoIsDownloading')}
            />
          ) : null}

          {firstEvent.videoNumber === 1 && playlistNumber > 0 ? (
            <Icon
              containerClassName={styles.statusIcon}
              name={icons.PREMIERE}
              kind={kinds.INFO}
              title={
                playlistNumber === 1
                  ? translate('ChannelPremiere')
                  : translate('PlaylistPremiere')
              }
            />
          ) : null}

        {showFinaleIcon && lastEvent.finaleType ? (
          <Icon
            containerClassName={styles.statusIcon}
            name={
              lastEvent.finaleType === 'channel'
                ? icons.FINALE_CHANNEL
                : icons.FINALE_PLAYLIST
            }
            kind={
              lastEvent.finaleType === 'channel'
                ? kinds.DANGER
                : kinds.WARNING
            }
            title={getFinaleTypeName(lastEvent.finaleType)}
          />
        ) : null}
        </div>
      </div>

      <div className={styles.airingInfo}>
        <div className={styles.airTime}>
          {formatTime(airDateUtc, timeFormat)} -{' '}
          {formatTime(endTime.toISOString(), timeFormat, {
            includeMinuteZero: true,
          })}
        </div>

        {!showVideoInformation ? (
          <Link
            className={styles.expandContainerInline}
            component="div"
            onPress={handleExpandPress}
          >
            <Icon name={icons.EXPAND} />
          </Link>
        ) : null}
      </div>

      {showVideoInformation ? (
        <Link
          className={styles.expandContainer}
          component="div"
          onPress={handleExpandPress}
        >
          &nbsp;
          <Icon name={icons.EXPAND} />
          &nbsp;
        </Link>
      ) : null}
    </div>
  );
}

export default CalendarEventGroup;
