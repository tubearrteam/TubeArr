import { createAction } from 'redux-actions';
import { sortDirections } from 'Helpers/Props';
import { createThunk, handleThunks } from 'Store/thunks';
import updateSectionState from 'Utilities/State/updateSectionState';
import createFetchHandler from './Creators/createFetchHandler';
import createHandleActions from './Creators/createHandleActions';
import createSetClientSideCollectionSortReducer from './Creators/Reducers/createSetClientSideCollectionSortReducer';

//
// Variables

export const section = 'videoSelection';

//
// State

export const defaultState = {
  isFetching: false,
  isReprocessing: false,
  isPopulated: false,
  error: null,
  sortKey: 'videoNumber',
  sortDirection: sortDirections.ASCENDING,
  items: []
};

export const persistState = [
  'videoSelection.sortKey',
  'videoSelection.sortDirection'
];

//
// Actions Types

export const FETCH_VIDEOS = 'videoSelection/fetchVideos';
export const SET_VIDEOS_SORT = 'videoSelection/setVideosSort';
export const CLEAR_VIDEOS = 'videoSelection/clearVideos';

//
// Action Creators

export const fetchVideos = createThunk(FETCH_VIDEOS);
export const setVideosSort = createAction(SET_VIDEOS_SORT);
export const clearVideos = createAction(CLEAR_VIDEOS);

//
// Action Handlers

export const actionHandlers = handleThunks({
  [FETCH_VIDEOS]: createFetchHandler(section, '/videos')
});

//
// Reducers

export const reducers = createHandleActions({

  [SET_VIDEOS_SORT]: createSetClientSideCollectionSortReducer(section),

  [CLEAR_VIDEOS]: (state) => {
    return updateSectionState(state, section, {
      ...defaultState,
      sortKey: state.sortKey,
      sortDirection: state.sortDirection
    });
  }

}, defaultState, section);
