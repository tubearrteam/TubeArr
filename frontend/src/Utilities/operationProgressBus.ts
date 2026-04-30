import type { Store } from 'redux';
import { requestFinished, requestStarted } from 'Store/Actions/operationProgressActions';

type OperationProgressDispatch = Store['dispatch'] | null;

let dispatchRef: OperationProgressDispatch = null;

export type ProgressTag = 'import_move';

export function setOperationProgressDispatch(dispatch: Store['dispatch']) {
  dispatchRef = dispatch;
}

export function progressRequestStarted(tag: ProgressTag, label?: string) {
  if (!dispatchRef) return;
  if (tag === 'import_move') {
    dispatchRef(requestStarted({ label: label ?? 'Working' }));
  }
}

export function progressRequestFinished(tag: ProgressTag) {
  if (!dispatchRef) return;
  if (tag === 'import_move') {
    dispatchRef(requestFinished());
  }
}

