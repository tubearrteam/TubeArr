import { createAction } from 'redux-actions';
import createFetchHandler from 'Store/Actions/Creators/createFetchHandler';
import createSaveHandler from 'Store/Actions/Creators/createSaveHandler';
import createSetSettingValueReducer from 'Store/Actions/Creators/Reducers/createSetSettingValueReducer';
import { createThunk } from 'Store/thunks';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import { set } from 'Store/Actions/baseActions';

const section = 'settings.slskd';

export const FETCH_SLSKD_SETTINGS = 'settings/slskd/fetchSlskdSettings';
export const SET_SLSKD_SETTINGS_VALUE = 'settings/slskd/setSlskdSettingsValue';
export const SAVE_SLSKD_SETTINGS = 'settings/slskd/saveSlskdSettings';
export const TEST_SLSKD = 'settings/slskd/testSlskd';

export const fetchSlskdSettings = createThunk(FETCH_SLSKD_SETTINGS);
export const saveSlskdSettings = createThunk(SAVE_SLSKD_SETTINGS);
export const testSlskd = createThunk(TEST_SLSKD);
export const setSlskdSettingsValue = createAction(SET_SLSKD_SETTINGS_VALUE, (payload) => ({
  section,
  ...payload
}));

function handleTestSlskd(getState, payload, dispatch) {
  dispatch(set({ section, isTesting: true, testMessage: null, testSuccess: null }));

  const { request } = createAjaxRequest({
    url: '/config/slskd/test',
    method: 'POST',
    dataType: 'json'
  });

  request.done((data) => {
    dispatch(set({
      section,
      isTesting: false,
      testSuccess: data.success === true,
      testMessage: data.message || (data.success ? 'OK' : 'Failed')
    }));
  });

  request.fail((xhr) => {
    const message = xhr.responseJSON?.message || xhr.statusText || 'Request failed';
    dispatch(set({
      section,
      isTesting: false,
      testSuccess: false,
      testMessage: message
    }));
  });
}

export default {
  defaultState: {
    isFetching: false,
    isPopulated: false,
    error: null,
    pendingChanges: {},
    isSaving: false,
    saveError: null,
    item: {},
    isTesting: false,
    testMessage: null,
    testSuccess: null
  },

  actionHandlers: {
    [FETCH_SLSKD_SETTINGS]: createFetchHandler(section, '/config/slskd'),
    [SAVE_SLSKD_SETTINGS]: createSaveHandler(section, '/config/slskd'),
    [TEST_SLSKD]: handleTestSlskd
  },

  reducers: {
    [SET_SLSKD_SETTINGS_VALUE]: createSetSettingValueReducer(section)
  }
};
