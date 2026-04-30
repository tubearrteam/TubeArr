import createHandleActions from './Creators/createHandleActions';

export const section = 'operationProgress';

export interface OperationProgressState {
  active: boolean;
  done: number;
  total: number;
  label: string | null;
  lastUpdated: number | null;
}

export const defaultState: OperationProgressState = {
  active: false,
  done: 0,
  total: 0,
  label: null,
  lastUpdated: null,
};

export const REQUEST_STARTED = 'operationProgress/requestStarted';
export const REQUEST_FINISHED = 'operationProgress/requestFinished';
export const RESET = 'operationProgress/reset';

export function requestStarted(payload?: { label?: string | null }) {
  return {
    type: REQUEST_STARTED,
    payload: {
      section,
      label: payload?.label ?? null,
      at: Date.now(),
    },
  };
}

export function requestFinished() {
  return {
    type: REQUEST_FINISHED,
    payload: {
      section,
      at: Date.now(),
    },
  };
}

export function resetProgress() {
  return {
    type: RESET,
    payload: {
      section,
      at: Date.now(),
    },
  };
}

export const reducers = createHandleActions(
  {
    [REQUEST_STARTED]: (state: OperationProgressState, { payload }: any) => {
      const nextTotal = (state.total ?? 0) + 1;
      return {
        ...state,
        active: true,
        total: nextTotal,
        label: payload?.label ?? state.label ?? null,
        lastUpdated: payload?.at ?? Date.now(),
      };
    },

    [REQUEST_FINISHED]: (state: OperationProgressState, { payload }: any) => {
      const nextDone = Math.min((state.done ?? 0) + 1, state.total ?? 0);
      const at = payload?.at ?? Date.now();
      return {
        ...state,
        done: nextDone,
        active: (state.total ?? 0) > 0 ? nextDone < (state.total ?? 0) : false,
        lastUpdated: at,
      };
    },

    [RESET]: (_state: OperationProgressState, { payload }: any) => {
      return {
        ...defaultState,
        lastUpdated: payload?.at ?? Date.now(),
      };
    },
  },
  defaultState,
  section
);

