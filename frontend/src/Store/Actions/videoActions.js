import _ from 'lodash';
import React from 'react';
import { createAction } from 'redux-actions';
import { batchActions } from 'redux-batched-actions';
import Icon from 'Components/Icon';
import videoEntities from 'Video/videoEntities';
import { icons, sortDirections } from 'Helpers/Props';
import { createThunk, handleThunks } from 'Store/thunks';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import translate from 'Utilities/String/translate';
import { set, update, updateItem } from './baseActions';
import createHandleActions from './Creators/createHandleActions';
import createSetClientSideCollectionSortReducer from './Creators/Reducers/createSetClientSideCollectionSortReducer';
import createSetTableOptionReducer from './Creators/Reducers/createSetTableOptionReducer';

//
// Variables

export const section = 'videos';

//
// State

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  error: null,
  params: {},
  sortKey: 'uploadDateUtc',
  sortDirection: sortDirections.DESCENDING,
  items: [],

  columns: [
    {
      name: 'monitored',
      columnLabel: () => translate('Monitored'),
      isVisible: true,
      isModifiable: false
    },
    {
      name: 'videoNumber',
      label: () => translate('VideoTableColumnHash'),
      isVisible: true,
      isSortable: true
    },
    {
      name: 'title',
      label: () => translate('Title'),
      isVisible: true,
      isSortable: true
    },
    {
      name: 'status',
      label: () => translate('Status'),
      isVisible: true,
      isSortable: false
    },
    {
      name: 'path',
      label: () => translate('Path'),
      isVisible: true,
      isSortable: false
    },
    {
      name: 'relativePath',
      label: () => translate('RelativePath'),
      isVisible: true,
      isSortable: false
    },
    {
      name: 'size',
      label: () => translate('Size'),
      isVisible: true,
      isSortable: false
    },
    {
      name: 'runtime',
      label: () => translate('Runtime'),
      isVisible: true,
      isSortable: true
    },
    {
      name: 'videoCodec',
      label: () => translate('VideoCodec'),
      isVisible: true,
      isSortable: false
    },
    {
      name: 'customFormats',
      label: () => translate('Formats'),
      isVisible: true,
      isSortable: false
    },
    {
      name: 'audioInfo',
      label: () => translate('AudioInfo'),
      isVisible: true,
      isSortable: false
    },
    {
      name: 'uploadDateUtc',
      label: () => translate('AirDate'),
      isVisible: true,
      isSortable: true
    },
    {
      name: 'actions',
      columnLabel: () => translate('Actions'),
      label: () => '',
      isVisible: true,
      isModifiable: false
    }
  ]
};

export const persistState = [
  'videos.columns',
  'videos.sortDirection',
  'videos.sortKey'
];

//
// Actions Types

export const FETCH_VIDEOS = 'videos/fetchVideos';
export const SET_VIDEOS_SORT = 'videos/setVideosSort';
export const SET_VIDEOS_TABLE_OPTION = 'videos/setVideosTableOption';
export const CLEAR_VIDEOS = 'videos/clearVideos';
export const TOGGLE_VIDEO_MONITORED = 'videos/toggleVideoMonitored';
export const TOGGLE_VIDEOS_MONITORED = 'videos/toggleVideosMonitored';

//
// Action Creators

export const fetchVideos = createThunk(FETCH_VIDEOS);
export const setVideosSort = createAction(SET_VIDEOS_SORT);
export const setVideosTableOption = createAction(SET_VIDEOS_TABLE_OPTION);
export const clearVideos = createAction(CLEAR_VIDEOS);
export const toggleVideoMonitored = createThunk(TOGGLE_VIDEO_MONITORED);
export const toggleVideosMonitored = createThunk(TOGGLE_VIDEOS_MONITORED);

//
// Action Handlers

export const actionHandlers = handleThunks({
  [FETCH_VIDEOS]: function(getState, payload, dispatch) {
    dispatch(set({ section, isFetching: true }));
    const params = payload || {};
    dispatch(set({ section, params }));
    const channelId = params.channelId;
    const url = channelId != null ? `/videos?channelId=${encodeURIComponent(channelId)}` : '/videos';
    const { request, abortRequest } = createAjaxRequest({
      url,
      method: 'GET',
      dataType: 'json',
      cache: false
    });
    request.done((data) => {
      let items = [];
      if (Array.isArray(data)) {
        items = data;
      } else if (data && Array.isArray(data.items)) {
        items = data.items;
      } else if (data && Array.isArray(data.data)) {
        items = data.data;
      } else if (data && typeof data === 'object' && data !== null && !Array.isArray(data)) {
        // Single video wrapped as object (ensure we always store an array)
        items = [data];
      }
      dispatch(batchActions([
        update({ section, data: items }),
        set({
          section,
          isFetching: false,
          isPopulated: true,
          error: null
        })
      ]));
    });
    request.fail((xhr) => {
      dispatch(set({
        section,
        isFetching: false,
        isPopulated: false,
        error: xhr.aborted ? null : xhr
      }));
    });
    return abortRequest;
  },

  [TOGGLE_VIDEO_MONITORED]: function(getState, payload, dispatch) {
    const {
      videoId: id,
      videoEntity = videoEntities.VIDEOS,
      monitored
    } = payload;

    // Optimistic update so bookmark reflects new value without refresh
    dispatch(updateItem({
      id,
      section: videoEntity,
      isSaving: true,
      monitored
    }));

    const promise = createAjaxRequest({
      url: `/videos/${id}`,
      method: 'PUT',
      data: JSON.stringify({ monitored }),
      dataType: 'json'
    }).request;

    promise.done(() => {
      dispatch(updateItem({
        id,
        section: videoEntity,
        isSaving: false,
        monitored
      }));
    });

    promise.fail(() => {
      dispatch(updateItem({
        id,
        section: videoEntity,
        isSaving: false,
        monitored: !monitored
      }));
    });
  },

  [TOGGLE_VIDEOS_MONITORED]: function(getState, payload, dispatch) {
    const {
      videoIds,
      videoEntity = videoEntities.VIDEOS,
      monitored
    } = payload;

    // Optimistic update: apply monitored immediately so bookmark/counts update without refresh
    dispatch(batchActions(
      videoIds.map((videoId) => {
        return updateItem({
          id: videoId,
          section: videoEntity,
          isSaving: true,
          monitored
        });
      })
    ));

    const promise = createAjaxRequest({
      url: '/videos/monitor',
      method: 'PUT',
      data: JSON.stringify({ videoIds, monitored }),
      dataType: 'json'
    }).request;

    promise.done(() => {
      dispatch(batchActions(
        videoIds.map((videoId) => {
          return updateItem({
            id: videoId,
            section: videoEntity,
            isSaving: false,
            monitored
          });
        })
      ));
    });

    promise.fail(() => {
      // Revert on failure so UI matches server
      dispatch(batchActions(
        videoIds.map((videoId) => {
          return updateItem({
            id: videoId,
            section: videoEntity,
            isSaving: false,
            monitored: !monitored
          });
        })
      ));
    });
  }
});

//
// Reducers

export const reducers = createHandleActions({

  [SET_VIDEOS_TABLE_OPTION]: createSetTableOptionReducer(section),

  [CLEAR_VIDEOS]: (state) => {
    return Object.assign({}, state, {
      isFetching: false,
      isPopulated: false,
      error: null,
      items: []
    });
  },

  [SET_VIDEOS_SORT]: createSetClientSideCollectionSortReducer(section)

}, defaultState, section);
