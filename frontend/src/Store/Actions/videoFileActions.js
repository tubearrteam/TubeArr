import _ from 'lodash';
import { createAction } from 'redux-actions';
import { batchActions } from 'redux-batched-actions';
import videoEntities from 'Video/videoEntities';
import { createThunk, handleThunks } from 'Store/thunks';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import { removeItem, set, updateItem } from './baseActions';
import createFetchHandler from './Creators/createFetchHandler';
import createHandleActions from './Creators/createHandleActions';
import createRemoveItemHandler from './Creators/createRemoveItemHandler';

//
// Variables

export const section = 'videoFiles';

//
// State

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  error: null,
  isDeleting: false,
  deleteError: null,
  isSaving: false,
  saveError: null,
  items: []
};

//
// Actions Types

export const FETCH_VIDEO_FILE = 'videoFiles/fetchVideoFile';
export const FETCH_VIDEO_FILES = 'videoFiles/fetchVideoFiles';
export const DELETE_VIDEO_FILE = 'videoFiles/deleteVideoFile';
export const DELETE_VIDEO_FILES = 'videoFiles/deleteVideoFiles';
export const UPDATE_VIDEO_FILES = 'videoFiles/updateVideoFiles';
export const CLEAR_VIDEO_FILES = 'videoFiles/clearVideoFiles';

//
// Action Creators

export const fetchVideoFile = createThunk(FETCH_VIDEO_FILE);
export const fetchVideoFiles = createThunk(FETCH_VIDEO_FILES);
export const deleteVideoFile = createThunk(DELETE_VIDEO_FILE);
export const deleteVideoFiles = createThunk(DELETE_VIDEO_FILES);
export const updateVideoFiles = createThunk(UPDATE_VIDEO_FILES);
export const clearVideoFiles = createAction(CLEAR_VIDEO_FILES);

//
// Helpers

const deleteVideoFileHelper = createRemoveItemHandler(section, '/videoFile');

//
// Action Handlers

export const actionHandlers = handleThunks({
  [FETCH_VIDEO_FILE]: createFetchHandler(section, '/videoFile'),
  [FETCH_VIDEO_FILES]: createFetchHandler(section, '/videoFile'),

  [DELETE_VIDEO_FILE]: function(getState, payload, dispatch) {
    const {
      id: videoFileId,
      videoEntity = videoEntities.VIDEOS
    } = payload;
    const deletePromise = deleteVideoFileHelper(getState, payload, dispatch);

    deletePromise.done(() => {
      const videos = getState().videos.items;
      const videosWithRemovedFiles = _.filter(videos, { videoFileId: videoFileId });

      dispatch(batchActions([
        ...videosWithRemovedFiles.map((video) => {
          return updateItem({
            section: videoEntity,
            ...video,
            videoFileId: 0,
            hasFile: false
          });
        })
      ]));
    });
  },

  [DELETE_VIDEO_FILES]: function(getState, payload, dispatch) {
    const {
      videoFileIds
    } = payload;

    dispatch(set({ section, isDeleting: true }));

    const promise = createAjaxRequest({
      url: '/videoFile/bulk',
      method: 'DELETE',
      dataType: 'json',
      data: JSON.stringify({ videoFileIds })
    }).request;

    promise.done(() => {
      const videos = getState().videos.items;
      const videosWithRemovedFiles = videoFileIds.reduce((acc, videoFileId) => {
        acc.push(..._.filter(videos, { videoFileId: videoFileId }));

        return acc;
      }, []);

      dispatch(batchActions([
        ...videoFileIds.map((id) => {
          return removeItem({ section, id });
        }),

        ...videosWithRemovedFiles.map((video) => {
          return updateItem({
            section: 'videos',
            ...video,
            videoFileId: 0,
            hasFile: false
          });
        }),

        set({
          section,
          isDeleting: false,
          deleteError: null
        })
      ]));
    });

    promise.fail((xhr) => {
      dispatch(set({
        section,
        isDeleting: false,
        deleteError: xhr
      }));
    });
  },

  [UPDATE_VIDEO_FILES]: function(getState, payload, dispatch) {
    const { files } = payload;

    dispatch(set({ section, isSaving: true }));

    const requestData = files;

    const promise = createAjaxRequest({
      url: '/videoFile/bulk',
      method: 'PUT',
      dataType: 'json',
      data: JSON.stringify(requestData)
    }).request;

    promise.done((data) => {
      dispatch(batchActions([
        ...files.map((file) => {
          const id = file.id;
          const props = {};
          const videoFile = data.find((f) => f.id === id);

          props.qualityCutoffNotMet = videoFile.qualityCutoffNotMet;
          props.customFormats = videoFile.customFormats;
          props.customFormatScore = videoFile.customFormatScore;
          props.languages = file.languages;
          props.quality = file.quality;
          props.releaseGroup = file.releaseGroup;
          props.indexerFlags = file.indexerFlags;

          return updateItem({
            section,
            id,
            ...props
          });
        }),

        set({
          section,
          isSaving: false,
          saveError: null
        })
      ]));
    });

    promise.fail((xhr) => {
      dispatch(set({
        section,
        isSaving: false,
        saveError: xhr
      }));
    });
  }
});

//
// Reducers

export const reducers = createHandleActions({

  [CLEAR_VIDEO_FILES]: (state) => {
    return Object.assign({}, state, defaultState);
  }

}, defaultState, section);
