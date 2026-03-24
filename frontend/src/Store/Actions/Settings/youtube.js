import { createAction } from 'redux-actions';
import createFetchHandler from 'Store/Actions/Creators/createFetchHandler';
import createSaveHandler from 'Store/Actions/Creators/createSaveHandler';
import createSetSettingValueReducer from 'Store/Actions/Creators/Reducers/createSetSettingValueReducer';
import { createThunk } from 'Store/thunks';

//
// Variables

const section = 'settings.youtube';

//
// Actions Types

export const FETCH_YOUTUBE_SETTINGS = 'settings/youtube/fetchYouTubeSettings';
export const SET_YOUTUBE_SETTINGS_VALUE = 'settings/youtube/setYouTubeSettingsValue';
export const SAVE_YOUTUBE_SETTINGS = 'settings/youtube/saveYouTubeSettings';

//
// Action Creators

export const fetchYouTubeSettings = createThunk(FETCH_YOUTUBE_SETTINGS);
export const saveYouTubeSettings = createThunk(SAVE_YOUTUBE_SETTINGS);
export const setYouTubeSettingsValue = createAction(SET_YOUTUBE_SETTINGS_VALUE, (payload) => {
  return {
    section,
    ...payload
  };
});

//
// Details

export default {

  //
  // State

  defaultState: {
    isFetching: false,
    isPopulated: false,
    error: null,
    pendingChanges: {},
    isSaving: false,
    saveError: null,
    item: {}
  },

  //
  // Action Handlers

  actionHandlers: {
    [FETCH_YOUTUBE_SETTINGS]: createFetchHandler(section, '/config/youtube'),
    [SAVE_YOUTUBE_SETTINGS]: createSaveHandler(section, '/config/youtube')
  },

  //
  // Reducers

  reducers: {
    [SET_YOUTUBE_SETTINGS_VALUE]: createSetSettingValueReducer(section)
  }

};
