import React from 'react';
import CircularProgressBar from 'Components/CircularProgressBar';

function formatDuration(totalSeconds?: number) {
  if (!Number.isFinite(totalSeconds) || totalSeconds == null || totalSeconds <= 0) {
    return null;
  }

  const seconds = Math.max(0, Math.floor(totalSeconds));
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  const secs = seconds % 60;

  return `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}:${String(secs).padStart(2, '0')}`;
}

interface CalendarEventQueueDetailsProps {
  title: string;
  size: number;
  sizeleft: number;
  estimatedCompletionTime?: string | number;
  estimatedSecondsRemaining?: number;
  status: string;
  trackedDownloadState: string;
  trackedDownloadStatus: string;
  statusMessages?: unknown[];
  errorMessage?: string;
}

function CalendarEventQueueDetails({
  title,
  size,
  sizeleft,
  estimatedCompletionTime,
  estimatedSecondsRemaining,
  status,
  trackedDownloadState,
  trackedDownloadStatus,
  statusMessages,
  errorMessage,
}: CalendarEventQueueDetailsProps) {
  const progress = size ? 100 - (sizeleft / size) * 100 : 0;
  const displayedEta =
    typeof estimatedSecondsRemaining === 'number'
      ? formatDuration(estimatedSecondsRemaining)
      : typeof estimatedCompletionTime === 'number'
        ? formatDuration(estimatedCompletionTime)
        : estimatedCompletionTime;

  return (
    <div>
      <CircularProgressBar
        progress={progress}
        size={20}
        strokeWidth={2}
        strokeColor="#7a43b6"
      />
      <div>{title}</div>
      <div>{status}</div>
      {displayedEta ? <div>{displayedEta}</div> : null}
      {errorMessage ? <div>{errorMessage}</div> : null}
      {trackedDownloadState || trackedDownloadStatus || statusMessages ? null : null}
    </div>
  );
}

export default CalendarEventQueueDetails;
