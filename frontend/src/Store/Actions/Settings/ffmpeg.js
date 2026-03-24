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

const section = 'settings.ffmpeg';

//
// Actions Types

export const FETCH_FFMPEG_SETTINGS = 'settings/ffmpeg/fetchFfmpegSettings';
export const SET_FFMPEG_SETTINGS_VALUE = 'settings/ffmpeg/setFfmpegSettingsValue';
export const SAVE_FFMPEG_SETTINGS = 'settings/ffmpeg/saveFfmpegSettings';
export const TEST_FFMPEG = 'settings/ffmpeg/testFfmpeg';
export const FETCH_FFMPEG_RELEASES = 'settings/ffmpeg/fetchFfmpegReleases';
export const DOWNLOAD_FFMPEG = 'settings/ffmpeg/downloadFfmpeg';
export const SET_FFMPEG_DOWNLOAD_SELECTION = 'settings/ffmpeg/setFfmpegDownloadSelection';

//
// Action Creators

export const fetchFfmpegSettings = createThunk(FETCH_FFMPEG_SETTINGS);
export const saveFfmpegSettings = createThunk(SAVE_FFMPEG_SETTINGS);
export const testFfmpeg = createThunk(TEST_FFMPEG);
export const fetchFfmpegReleases = createThunk(FETCH_FFMPEG_RELEASES);
export const downloadFfmpeg = createThunk(DOWNLOAD_FFMPEG);
export const setFfmpegSettingsValue = createAction(SET_FFMPEG_SETTINGS_VALUE, (payload) => {
  return {
    section,
    ...payload
  };
});
export const setFfmpegDownloadSelection = createAction(SET_FFMPEG_DOWNLOAD_SELECTION, (payload) => ({
  section,
  ...payload
}));

//
// Action Handlers

function handleTestFfmpeg(getState, payload, dispatch) {
  dispatch(set({ section, isTesting: true, testMessage: null, testSuccess: null }));

  const { request } = createAjaxRequest({
    url: '/config/ffmpeg/test',
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

function handleFetchFfmpegReleases(getState, payload, dispatch) {
  dispatch(set({ section, isFetchingReleases: true, releasesError: null }));
  const { request } = createAjaxRequest({
    url: '/config/ffmpeg/releases',
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

function handleDownloadFfmpeg(getState, payload, dispatch) {
  const { downloadUrl, assetName, releaseTag } = payload;
  dispatch(set({ section, isDownloading: true, downloadError: null, downloadSuccess: null }));
  const { request } = createAjaxRequest({
    url: '/config/ffmpeg/download',
    method: 'POST',
    contentType: 'application/json',
    data: JSON.stringify({ downloadUrl, assetName, releaseTag })
  });
  request.done((data) => {
    dispatch(set({
      section,
      isDownloading: false,
      downloadError: null,
      downloadSuccess: data.savePath || data.executablePath || 'Download complete.',
      item: Object.assign({}, getState().settings?.ffmpeg?.item, { executablePath: data.executablePath || data.savePath }),
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
    downloadSuccess: null
  },

  //
  // Action Handlers

  actionHandlers: {
    [FETCH_FFMPEG_SETTINGS]: createFetchHandler(section, '/config/ffmpeg'),
    [SAVE_FFMPEG_SETTINGS]: createSaveHandler(section, '/config/ffmpeg'),
    [TEST_FFMPEG]: handleTestFfmpeg,
    [FETCH_FFMPEG_RELEASES]: handleFetchFfmpegReleases,
    [DOWNLOAD_FFMPEG]: handleDownloadFfmpeg
  },

  //
  // Reducers

  reducers: {
    [SET_FFMPEG_SETTINGS_VALUE]: createSetSettingValueReducer(section),
    [SET_FFMPEG_DOWNLOAD_SELECTION]: (state, { payload }) => {
      if (payload.section !== section) return state;
      const newState = getSectionState(state, section);
      if (payload.selectedReleaseTag !== undefined) newState.selectedReleaseTag = payload.selectedReleaseTag;
      if (payload.selectedAsset !== undefined) newState.selectedAsset = payload.selectedAsset;
      return updateSectionState(state, section, newState);
    }
  }

};
