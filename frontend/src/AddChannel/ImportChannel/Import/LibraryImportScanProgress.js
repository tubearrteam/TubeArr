import PropTypes from 'prop-types';
import React, { useEffect, useRef } from 'react';
import translate from 'Utilities/String/translate';
import styles from './LibraryImportScanProgress.css';

function LibraryImportScanProgress(props) {
  const { events, error, connecting } = props;
  const logRef = useRef(null);

  useEffect(() => {
    const el = logRef.current;
    if (el) {
      el.scrollTop = el.scrollHeight;
    }
  }, [events.length]);

  let total = 0;
  let current = 0;
  for (let i = events.length - 1; i >= 0; i--) {
    const e = events[i];
    if (e.phase === 'started' && e.total != null) {
      total = e.total;
      break;
    }
  }
  for (let i = events.length - 1; i >= 0; i--) {
    const e = events[i];
    if (e.phase === 'folder' && e.index != null) {
      current = e.index;
      break;
    }
  }

  const lines = [];
  for (const e of events) {
    if (e.phase === 'started') {
      lines.push({
        key: `s-${lines.length}`,
        text: translate('LibraryImportScanProgressStarted', { total: e.total ?? 0 }),
        kind: 'meta'
      });
    } else if (e.phase === 'folder') {
      lines.push({
        key: `f-${e.index}-${e.folderName}`,
        text: translate('LibraryImportScanProgressResolving', {
          index: e.index,
          total: e.total,
          name: e.folderName
        }),
        kind: 'working'
      });
    } else if (e.phase === 'folderResult') {
      const ok = e.resolveSuccess === true;
      lines.push({
        key: `r-${e.index}-${e.folderName}`,
        text: ok
          ? translate('LibraryImportScanProgressResolved', {
              index: e.index,
              total: e.total,
              name: e.folderName,
              title: e.channelTitle || ''
            })
          : translate('LibraryImportScanProgressUnresolved', {
              index: e.index,
              total: e.total,
              name: e.folderName,
              detail: e.message || ''
            }),
        kind: ok ? 'ok' : 'bad'
      });
    } else if (e.phase === 'error' && e.message) {
      lines.push({
        key: `e-${lines.length}`,
        text: translate('LibraryImportScanProgressErrorLine', { message: e.message }),
        kind: 'bad'
      });
    }
  }

  return (
    <div className={styles.panel}>
      {connecting && events.length === 0 ? (
        <div className={styles.connecting}>
          {translate('LibraryImportScanProgressConnecting')}
        </div>
      ) : null}

      {total > 0 ? (
        <div className={styles.progressWrap}>
          <progress
            className={styles.progress}
            value={current}
            max={total}
          />
          <span className={styles.progressLabel}>
            {translate('LibraryImportScanProgressCount', { current, total })}
          </span>
        </div>
      ) : null}

      <div className={styles.log} ref={logRef} aria-live="polite">
        {lines.map((line) => (
          <div
            key={line.key}
            className={styles.logLine}
            data-kind={line.kind}
          >
            {line.text}
          </div>
        ))}
      </div>

      {error ? (
        <div className={styles.streamError}>
          {translate('LibraryImportScanStreamFailed')}
        </div>
      ) : null}
    </div>
  );
}

LibraryImportScanProgress.propTypes = {
  events: PropTypes.arrayOf(PropTypes.object).isRequired,
  error: PropTypes.bool,
  connecting: PropTypes.bool
};

LibraryImportScanProgress.defaultProps = {
  error: false,
  connecting: false
};

export default LibraryImportScanProgress;
