import React, { useCallback, useEffect, useState } from 'react';
import Alert from 'Components/Alert';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import TableRow from 'Components/Table/TableRow';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import { icons, kinds } from 'Helpers/Props';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import translate from 'Utilities/String/translate';

interface TraceRow {
  utc: string;
  type: number;
  title: string;
  guid: string;
  pathSnippet: string;
  resultCount: number;
  chosenMatchGuid: string | null;
}

const columns = [
  { name: 'utc', label: () => translate('Time') },
  { name: 'type', label: () => 'Type' },
  { name: 'title', label: () => translate('Title') },
  { name: 'guid', label: () => 'GUID' },
  { name: 'pathSnippet', label: () => translate('Path') },
  { name: 'resultCount', label: () => translate('Count') },
  { name: 'chosenMatchGuid', label: () => translate('PlexMatchChosenGuid') }
];

function PlexMatchDebugPage() {
  const [rows, setRows] = useState<TraceRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => {
    setLoading(true);
    setError(null);
    const { request } = createAjaxRequest({
      url: '/debug/plex/match-traces',
      method: 'GET',
      dataType: 'json'
    });
    request.done((data: TraceRow[]) => {
      setRows(Array.isArray(data) ? data : []);
      setLoading(false);
    });
    request.fail((xhr: { statusText?: string }) => {
      setError(xhr.statusText || translate('OrganizeLoadError'));
      setLoading(false);
    });
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <PageContent title={translate('PlexMatchDebug')}>
      <PageToolbar>
        <PageToolbarSection>
          <PageToolbarButton
            label={translate('Refresh')}
            iconName={icons.REFRESH}
            spinningName={icons.REFRESH}
            isSpinning={loading}
            onPress={load}
          />
        </PageToolbarSection>
      </PageToolbar>

      <PageContentBody>
        {error ? (
          <Alert kind={kinds.DANGER}>{error}</Alert>
        ) : null}

        {loading && !rows.length ? <LoadingIndicator /> : null}

        {!loading && !error && !rows.length ? (
          <Alert kind={kinds.INFO}>{translate('PlexMatchDebugEmpty')}</Alert>
        ) : null}

        {!!rows.length ? (
          <Table columns={columns} canModifyColumns={false}>
            <TableBody>
              {rows.map((r, i) => (
                <TableRow key={`${r.utc}-${i}`}>
                  <TableRowCell>{r.utc}</TableRowCell>
                  <TableRowCell>{r.type}</TableRowCell>
                  <TableRowCell>{r.title}</TableRowCell>
                  <TableRowCell>{r.guid}</TableRowCell>
                  <TableRowCell>{r.pathSnippet}</TableRowCell>
                  <TableRowCell>{r.resultCount}</TableRowCell>
                  <TableRowCell>{r.chosenMatchGuid ?? '—'}</TableRowCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        ) : null}

        <div style={{ marginTop: 16 }}>
          <Button onPress={() => window.history.back()}>{translate('Back')}</Button>
        </div>
      </PageContentBody>
    </PageContent>
  );
}

export default PlexMatchDebugPage;
