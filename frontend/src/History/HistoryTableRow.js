import _ from 'lodash';
import PropTypes from 'prop-types';
import React from 'react';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import TableRow from 'Components/Table/TableRow';
import VideoFormats from 'Video/VideoFormats';
import VideoQuality from 'Video/VideoQuality';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import RelativeDateCell from 'Components/Table/Cells/RelativeDateCell';
import Tooltip from 'Components/Tooltip/Tooltip';
import { icons, tooltipPositions } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import styles from './HistoryTableRow.css';

function getCellValue(item, columnName) {
  const v = _.get(item, columnName);
  if (v == null) return '-';
  if (typeof v === 'object') return typeof v === 'object' && v !== null && (v.title || v.sortTitle) ? (v.title || v.sortTitle) : '-';
  return String(v);
}

function getEventInfo(eventType) {
  switch (String(eventType)) {
    case '1':
      return { icon: icons.QUEUED, label: translate('Grabbed') };
    case '3':
      return { icon: icons.DOWNLOADED, label: translate('Imported') };
    case '4':
      return { icon: icons.WARNING, label: translate('Failed') };
    case '5':
      return { icon: icons.DELETE, label: translate('Deleted') };
    case '6':
      return { icon: icons.EDIT, label: translate('Renamed') };
    case '7':
      return { icon: icons.IGNORE, label: translate('Ignored') };
    default:
      return { icon: icons.INFO, label: translate('History') };
  }
}

function HistoryTableRow(props) {
  const { columns, date, ...item } = props;
  const eventInfo = getEventInfo(item.eventType);

  return (
    <TableRow>
      {columns.filter((c) => c && c.isVisible !== false).map((column) => {
        if (column.name === 'eventType') {
          return (
            <TableRowCell key={column.name} className={styles.eventTypeCell}>
              <Icon
                name={eventInfo.icon}
                title={eventInfo.label}
              />
            </TableRowCell>
          );
        }

        if (column.name === 'channel.sortTitle') {
          return (
            <TableRowCell key={column.name} className={styles.channelCell}>
              {getCellValue({ ...item, date }, column.name)}
            </TableRowCell>
          );
        }

        if (column.name === 'videos.title') {
          return (
            <TableRowCell key={column.name} className={styles.videoCell}>
              {getCellValue({ ...item, date }, column.name)}
            </TableRowCell>
          );
        }

        if (column.name === 'quality') {
          const q = item.quality;
          return (
            <TableRowCell key={column.name} className={styles.badgeCell}>
              {q && typeof q === 'object' && q.quality ? (
                <VideoQuality quality={q} isCutoffNotMet={false} />
              ) : (
                <Label>{getCellValue({ ...item, date }, column.name)}</Label>
              )}
            </TableRowCell>
          );
        }

        if (column.name === 'customFormats') {
          const formats = item.customFormats;
          return (
            <TableRowCell key={column.name} className={styles.badgeCell}>
              {Array.isArray(formats) ? (
                <VideoFormats formats={formats} />
              ) : (
                <Label>{getCellValue({ ...item, date }, column.name)}</Label>
              )}
            </TableRowCell>
          );
        }

        if (column.name === 'date') {
          return (
            <TableRowCell key={column.name} className={styles.dateCell}>
              <RelativeDateCell date={date} />
            </TableRowCell>
          );
        }

        if (column.name === 'details') {
          const details = getCellValue({ ...item, date }, column.name);
          return (
            <TableRowCell key={column.name} className={styles.detailsCell}>
              <Tooltip
                anchor={<Icon name={icons.INFO} />}
                tooltip={<span>{details}</span>}
                position={tooltipPositions.LEFT}
              />
            </TableRowCell>
          );
        }

        return (
          <TableRowCell key={column.name}>
            {getCellValue({ ...item, date }, column.name)}
          </TableRowCell>
        );
      })}
    </TableRow>
  );
}

HistoryTableRow.propTypes = {
  columns: PropTypes.arrayOf(PropTypes.object).isRequired,
  date: PropTypes.string
};

export default HistoryTableRow;
