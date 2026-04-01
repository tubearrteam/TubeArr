import { createAction } from 'redux-actions';
import createFetchHandler from 'Store/Actions/Creators/createFetchHandler';
import createSaveHandler from 'Store/Actions/Creators/createSaveHandler';
import createSetSettingValueReducer from 'Store/Actions/Creators/Reducers/createSetSettingValueReducer';
import { createThunk } from 'Store/thunks';

//
// Variables
//

const section = 'settings.plexProvider';

//
// Actions Types
//

export const FETCH_PLEX_PROVIDER_SETTINGS = 'settings/plexProvider/fetchPlexProviderSettings';
export const SAVE_PLEX_PROVIDER_SETTINGS = 'settings/plexProvider/savePlexProviderSettings';
export const SET_PLEX_PROVIDER_SETTINGS_VALUE = 'settings/plexProvider/setPlexProviderSettingsValue';

//
// Action Creators
//

export const fetchPlexProviderSettings = createThunk(FETCH_PLEX_PROVIDER_SETTINGS);
export const savePlexProviderSettings = createThunk(SAVE_PLEX_PROVIDER_SETTINGS);
export const setPlexProviderSettingsValue = createAction(SET_PLEX_PROVIDER_SETTINGS_VALUE, (payload) => {
  return {
    section,
    ...payload
  };
});

//
// Details
//

export default {

  //
  // State
  //

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
  //

  actionHandlers: {
    [FETCH_PLEX_PROVIDER_SETTINGS]: createFetchHandler(section, '/config/plex-provider'),
    [SAVE_PLEX_PROVIDER_SETTINGS]: createSaveHandler(section, '/config/plex-provider')
  },

  //
  // Reducers
  //

  reducers: {
    [SET_PLEX_PROVIDER_SETTINGS_VALUE]: createSetSettingValueReducer(section)
  }

};

