/**
 * Database-operations queue UI: NFO sync, library thumbnail repair (active queue only).
 */
export function isActiveDbOpsQueueItem(item) {
  if (!item) {
    return false;
  }
  if (item.body?.dbOpsProgress == null) {
    return false;
  }
  const s = item.status;
  return s === 'queued' || s === 'started';
}
