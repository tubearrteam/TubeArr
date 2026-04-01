import _ from 'lodash';
import { createAction } from 'redux-actions';
import { batchActions } from 'redux-batched-actions';
import { createThunk, handleThunks } from 'Store/thunks';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import getNewChannel from 'Utilities/Channel/getNewChannel';
import { normalizeChannelSearchItems } from 'Utilities/Channel/normalizeChannelSearchItem';
import monitorOptions from 'Utilities/Channel/monitorOptions';
import * as channelTypes from 'Utilities/Channel/channelTypes';
import getSectionState from 'Utilities/State/getSectionState';
import updateSectionState from 'Utilities/State/updateSectionState';
import { set, update, updateItem } from './baseActions';
import createHandleActions from './Creators/createHandleActions';
import createSetSettingValueReducer from './Creators/Reducers/createSetSettingValueReducer';

//
// Variables

export const section = 'addChannel';
let abortCurrentRequest = null;
let abortCurrentResolveRequest = null;

const resolveCache = new Map();
const RESOLVE_CACHE_TTL_MS = 60000;

function getCachedResolve(input) {
  const key = (input || '').trim().toLowerCase();
  if (!key) return null;
  const entry = resolveCache.get(key);
  if (!entry || Date.now() - entry.at > RESOLVE_CACHE_TTL_MS) return null;
  return entry;
}

function setCachedResolve(input, data) {
  const key = (input || '').trim().toLowerCase();
  if (!key || !data) return;
  resolveCache.set(key, { data, at: Date.now() });
}

//
// Resolve status: idle | typing | resolving | resolvedDirect | resolvedHttp | searchPending | searching | resolvedSearch | failed

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  error: null,
  isAdding: false,
  isAdded: false,
  addError: null,
  items: [],

  resolveStatus: 'idle',
  pendingInputId: null,

  defaults: {
    rootFolderPath: '',
    monitor: monitorOptions[0].key,
    roundRobinLatestVideoCount: '',
    qualityProfileId: 0,
    channelType: channelTypes.STANDARD,
    playlistFolder: true,
    searchForMissingVideos: false,
    searchForCutoffUnmetVideos: false,
    filterOutShorts: false,
    filterOutLivestreams: false,
    tags: []
  }
};

export const persistState = [
  'addChannel.defaults'
];

//
// Actions Types

export const LOOKUP_CHANNEL = 'addChannel/lookupChannel';
export const RESOLVE_CHANNEL = 'addChannel/resolveChannel';
export const ADD_CHANNEL = 'addChannel/addChannel';
export const SET_ADD_CHANNEL_VALUE = 'addChannel/setAddChannelValue';
export const SET_ADD_CHANNEL_RESOLVE_STATUS = 'addChannel/setAddChannelResolveStatus';
export const CLEAR_ADD_CHANNEL = 'addChannel/clearAddChannel';
export const SET_ADD_CHANNEL_DEFAULT = 'addChannel/setAddChannelDefault';

//
// Action Creators

export const SEARCH_FALLBACK_DEBOUNCE_MS = 2000;

export const lookupChannel = createThunk(LOOKUP_CHANNEL);
export const resolveChannel = createThunk(RESOLVE_CHANNEL);
export const addChannel = createThunk(ADD_CHANNEL);

export const RUN_SEARCH_FALLBACK = 'addChannel/runSearchFallback';
export const runSearchFallback = createThunk(RUN_SEARCH_FALLBACK);
export const setAddChannelResolveStatus = createAction(SET_ADD_CHANNEL_RESOLVE_STATUS, (payload) => ({
  section,
  ...payload
}));
export const clearAddChannel = createAction(CLEAR_ADD_CHANNEL);
export const setAddChannelDefault = createAction(SET_ADD_CHANNEL_DEFAULT);

export const setAddChannelValue = createAction(SET_ADD_CHANNEL_VALUE, (payload) => {
  return {
    section,
    ...payload
  };
});

//
// Action Handlers

export const actionHandlers = handleThunks({

  [RESOLVE_CHANNEL]: function(getState, payload, dispatch) {
    const inputId = payload.inputId;
    const input = (payload.input || '').trim();
    const cached = getCachedResolve(input);
    const cachedSuccess = cached && (cached.data.success !== undefined ? cached.data.success : cached.data.Success);
    const cachedItems = cached && (cached.data.items ?? cached.data.Items);
    if (cachedSuccess && cachedItems && cachedItems.length > 0) {
      const method = cached.data.resolutionMethod ?? cached.data.ResolutionMethod ?? '';
      const isDirect = method === 'direct-channel-id' || method === 'direct-channel-url' || method === 'direct-handle';
      dispatch(batchActions([
        update({ section, data: normalizeChannelSearchItems(cachedItems) }),
        set({
          section,
          isFetching: false,
          isPopulated: true,
          error: null,
          resolveStatus: isDirect ? 'resolvedDirect' : 'resolvedHttp',
          pendingInputId: null
        })
      ]));
      return;
    }

    dispatch(set({
      section,
      isFetching: true,
      resolveStatus: 'resolving',
      pendingInputId: inputId
    }));

    if (abortCurrentResolveRequest) {
      abortCurrentResolveRequest();
    }

    const { request, abortRequest } = createAjaxRequest({
      url: '/channels/resolve',
      data: { input: payload.input }
    });

    abortCurrentResolveRequest = abortRequest;

    request.done((data) => {
      const state = getState();
      if (state.addChannel.pendingInputId !== inputId) {
        return;
      }
      // Backend may return PascalCase (Success, Items, ...); accept both for compatibility
      const success = data && (data.success !== undefined ? data.success : data.Success);
      const items = normalizeChannelSearchItems(data && (data.items ?? data.Items));
      const method = (data && (data.resolutionMethod ?? data.ResolutionMethod)) || '';
      const isDirect = method === 'direct-channel-id' || method === 'direct-channel-url' || method === 'direct-handle';
      if (success && items.length > 0) {
        setCachedResolve(payload.input, data);
      }
      const failureReason = data && (data.failureReason ?? data.FailureReason);

      dispatch(batchActions([
        update({ section, data: items }),
        set({
          section,
          isFetching: false,
          isPopulated: success && items.length > 0,
          error: success ? null : failureReason ? { message: failureReason } : null,
          resolveStatus: success ? (isDirect ? 'resolvedDirect' : 'resolvedHttp') : 'failed',
          pendingInputId: null
        })
      ]));
    });

    request.fail((xhr) => {
      const state = getState();
      if (state.addChannel.pendingInputId !== inputId) {
        return;
      }
      // When aborted, do not clear pendingInputId so the newer resolve response can still be applied
      if (xhr.aborted) {
        return;
      }
      dispatch(set({
        section,
        isFetching: false,
        isPopulated: false,
        error: xhr,
        resolveStatus: 'failed',
        pendingInputId: null
      }));
    });
  },

  [RUN_SEARCH_FALLBACK]: function(getState, payload, dispatch) {
    const state = getState();
    const { resolveStatus, pendingInputId } = state.addChannel;
    if (resolveStatus === 'resolvedDirect' || resolveStatus === 'resolvedHttp') {
      return;
    }
    if (pendingInputId !== payload.inputId) {
      return;
    }
    dispatch(lookupChannel({ term: payload.term, inputId: payload.inputId }));
  },

  [LOOKUP_CHANNEL]: function(getState, payload, dispatch) {
    const inputId = payload.inputId;
    dispatch(set({
      section,
      isFetching: true,
      resolveStatus: 'searching',
      pendingInputId: inputId
    }));

    if (abortCurrentRequest) {
      abortCurrentRequest();
    }

    const { request, abortRequest } = createAjaxRequest({
      url: '/channels/search',
      data: {
        term: payload.term
      }
    });

    abortCurrentRequest = abortRequest;

    request.done((data) => {
      const state = getState();
      if (state.addChannel.pendingInputId !== inputId) {
        return;
      }
      // If resolve already succeeded (direct or HTTP), don't overwrite with search results
      if (state.addChannel.resolveStatus === 'resolvedDirect' || state.addChannel.resolveStatus === 'resolvedHttp') {
        dispatch(set({ section, isFetching: false, pendingInputId: null }));
        return;
      }
      // Search API returns a raw array; normalize so section.items is always set and camelCase
      const rawItems = Array.isArray(data) ? data : (data && (data.items ?? data.Items)) || [];
      const items = normalizeChannelSearchItems(rawItems);
      dispatch(batchActions([
        update({ section, data: items }),
        set({
          section,
          isFetching: false,
          isPopulated: true,
          error: null,
          resolveStatus: 'resolvedSearch',
          pendingInputId: null
        })
      ]));
    });

    request.fail((xhr) => {
      const state = getState();
      if (state.addChannel.pendingInputId !== inputId) {
        return;
      }
      if (xhr.aborted) {
        return;
      }
      dispatch(set({
        section,
        isFetching: false,
        isPopulated: false,
        error: xhr,
        resolveStatus: 'failed',
        pendingInputId: null
      }));
    });
  },

  [ADD_CHANNEL]: function(getState, payload, dispatch) {
    dispatch(set({ section, isAdding: true }));

    const items = getState().addChannel.items;

    const youtubeChannelId = payload.youtubeChannelId;
    const existing = youtubeChannelId ? _.find(items, { youtubeChannelId }) : null;
    const newChannel = getNewChannel(_.cloneDeep(existing), payload);

    const promise = createAjaxRequest({
      url: '/channels',
      method: 'POST',
      dataType: 'json',
      contentType: 'application/json',
      data: JSON.stringify(newChannel)
    }).request;

    promise.done((data) => {
      dispatch(batchActions([
        updateItem({ section: 'channels', ...data }),

        set({
          section,
          isAdding: false,
          isAdded: true,
          addError: null
        })
      ]));
    });

    promise.fail((xhr) => {
      dispatch(set({
        section,
        isAdding: false,
        isAdded: false,
        addError: xhr
      }));
    });
  }
});

//
// Reducers

export const reducers = createHandleActions({

  [SET_ADD_CHANNEL_VALUE]: createSetSettingValueReducer(section),

  [SET_ADD_CHANNEL_DEFAULT]: function(state, { payload }) {
    const newState = getSectionState(state, section);

    newState.defaults = {
      ...newState.defaults,
      ...payload
    };

    return updateSectionState(state, section, newState);
  },

  [CLEAR_ADD_CHANNEL]: function(state) {
    const {
      defaults,
      ...otherDefaultState
    } = defaultState;

    return Object.assign({}, state, otherDefaultState, { defaults: state.defaults ?? defaults });
  },

  [SET_ADD_CHANNEL_RESOLVE_STATUS]: function(state, { payload }) {
    const newState = getSectionState(state, section);
    if (payload.resolveStatus != null) {
      newState.resolveStatus = payload.resolveStatus;
      if (payload.resolveStatus === 'searchPending') {
        newState.isFetching = true;
      }
    }
    if (payload.pendingInputId !== undefined) {
      newState.pendingInputId = payload.pendingInputId;
    }
    return updateSectionState(state, section, newState);
  }

}, defaultState, section);
