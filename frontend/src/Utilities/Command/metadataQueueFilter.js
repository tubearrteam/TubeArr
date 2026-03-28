/**
 * Metadata queue UI: only commands that are still in the execution queue (not finished).
 * Completed/failed jobs are listed under Activity → History (metadata section).
 */
export function isActiveMetadataQueueItem(item) {
  if (!item) {
    return false;
  }
  const hasMeta =
    item.body?.metadataProgress != null || item.body?.metadataStep != null;
  if (!hasMeta) {
    return false;
  }
  const s = item.status;
  return s === 'queued' || s === 'started';
}
