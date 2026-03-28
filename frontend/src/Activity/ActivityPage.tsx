import React, { useEffect, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import AppState from 'App/State/AppState';
import Link from 'Components/Link/Link';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import { fetchCommands } from 'Store/Actions/commandActions';
import { fetchQueueStatus, gotoQueueFirstPage } from 'Store/Actions/queueActions';
import { gotoHistoryFirstPage } from 'Store/Actions/historyActions';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import { isActiveMetadataQueueItem } from 'Utilities/Command/metadataQueueFilter';
import translate from 'Utilities/String/translate';
import styles from './ActivityPage.css';

export default function ActivityPage() {
  const dispatch = useDispatch();
  const queueStatus = useSelector((state: AppState) => state.queue?.status?.item);
  const queueStatusPopulated = useSelector((state: AppState) => state.queue?.status?.isPopulated);
  const queueStatusFetching = useSelector((state: AppState) => state.queue?.status?.isFetching);
  const queuePagedTotal = useSelector((state: AppState) => state.queue?.paged?.totalRecords ?? 0);
  const commandItems = useSelector((state: AppState) => state.commands?.items ?? []);
  const historyTotalRecords = useSelector((state: AppState) => state.history?.totalRecords ?? 0);
  const [metadataHistoryTotal, setMetadataHistoryTotal] = useState(0);

  useEffect(() => {
    dispatch(fetchQueueStatus());
    dispatch(fetchCommands());
    dispatch(gotoQueueFirstPage());
    dispatch(gotoHistoryFirstPage());
  }, [dispatch]);

  useEffect(() => {
    createAjaxRequest({
      url: '/metadata-history?page=1&pageSize=1',
      method: 'GET',
      dataType: 'json',
    }).request.done((data: { totalRecords?: number }) => {
      setMetadataHistoryTotal(typeof data.totalRecords === 'number' ? data.totalRecords : 0);
    });
  }, []);

  const total =
    queueStatus != null && 'totalCount' in queueStatus && typeof queueStatus.totalCount === 'number'
      ? queueStatus.totalCount
      : queuePagedTotal;
  const count =
    queueStatus != null && 'count' in queueStatus && typeof queueStatus.count === 'number'
      ? queueStatus.count
      : 0;

  const downloadQueueSummary = total > 0
    ? `${translate('Queue')}: ${total}${count > 0 ? ` · ${translate('Downloading')}: ${count}` : ''}`
    : translate('QueueIsEmpty');

  const metadataQueueCount = commandItems.filter(isActiveMetadataQueueItem).length;
  const metadataQueueSummary = metadataQueueCount > 0
    ? `${translate('Queue')}: ${metadataQueueCount}`
    : translate('QueueIsEmpty');

  const historyCombinedTotal = historyTotalRecords + metadataHistoryTotal;
  const historySummary = historyCombinedTotal > 0
    ? translate('TotalRecords', { totalRecords: historyCombinedTotal })
    : translate('NoHistoryFound');

  return (
    <PageContent title={translate('Activity')}>
      <PageContentBody>
        {queueStatusFetching && !queueStatusPopulated && <LoadingIndicator />}
        {(queueStatusPopulated || !queueStatusFetching) && (
          <div>
            <Link
              className={styles.link}
              to="/activity/download-queue"
            >
              {translate('Download')} {translate('Queue')}
            </Link>

            <div className={styles.summary}>
              {downloadQueueSummary}
            </div>

            <Link
              className={styles.link}
              to="/activity/metadata-queue"
            >
              {translate('Metadata')} {translate('Queue')}
            </Link>

            <div className={styles.summary}>
              {metadataQueueSummary}
            </div>

            <Link
              className={styles.link}
              to="/activity/history"
            >
              {translate('History')}
            </Link>

            <div className={styles.summary}>
              {historySummary}
            </div>
          </div>
        )}
      </PageContentBody>
    </PageContent>
  );
}
