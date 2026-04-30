import React, { useEffect } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import { resetProgress } from 'Store/Actions/operationProgressActions';
import AppState from 'App/State/AppState';
import styles from './OperationProgressIndicator.css';

const HIDE_DELAY_MS = 800;

export default function OperationProgressIndicator() {
  const dispatch = useDispatch();
  const { total, done, active } = useSelector(
    (state: AppState) => state.operationProgress
  );

  useEffect(() => {
    if (!total) return;
    if (active) return;
    if (done < total) return;

    const t = window.setTimeout(() => {
      dispatch(resetProgress());
    }, HIDE_DELAY_MS);

    return () => window.clearTimeout(t);
  }, [dispatch, total, done, active]);

  if (!total) return null;

  return (
    <div className={styles.indicator} aria-live="polite">
      <LoadingIndicator className={styles.spinner} size={18} />
      <div className={styles.text}>
        {Math.min(done, total)}/{total}
      </div>
    </div>
  );
}

