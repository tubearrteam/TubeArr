import React, { useEffect, useState } from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import TableRow from 'Components/Table/TableRow';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import RelativeDateCell from 'Components/Table/Cells/RelativeDateCell';
import { kinds } from 'Helpers/Props';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import translate from 'Utilities/String/translate';

type MetadataHistoryRow = {
  id: number;
  channelId: number | null;
  channelTitle: string | null;
  name: string;
  jobType: string;
  resultStatus: string;
  resultStatusLabel: string;
  message: string | null;
  queuedAt: string;
  startedAt: string | null;
  endedAt: string | null;
  acquisitionMethods: string[];
};

const columns = [
  { name: 'name', label: () => translate('Name'), isVisible: true },
  { name: 'channel', label: () => translate('Channels'), isVisible: true },
  { name: 'status', label: () => translate('Status'), isVisible: true },
  { name: 'ended', label: () => translate('Ended'), isVisible: true },
];

function formatStatus(row: MetadataHistoryRow) {
  switch (row.resultStatus) {
    case 'completed':
      return translate('Completed');
    case 'failed':
      return translate('Failed');
    case 'aborted':
      return translate('MetadataHistoryCancelled');
    default:
      return row.resultStatus;
  }
}

interface MetadataHistorySectionProps {
  refreshNonce: number;
}

export default function MetadataHistorySection({ refreshNonce }: MetadataHistorySectionProps) {
  const [isFetching, setIsFetching] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const [items, setItems] = useState<MetadataHistoryRow[]>([]);

  useEffect(() => {
    setIsFetching(true);
    setError(null);

    const promise = createAjaxRequest({
      url: '/metadata-history?page=1&pageSize=100&sortDirection=descending',
      method: 'GET',
      dataType: 'json',
    }).request;

    promise
      .done((data) => {
        setItems(data.records ?? []);
      })
      .fail((xhr) => {
        setError(xhr);
        setItems([]);
      })
      .always(() => {
        setIsFetching(false);
      });
  }, [refreshNonce]);

  return (
    <FieldSet legend={translate('MetadataHistorySection')}>
      {isFetching && <LoadingIndicator />}

      {error ? (
        <Alert kind={kinds.DANGER}>
          {translate('HistoryLoadError')}
        </Alert>
      ) : null}

      {!isFetching && !error && !items.length && (
        <Alert kind={kinds.INFO}>
          {translate('NoMetadataHistoryFound')}
        </Alert>
      )}

      {!isFetching && !error && !!items.length && (
        <Table columns={columns}>
          <TableBody>
            {items.map((row) => (
              <TableRow key={row.id}>
                <TableRowCell>
                  {row.name}
                  {row.message ? (
                    <span title={row.message}> — {row.message}</span>
                  ) : null}
                </TableRowCell>
                <TableRowCell>
                  {row.channelTitle ?? (row.channelId != null ? `#${row.channelId}` : '—')}
                </TableRowCell>
                <TableRowCell>{formatStatus(row)}</TableRowCell>
                {row.endedAt ? (
                  <RelativeDateCell component={TableRowCell} date={row.endedAt} />
                ) : (
                  <TableRowCell>—</TableRowCell>
                )}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </FieldSet>
  );
}
