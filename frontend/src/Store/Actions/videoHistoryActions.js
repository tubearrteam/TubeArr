import { createAction } from 'redux-actions';
import { batchActions } from 'redux-batched-actions';
import { sortDirections } from 'Helpers/Props';
import { createThunk, handleThunks } from 'Store/thunks';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import { set, update } from './baseActions';
import createHandleActions from './Creators/createHandleActions';

//
// Variables

export const section = 'videoHistory';

//
// State

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  error: null,
  items: [],
  params: {}
};

//
// Actions Types

export const FETCH_VIDEO_HISTORY = 'videoHistory/fetchVideoHistory';
export const CLEAR_VIDEO_HISTORY = 'videoHistory/clearVideoHistory';
export const VIDEO_HISTORY_MARK_AS_FAILED = 'videoHistory/markAsFailed';

//
// Action Creators

export const fetchVideoHistory = createThunk(FETCH_VIDEO_HISTORY);
export const clearVideoHistory = createAction(CLEAR_VIDEO_HISTORY);
export const videoHistoryMarkAsFailed = createThunk(VIDEO_HISTORY_MARK_AS_FAILED);

//
// Action Handlers

export const actionHandlers = handleThunks({

  [FETCH_VIDEO_HISTORY]: function(getState, payload, dispatch) {
    dispatch(set({ section, isFetching: true }));

    const queryParams = {
      pageSize: 1000,
      page: 1,
      sortKey: 'date',
      sortDirection: sortDirections.DESCENDING,
      videoId: payload.videoId
    };
    dispatch(set({ section, params: { videoId: payload.videoId } }));

    const promise = createAjaxRequest({
      url: '/history',
      data: queryParams
    }).request;

    promise.done((data) => {
      dispatch(batchActions([
        update({ section, data: data.records }),

        set({
          section,
          isFetching: false,
          isPopulated: true,
          error: null
        })
      ]));
    });

    promise.fail((xhr) => {
      dispatch(set({
        section,
        isFetching: false,
        isPopulated: false,
        error: xhr
      }));
    });
  },

  [VIDEO_HISTORY_MARK_AS_FAILED]: function(getState, payload, dispatch) {
    const {
      historyId,
      videoId
    } = payload;

    const promise = createAjaxRequest({
      url: `/history/failed/${historyId}`,
      method: 'POST'
    }).request;

    promise.done(() => {
      dispatch(fetchVideoHistory({ videoId }));
    });
  }
});

//
// Reducers

export const reducers = createHandleActions({

  [CLEAR_VIDEO_HISTORY]: (state) => {
    return Object.assign({}, state, defaultState);
  }

}, defaultState, section);

