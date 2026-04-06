/**
 * File-operations queue UI: rename files, rename channel layout, map unmapped (active queue only).
 */
export function isActiveFileOpsQueueItem(item) {
  if (!item) {
    return false;
  }
  if (item.body?.fileOpsProgress == null) {
    return false;
  }
  const s = item.status;
  return s === 'queued' || s === 'started';
}
