import moment from 'moment';

/**
 * Sonarr-style list time: "11:36pm" for today, "Mar 30, 11:36pm" for other days.
 */
export default function formatLogFileListTime(isoDate) {
  if (!isoDate) {
    return '';
  }

  const m = moment(isoDate);
  const timePart = `${m.format('h:mm')}${m.format('a').toLowerCase()}`;

  if (m.isSame(moment(), 'day')) {
    return timePart;
  }

  return `${m.format('MMM D')}, ${timePart}`;
}
