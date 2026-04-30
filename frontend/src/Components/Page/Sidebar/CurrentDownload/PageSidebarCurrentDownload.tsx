import React from 'react';
import { useSelector } from 'react-redux';
import AppState from 'App/State/AppState';
import Queue from 'typings/Queue';
import styles from './PageSidebarCurrentDownload.css';

function formatBytes(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes < 0) return '';
  const units = ['B', 'KiB', 'MiB', 'GiB', 'TiB', 'PiB'];
  let value = bytes;
  let unitIndex = 0;
  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024;
    unitIndex++;
  }
  const decimals = unitIndex === 0 ? 0 : value >= 10 ? 1 : 2;
  return `${value.toFixed(decimals)}${units[unitIndex]}`;
}

function formatDuration(totalSeconds?: number | null): string | null {
  if (!Number.isFinite(totalSeconds) || (totalSeconds ?? 0) < 0) return null;
  const seconds = Math.max(0, Math.floor(totalSeconds as number));
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  const secs = seconds % 60;
  const hh = String(hours).padStart(2, '0');
  const mm = String(minutes).padStart(2, '0');
  const ss = String(secs).padStart(2, '0');
  return `${hh}:${mm}:${ss}`;
}

function pickActiveDownload(items: Queue[]): Queue | null {
  if (!Array.isArray(items) || items.length === 0) return null;
  const downloading = items.filter((i) => i.status === 'Downloading');
  if (!downloading.length) return null;
  // pick the one with the smallest ETA if available; otherwise first.
  const withEta = downloading
    .filter((i) => typeof i.estimatedSecondsRemaining === 'number' && i.estimatedSecondsRemaining! >= 0)
    .sort((a, b) => (a.estimatedSecondsRemaining! - b.estimatedSecondsRemaining!));
  return withEta[0] ?? downloading[0];
}

export default function PageSidebarCurrentDownload() {
  const queueItems = useSelector((state: AppState) => state.queue?.paged?.items ?? []) as Queue[];
  const item = pickActiveDownload(queueItems);

  if (!item) return null;

  const title =
    (item.video && typeof item.video.title === 'string' && item.video.title) ||
    item.title ||
    'Downloading';

  const downloaded = typeof item.downloadedBytes === 'number' ? item.downloadedBytes : null;
  const total = typeof item.totalBytes === 'number' ? item.totalBytes : null;
  const speed = typeof item.speedBytesPerSecond === 'number' ? item.speedBytesPerSecond : null;
  const eta = formatDuration(item.estimatedSecondsRemaining);
  const pct =
    typeof item.sizeleft === 'number' && typeof item.size === 'number' && item.size > 0
      ? Math.max(0, Math.min(100, Math.round(((item.size - item.sizeleft) / item.size) * 100)))
      : null;

  const parts: string[] = [];
  if (downloaded != null && downloaded >= 0) {
    const dlText = formatBytes(downloaded);
    const totText = total != null && total > 0 ? formatBytes(total) : '';
    parts.push(totText ? `${dlText}/${totText}` : dlText);
  } else if (pct != null) {
    parts.push(`${pct}%`);
  }
  if (speed != null && speed > 0) parts.push(`${formatBytes(speed)}/s`);
  if (eta) parts.push(`ETA ${eta}`);

  const summary = parts.filter(Boolean).join(' · ');

  return (
    <div className={styles.currentDownload}>
      <div className={styles.label}>Downloading</div>
      <div className={styles.title} title={title}>
        {title}
      </div>
      {summary ? <div className={styles.summary}>{summary}</div> : null}
    </div>
  );
}

