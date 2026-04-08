import React, { useCallback, useEffect, useState } from 'react';
import Button from 'Components/Link/Button';
import FieldSet from 'Components/FieldSet';
import { kinds } from 'Helpers/Props';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import translate from 'Utilities/String/translate';
import styles from './VideoSlskdPanel.css';

interface ScoreSignal {
  code: string;
  weight: number;
  detail?: string;
}

interface SlskdCandidateRow {
  id: string;
  username: string;
  filename: string;
  size: number;
  extension?: string;
  durationSeconds?: number;
  bitrateKbps?: number;
  matchScore: number;
  confidence: string;
  matchedSignals?: ScoreSignal[];
  searchQueryUsed?: string;
}

interface CandidatesResponse {
  queueId?: number;
  phase?: string;
  candidates?: SlskdCandidateRow[];
  chosenId?: string;
  lastError?: string | null;
  fallbackUsed?: boolean;
  primaryFailureSummary?: string;
  message?: string;
}

export interface VideoSlskdPanelProps {
  videoId: number;
}

function formatSignals(s: ScoreSignal[] | undefined): string {
  if (!s?.length) {
    return '';
  }
  return s.map((x) => `${x.code} (${x.weight})${x.detail ? `: ${x.detail}` : ''}`).join('\n');
}

function VideoSlskdPanel(props: VideoSlskdPanelProps) {
  const { videoId } = props;
  const [data, setData] = useState<CandidatesResponse | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  const load = useCallback(() => {
    const { request } = createAjaxRequest({
      url: `/videos/${videoId}/external/slskd/candidates`,
      method: 'GET',
      dataType: 'json',
    });
    request.done((payload: CandidatesResponse) => {
      setData(payload);
    });
    request.fail(() => {
      setData({ message: translate('ErrorLoadingItem') });
    });
  }, [videoId]);

  useEffect(() => {
    load();
    const t = window.setInterval(load, 4000);
    return () => window.clearInterval(t);
  }, [load]);

  const onSearchAgain = useCallback(() => {
    const { request } = createAjaxRequest({
      url: `/videos/${videoId}/external/slskd/search`,
      method: 'POST',
      dataType: 'json',
      data: JSON.stringify({}),
    });
    request.done(() => load());
    request.fail(() => load());
  }, [videoId, load]);

  const onSelect = useCallback(
    (candidateId: string) => {
      setBusyId(candidateId);
      const { request } = createAjaxRequest({
        url: `/videos/${videoId}/external/slskd/select`,
        method: 'POST',
        dataType: 'json',
        data: JSON.stringify({ candidateId }),
      });
      request.always(() => {
        setBusyId(null);
        load();
      });
    },
    [videoId, load]
  );

  const onCancel = useCallback(
    (queueId: number) => {
      const { request } = createAjaxRequest({
        url: `/queue/items/${queueId}/external/cancel`,
        method: 'POST',
        dataType: 'json',
        data: JSON.stringify({}),
      });
      request.always(() => load());
    },
    [load]
  );

  const phase = data?.phase ?? '';
  const candidates = data?.candidates ?? [];
  const hasSession = !!(phase && phase.length > 0) || candidates.length > 0 || (data?.queueId != null && data.queueId > 0);

  return (
    <div className={styles.panel}>
      <FieldSet legend={translate('SlskdVideoPanelTitle')}>
        {data?.message && !hasSession ? (
          <div className={styles.meta}>{data.message}</div>
        ) : null}

        {data?.fallbackUsed && data.primaryFailureSummary ? (
          <div className={styles.meta}>
            {translate('SlskdPriorProviderNote')}: {data.primaryFailureSummary}
          </div>
        ) : null}

        {hasSession ? (
          <>
            <div className={styles.meta}>
              {translate('SlskdVideoPanelPhase')}: {phase || '—'}
              {data?.queueId != null ? ` · #${data.queueId}` : ''}
            </div>
            {data?.lastError ? <div className={styles.meta}>{data.lastError}</div> : null}

            <div className={styles.actions}>
              <Button kind={kinds.DEFAULT} onPress={onSearchAgain}>
                {translate('SlskdVideoPanelRefreshSearch')}
              </Button>
              {data?.queueId != null ? (
                <Button kind={kinds.DANGER} onPress={() => onCancel(data.queueId!)}>
                  {translate('SlskdVideoPanelCancelQueue')}
                </Button>
              ) : null}
            </div>

            {candidates.length > 0 ? (
              <table className={styles.table}>
                <thead>
                  <tr>
                    <th>{translate('Release')}</th>
                    <th>{translate('SlskdVideoPanelScore')}</th>
                    <th>{translate('Size')}</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {candidates.map((c) => {
                    const sig = formatSignals(c.matchedSignals);
                    const canPick =
                      phase === 'candidatesReady' || phase === 'awaitingManualPick';
                    return (
                      <tr key={c.id}>
                        <td>
                          <div>{c.filename}</div>
                          <div className={styles.meta}>
                            {c.username}
                            {sig ? (
                              <span className={styles.signalHint} title={sig}>
                                {' '}
                                · {translate('SlskdVideoPanelSignals')}
                              </span>
                            ) : null}
                          </div>
                        </td>
                        <td>
                          {c.matchScore} ({c.confidence})
                        </td>
                        <td>{c.size != null ? String(c.size) : '—'}</td>
                        <td>
                          {canPick ? (
                            <Button
                              kind={kinds.PRIMARY}
                              isDisabled={busyId === c.id}
                              onPress={() => onSelect(c.id)}
                            >
                              {translate('SlskdVideoPanelSelect')}
                            </Button>
                          ) : null}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            ) : phase === 'pendingSearch' || phase === 'transferring' ? (
              <div className={styles.meta}>{translate('AddNewChannelSearching')}</div>
            ) : (
              <div className={styles.meta}>{translate('NoResultsFound')}</div>
            )}
          </>
        ) : (
          <div className={styles.meta}>{translate('SlskdVideoPanelNoSession')}</div>
        )}
      </FieldSet>
    </div>
  );
}

export default VideoSlskdPanel;
