import { createAction } from 'redux-actions';
import createFetchHandler from 'Store/Actions/Creators/createFetchHandler';
import createSaveHandler from 'Store/Actions/Creators/createSaveHandler';
import createSetSettingValueReducer from 'Store/Actions/Creators/Reducers/createSetSettingValueReducer';
import { createThunk } from 'Store/thunks';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import getSectionState from 'Utilities/State/getSectionState';
import updateSectionState from 'Utilities/State/updateSectionState';
import { set } from 'Store/Actions/baseActions';

//
// Variables

const section = 'settings.ytdlp';

//
// Actions Types

export const FETCH_YTDLP_SETTINGS = 'settings/ytdlp/fetchYtdlpSettings';
export const SET_YTDLP_SETTINGS_VALUE = 'settings/ytdlp/setYtdlpSettingsValue';
export const SAVE_YTDLP_SETTINGS = 'settings/ytdlp/saveYtdlpSettings';
export const TEST_YTDLP = 'settings/ytdlp/testYtdlp';
export const FETCH_YTDLP_RELEASES = 'settings/ytdlp/fetchYtdlpReleases';
export const DOWNLOAD_YTDLP = 'settings/ytdlp/downloadYtdlp';
export const UPDATE_YTDLP = 'settings/ytdlp/updateYtdlp';
export const SET_YTDLP_DOWNLOAD_SELECTION = 'settings/ytdlp/setYtdlpDownloadSelection';

//
// Action Creators

export const fetchYtdlpSettings = createThunk(FETCH_YTDLP_SETTINGS);
export const saveYtdlpSettings = createThunk(SAVE_YTDLP_SETTINGS);
export const testYtdlp = createThunk(TEST_YTDLP);
export const fetchYtdlpReleases = createThunk(FETCH_YTDLP_RELEASES);
export const downloadYtdlp = createThunk(DOWNLOAD_YTDLP);
export const updateYtdlp = createThunk(UPDATE_YTDLP);
export const setYtdlpSettingsValue = createAction(SET_YTDLP_SETTINGS_VALUE, (payload) => {
  return {
    section,
    ...payload
  };
});
export const setYtdlpDownloadSelection = createAction(SET_YTDLP_DOWNLOAD_SELECTION, (payload) => ({
  section,
  ...payload
}));

//
// Action Handlers

function handleTestYtdlp(getState, payload, dispatch) {
  dispatch(set({ section, isTesting: true, testMessage: null, testSuccess: null }));

  const { request } = createAjaxRequest({
    url: '/config/ytdlp/test',
    method: 'POST',
    dataType: 'json'
  });

  request.done((data) => {
    dispatch(set({
      section,
      isTesting: false,
      testSuccess: data.success === true,
      testMessage: data.message || (data.success ? 'Success' : 'Failed')
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

function handleFetchYtdlpReleases(getState, payload, dispatch) {
  dispatch(set({ section, isFetchingReleases: true, releasesError: null }));
  const { request } = createAjaxRequest({
    url: '/config/ytdlp/releases',
    method: 'GET',
    dataType: 'json'
  });
  request.done((data) => {
    dispatch(set({
      section,
      isFetchingReleases: false,
      releases: Array.isArray(data) ? data : [],
      releasesError: null,
      selectedReleaseTag: null,
      selectedAsset: null
    }));
  });
  request.fail((xhr) => {
    const message = xhr.responseJSON?.message || xhr.statusText || 'Failed to fetch releases';
    dispatch(set({
      section,
      isFetchingReleases: false,
      releases: [],
      releasesError: message
    }));
  });
}

function handleDownloadYtdlp(getState, payload, dispatch) {
  const { downloadUrl, assetName } = payload;
  dispatch(set({ section, isDownloading: true, downloadError: null, downloadSuccess: null }));
  const { request } = createAjaxRequest({
    url: '/config/ytdlp/download',
    method: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({ downloadUrl, assetName })
  });
  request.done((data) => {
    dispatch(set({
      section,
      isDownloading: false,
      downloadError: null,
      downloadSuccess: data.savePath || 'Download complete.',
      item: Object.assign({}, getState().settings?.ytdlp?.item, { executablePath: data.executablePath || data.savePath }),
      pendingChanges: {}
    }));
  });
  request.fail((xhr) => {
    const message = xhr.responseJSON?.message || xhr.statusText || 'Download failed';
    dispatch(set({
      section,
      isDownloading: false,
      downloadError: message,
      downloadSuccess: null
    }));
  });
}

function handleUpdateYtdlp(getState, payload, dispatch) {
  dispatch(set({ section, isUpdating: true, updateMessage: null, updateSuccess: null }));
  const { request } = createAjaxRequest({
    url: '/config/ytdlp/update',
    method: 'POST',
    dataType: 'json'
  });
  request.done((data) => {
    dispatch(set({
      section,
      isUpdating: false,
      updateSuccess: data.success === true,
      updateMessage: data.message || (data.success ? 'Updated.' : 'Update failed.')
    }));
  });
  request.fail((xhr) => {
    const message = xhr.responseJSON?.message || xhr.statusText || 'Update failed';
    dispatch(set({
      section,
      isUpdating: false,
      updateSuccess: false,
      updateMessage: message
    }));
  });
}

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
    item: {},
    isTesting: false,
    testMessage: null,
    testSuccess: null,
    releases: [],
    isFetchingReleases: false,
    releasesError: null,
    selectedReleaseTag: null,
    selectedAsset: null,
    isDownloading: false,
    downloadError: null,
    downloadSuccess: null,
    isUpdating: false,
    updateMessage: null,
    updateSuccess: null
  },

  //
  // Action Handlers

  actionHandlers: {
    [FETCH_YTDLP_SETTINGS]: createFetchHandler(section, '/config/ytdlp'),
    [SAVE_YTDLP_SETTINGS]: createSaveHandler(section, '/config/ytdlp'),
    [TEST_YTDLP]: handleTestYtdlp,
    [FETCH_YTDLP_RELEASES]: handleFetchYtdlpReleases,
    [DOWNLOAD_YTDLP]: handleDownloadYtdlp,
    [UPDATE_YTDLP]: handleUpdateYtdlp
  },

  //
  // Reducers

  reducers: {
    [SET_YTDLP_SETTINGS_VALUE]: createSetSettingValueReducer(section),
    [SET_YTDLP_DOWNLOAD_SELECTION]: (state, { payload }) => {
      if (payload.section !== section) return state;
      const newState = getSectionState(state, section);
      if (payload.selectedReleaseTag !== undefined) newState.selectedReleaseTag = payload.selectedReleaseTag;
      if (payload.selectedAsset !== undefined) newState.selectedAsset = payload.selectedAsset;
      return updateSectionState(state, section, newState);
    }
  }

};
