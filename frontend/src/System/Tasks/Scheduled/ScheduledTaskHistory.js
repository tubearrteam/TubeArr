import React, { useEffect, useState } from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import TableRow from 'Components/Table/TableRow';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import { kinds } from 'Helpers/Props';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import translate from 'Utilities/String/translate';

const columns = [
  { name: 'displayName', label: () => translate('Name'), isVisible: true },
  { name: 'completedAt', label: () => translate('LastExecution'), isVisible: true },
  { name: 'duration', label: () => translate('Duration'), isVisible: true },
  { name: 'resultMessage', label: () => translate('Message'), isVisible: true },
];

function ScheduledTaskHistory() {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
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

  return (
    <FieldSet legend={`${translate('Scheduled')} ${translate('History')}`}>
      {loading && <LoadingIndicator />}
      {!loading && error && (
        <Alert kind={kinds.DANGER}>{error}</Alert>
      )}
      {!loading && !error && items.length === 0 && (
        <Alert kind={kinds.INFO}>{translate('NoEventsFound')}</Alert>
      )}
      {!loading && !error && items.length > 0 && (
        <Table columns={columns}>
          <TableBody>
            {items.map((row, idx) => (
              <TableRow key={`${row.taskName}-${row.completedAt}-${idx}`}>
                <TableRowCell>{row.displayName || row.taskName}</TableRowCell>
                <TableRowCell>{row.completedAt}</TableRowCell>
                <TableRowCell>{row.duration}</TableRowCell>
                <TableRowCell>
                  {row.resultMessage ? String(row.resultMessage) : ''}
                </TableRowCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </FieldSet>
  );
}

export default ScheduledTaskHistory;
