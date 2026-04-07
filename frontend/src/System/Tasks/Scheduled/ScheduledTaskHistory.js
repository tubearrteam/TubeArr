import moment from 'moment';
import React, { useCallback, useEffect, useState } from 'react';
import { useSelector } from 'react-redux';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import IconButton from 'Components/Link/IconButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import TableRow from 'Components/Table/TableRow';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import { icons, kinds } from 'Helpers/Props';
import createUISettingsSelector from 'Store/Selectors/createUISettingsSelector';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import formatDate from 'Utilities/Date/formatDate';
import formatDateTime from 'Utilities/Date/formatDateTime';
import translate from 'Utilities/String/translate';
import styles from './ScheduledTaskHistory.css';

const columns = [
  { name: 'displayName', label: () => translate('Name'), isVisible: true },
  { name: 'startedAt', label: () => translate('Started'), isVisible: true },
  { name: 'completedAt', label: () => translate('LastExecution'), isVisible: true },
  { name: 'duration', label: () => translate('Duration'), isVisible: true },
  { name: 'resultMessage', label: () => translate('Message'), isVisible: true },
];

function ScheduledTaskHistory() {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const { showRelativeDates, longDateFormat, shortDateFormat, timeFormat } = useSelector(
    createUISettingsSelector()
  );

  const loadHistory = useCallback(() => {
    setLoading(true);
    setError(null);

    const { request } = createAjaxRequest({
      url: '/system/task/history?limit=100',
      method: 'GET',
      dataType: 'json',
    });

    request
      .then((data) => {
        setItems(Array.isArray(data) ? data : []);
        setLoading(false);
      })
      .catch((xhr) => {
        setError(xhr?.statusText || translate('Error'));
        setLoading(false);
      });
  }, []);

  useEffect(() => {
    loadHistory();
  }, [loadHistory]);

  const formatInstant = useCallback(
    (iso) => {
      if (!iso) {
        return '-';
      }
      const m = moment(iso);
      if (!m.isValid()) {
        return String(iso);
      }
      if (showRelativeDates) {
        return m.fromNow();
      }
      return formatDateTime(iso, longDateFormat, timeFormat, { includeSeconds: true }) || formatDate(iso, shortDateFormat);
    },
    [showRelativeDates, longDateFormat, shortDateFormat, timeFormat]
  );

  return (
    <FieldSet
      legend={
        <span className={styles.legendRow}>
          <span>{`${translate('Scheduled')} ${translate('History')}`}</span>
          <IconButton
            className={styles.refresh}
            name={icons.REFRESH}
            title={translate('Refresh')}
            isDisabled={loading}
            onPress={loadHistory}
          />
        </span>
      }
    >
      {loading && <LoadingIndicator />}
      {!loading && error && <Alert kind={kinds.DANGER}>{error}</Alert>}
      {!loading && !error && items.length === 0 && (
        <Alert kind={kinds.INFO}>{translate('NoEventsFound')}</Alert>
      )}
      {!loading && !error && items.length > 0 && (
        <Table columns={columns}>
          <TableBody>
            {items.map((row, idx) => (
              <TableRow key={`${row.taskName}-${row.completedAt}-${idx}`}>
                <TableRowCell>{row.displayName || row.taskName}</TableRowCell>
                <TableRowCell title={row.startedAt ? formatDateTime(row.startedAt, longDateFormat, timeFormat, { includeSeconds: true }) : undefined}>
                  {formatInstant(row.startedAt)}
                </TableRowCell>
                <TableRowCell title={row.completedAt ? formatDateTime(row.completedAt, longDateFormat, timeFormat, { includeSeconds: true }) : undefined}>
                  {formatInstant(row.completedAt)}
                </TableRowCell>
                <TableRowCell>{row.duration}</TableRowCell>
                <TableRowCell>{row.resultMessage ? String(row.resultMessage) : ''}</TableRowCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </FieldSet>
  );
}

export default ScheduledTaskHistory;
