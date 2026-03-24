import _ from 'lodash';
import PropTypes from 'prop-types';
import React from 'react';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import IconButton from 'Components/Link/IconButton';
import ProgressBar from 'Components/ProgressBar';
import TableRow from 'Components/Table/TableRow';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import styles from './QueueTableRow.css';

function getStatusInfo(status) {
  if (status === 'Downloading') {
    return {
      icon: icons.DOWNLOADING,
      spinning: true,
      text: translate('Downloading')
    };
  }

  if (status === 'Completed') {
    return {
      icon: icons.CHECK_CIRCLE,
      spinning: false,
      text: translate('Downloaded')
    };
  }

  if (status === 'Failed') {
    return {
      icon: icons.WARNING,
      spinning: false,
      text: translate('Failed')
    };
  }

  return {
    icon: icons.QUEUED,
    spinning: false,
    text: translate('QueueWaiting')
  };
}

function getProgressPercent(item) {
  const status = item.status || item.statusLabel;
  const value = _.get(item, 'progress');

  if (typeof value === 'number') {
    return Math.max(0, Math.min(100, Math.round(value * 100)));
  }

  if (status === 'Completed') {
    return 100;
  }

  return 0;
}

function formatDuration(totalSeconds) {
  if (!Number.isFinite(totalSeconds) || totalSeconds <= 0) {
    return null;
  }

  const seconds = Math.max(0, Math.floor(totalSeconds));
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  const secs = seconds % 60;

  const hh = String(hours).padStart(2, '0');
  const mm = String(minutes).padStart(2, '0');
  const ss = String(secs).padStart(2, '0');
  return `${hh}:${mm}:${ss}`;
}

function estimateTimeLeft(item) {
  const status = item.status || item.statusLabel;
  if (status === 'Queued') return translate('QueueWaiting');
  if (status === 'Failed') return translate('Failed');
  if (status === 'Completed') return '00:00:00';

  const realEtaSeconds = _.get(item, 'estimatedSecondsRemaining');
  if (Number.isFinite(realEtaSeconds) && realEtaSeconds >= 0) {
    return formatDuration(realEtaSeconds) || '-';
  }

  const estimatedCompletionTime = _.get(item, 'estimatedCompletionTime');
  if (Number.isFinite(estimatedCompletionTime) && estimatedCompletionTime >= 0) {
    return formatDuration(estimatedCompletionTime) || '-';
  }

  const progress = _.get(item, 'progress');
  const startedAt = _.get(item, 'startedAt');
  if (typeof progress !== 'number' || progress <= 0 || !startedAt) {
    return '-';
  }

  const started = new Date(startedAt).getTime();
  if (Number.isNaN(started)) {
    return '-';
  }

  const elapsedSeconds = (Date.now() - started) / 1000;
  if (elapsedSeconds <= 0) {
    return '-';
  }

  const totalEstimate = elapsedSeconds / progress;
  const remaining = totalEstimate - elapsedSeconds;
  return formatDuration(remaining) || '-';
}

function getCellValue(item, columnName) {
  const value = _.get(item, columnName);

  if (value == null) return '-';
  if (typeof value === 'object' && value !== null) {
    if (columnName === 'quality' && typeof value.name === 'string') return value.name;
    return (value.title || value.sortTitle) ? (value.title || value.sortTitle) : '-';
  }
  return String(value);
}

function QueueTableRow(props) {
  const { columns, onRemovePress, ...item } = props;
  const status = item.status || item.statusLabel;
  const statusInfo = getStatusInfo(status);
  const progressPercent = getProgressPercent(item);
  const qualityName = getCellValue(item, 'quality');
  const formatValue = getCellValue(item, 'customFormats');
  const videoTitle = getCellValue(item, 'videos.title');
  const channelTitle = getCellValue(item, 'channel.sortTitle');
  const youtubeVideoId = _.get(item, 'video.youtubeVideoId');

  return (
    <TableRow>
      {columns.filter((c) => c && c.isVisible !== false).map((column) => {
        const name = column.name;

        if (name === 'status') {
          return (
            <TableRowCell key={name} className={styles.statusCell}>
              <Icon
                className={styles.statusIcon}
                name={statusInfo.icon}
                title={statusInfo.text}
                isSpinning={statusInfo.spinning}
              />
            </TableRowCell>
          );
        }

        if (name === 'channel.sortTitle' || name === 'channels') {
          return (
            <TableRowCell key={name} className={styles.channelCell}>
              <div className={styles.channelTitle}>{channelTitle}</div>
            </TableRowCell>
          );
        }

        if (name === 'videos.title' || name === 'video') {
          return (
            <TableRowCell key={name} className={styles.videoCell}>
              <div className={styles.videoTitle}>{videoTitle}</div>
              {youtubeVideoId ? (
                <div className={styles.subText}>#{youtubeVideoId}</div>
              ) : null}
              {item.errorMessage ? (
                <div className={styles.errorText}>{item.errorMessage}</div>
              ) : null}
            </TableRowCell>
          );
        }

        if (name === 'quality') {
          return (
            <TableRowCell key={name} className={styles.badgeCell}>
              <Label>{qualityName}</Label>
            </TableRowCell>
          );
        }

        if (name === 'customFormats') {
          return (
            <TableRowCell key={name} className={styles.badgeCell}>
              <Label>{formatValue}</Label>
            </TableRowCell>
          );
        }

        if (name === 'estimatedCompletionTime' || name === 'timeleft') {
          return (
            <TableRowCell key={name} className={styles.timeCell}>
              {estimateTimeLeft(item)}
            </TableRowCell>
          );
        }

        if (name === 'progress') {
          return (
            <TableRowCell key={name} className={styles.progressCell}>
              <ProgressBar
                progress={progressPercent}
                showText={false}
                title={`${progressPercent}%`}
              />
            </TableRowCell>
          );
        }

        if (name === 'actions') {
          return (
            <TableRowCell key={name} className={styles.actionCell}>
              <IconButton
                name={icons.REMOVE}
                title={translate('RemoveFromQueue')}
                onPress={() => onRemovePress(item.id)}
              />
            </TableRowCell>
          );
        }

        return (
          <TableRowCell key={name}>
            {getCellValue(item, name)}
          </TableRowCell>
        );
      })}
    </TableRow>
  );
}

QueueTableRow.propTypes = {
  columns: PropTypes.arrayOf(PropTypes.object).isRequired,
  onRemovePress: PropTypes.func
};

QueueTableRow.defaultProps = {
  onRemovePress: () => {}
};

export default QueueTableRow;
