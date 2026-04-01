import _ from 'lodash';
import { createAction } from 'redux-actions';
import { batchActions } from 'redux-batched-actions';
import { createThunk, handleThunks } from 'Store/thunks';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import getNewChannel from 'Utilities/Channel/getNewChannel';
import { normalizeChannelSearchItem, normalizeChannelSearchItems } from 'Utilities/Channel/normalizeChannelSearchItem';
import * as channelTypes from 'Utilities/Channel/channelTypes';
import getSectionState from 'Utilities/State/getSectionState';
import updateSectionState from 'Utilities/State/updateSectionState';
import { removeItem, set, updateItem } from './baseActions';
import createHandleActions from './Creators/createHandleActions';
import { fetchRootFolders } from './rootFolderActions';

//
// Variables

export const section = 'importChannel';
let concurrentLookups = 0;
let abortCurrentLookup = null;
const queue = [];

//
// State

export const defaultState = {
  isLookingUpChannel: false,
  isImporting: false,
  isImported: false,
  importError: null,
  items: []
};

//
// Actions Types

export const QUEUE_LOOKUP_CHANNEL = 'importChannel/queueLookupChannel';
export const START_LOOKUP_CHANNEL = 'importChannel/startLookupChannel';
export const CANCEL_LOOKUP_CHANNEL = 'importChannel/cancelLookupChannel';
export const LOOKUP_UNSEARCHED_CHANNELS = 'importChannel/lookupUnsearchedChannels';
export const CLEAR_IMPORT_CHANNEL = 'importChannel/clearImportChannel';
export const SET_IMPORT_CHANNEL_VALUE = 'importChannel/setImportChannelValue';
export const IMPORT_CHANNEL = 'importChannel/importChannel';

//
// Action Creators

export const queueLookupChannel = createThunk(QUEUE_LOOKUP_CHANNEL);
export const startLookupChannel = createThunk(START_LOOKUP_CHANNEL);
export const importChannel = createThunk(IMPORT_CHANNEL);
export const lookupUnsearchedChannels = createThunk(LOOKUP_UNSEARCHED_CHANNELS);
export const clearImportChannel = createAction(CLEAR_IMPORT_CHANNEL);
export const cancelLookupChannel = createAction(CANCEL_LOOKUP_CHANNEL);

export const setImportChannelValue = createAction(SET_IMPORT_CHANNEL_VALUE, (payload) => {
  return {
    section,
    ...payload
  };
});

//
// Action Handlers

export const actionHandlers = handleThunks({

  [QUEUE_LOOKUP_CHANNEL]: function(getState, payload, dispatch) {
    const {
      name,
      path,
      relativePath,
      term,
      topOfQueue = false
    } = payload;

    const state = getState().importChannel;
    const item = _.find(state.items, { id: name }) || {
      id: name,
      term,
      path,
      relativePath,
      isFetching: false,
      isPopulated: false,
      error: null
    };

    dispatch(updateItem({
      section,
      ...item,
      term,
      isQueued: true,
      items: []
    }));

    const itemIndex = queue.indexOf(item.id);

    if (itemIndex >= 0) {
      queue.splice(itemIndex, 1);
    }

    if (topOfQueue) {
      queue.unshift(item.id);
    } else {
      queue.push(item.id);
    }

    if (term && term.length > 2) {
      dispatch(startLookupChannel({ start: true }));
    }
  },

  [START_LOOKUP_CHANNEL]: function(getState, payload, dispatch) {
    if (concurrentLookups >= 1) {
      return;
    }

    const state = getState().importChannel;

    const {
      isLookingUpChannel,
      items
    } = state;

    const queueId = queue[0];

    if (payload.start && !isLookingUpChannel) {
      dispatch(set({ section, isLookingUpChannel: true }));
    } else if (!isLookingUpChannel) {
      return;
    } else if (!queueId) {
      dispatch(set({ section, isLookingUpChannel: false }));
      return;
    }

    concurrentLookups++;
    queue.splice(0, 1);

    const queued = items.find((i) => i.id === queueId);

    dispatch(updateItem({
      section,
      id: queued.id,
      isFetching: true
    }));

    const { request, abortRequest } = createAjaxRequest({
      url: '/channels/search',
      data: {
        term: queued.term
      }
    });

    abortCurrentLookup = abortRequest;

    request.done((data) => {
      const rawList = Array.isArray(data) ? data : [];
      const items = normalizeChannelSearchItems(rawList);
      const rawSelected = queued.selectedChannel || rawList[0];
      const selectedChannel = rawSelected ? normalizeChannelSearchItem(rawSelected) : null;

      const itemProps = {
        section,
        id: queued.id,
        isFetching: false,
        isPopulated: true,
        error: null,
        items,
        isQueued: false,
        selectedChannel,
        updateOnly: true
      };

      if (selectedChannel && selectedChannel.channelType !== channelTypes.STANDARD) {
        itemProps.channelType = selectedChannel.channelType;
      }

      dispatch(updateItem(itemProps));
    });

    request.fail((xhr) => {
      dispatch(updateItem({
        section,
        id: queued.id,
        isFetching: false,
        isPopulated: false,
        error: xhr,
        isQueued: false,
        updateOnly: true
      }));
    });

    request.always(() => {
      concurrentLookups--;

      dispatch(startLookupChannel());
    });
  },

  [LOOKUP_UNSEARCHED_CHANNELS]: function(getState, payload, dispatch) {
    const state = getState().importChannel;

    if (state.isLookingUpChannel) {
      return;
    }

    state.items.forEach((item) => {
      const id = item.id;

      if (
        !item.isPopulated &&
        !queue.includes(id)
      ) {
        queue.push(item.id);
      }
    });

    if (queue.length) {
      dispatch(startLookupChannel({ start: true }));
    }
  },

  [IMPORT_CHANNEL]: function(getState, payload, dispatch) {
    dispatch(set({ section, isImporting: true }));

    const ids = payload.ids;
    const rootFolderId = payload.rootFolderId;
    const items = getState().importChannel.items;
    const addedIds = [];

    const allNewChannels = ids.reduce((acc, id) => {
      const item = items.find((i) => i.id === id);
      const selectedChannel = item.selectedChannel;

      // Make sure we have a selected channel and
      // the same channel hasn't been added yet.
      const selectedYoutubeChannelId = selectedChannel?.youtubeChannelId;

      const isAlreadyAdded = acc.some((a) => {
        return selectedYoutubeChannelId && a.youtubeChannelId === selectedYoutubeChannelId;
      });

      if (selectedChannel && !isAlreadyAdded) {
        const newChannel = getNewChannel(_.cloneDeep(selectedChannel), item);
        newChannel.path = item.path;

        addedIds.push(id);
        acc.push(newChannel);
      }

      return acc;
    }, []);

    const promise = createAjaxRequest({
      url: '/channels/import',
      method: 'POST',
      contentType: 'application/json',
      data: JSON.stringify(allNewChannels)
    }).request;

    promise.done((data) => {
      dispatch(batchActions([
        set({
          section,
          isImporting: false,
          isImported: true,
          importError: null
        }),

        ...data.map((channel) => updateItem({ section: 'channels', ...channel })),

        ...addedIds.map((id) => removeItem({ section, id }))
      ]));

      if (rootFolderId != null) {
        dispatch(fetchRootFolders({ id: rootFolderId, timeout: false }));
      } else {
        dispatch(fetchRootFolders());
      }
    });

    promise.fail((xhr) => {
      dispatch(batchActions([
        set({
          section,
          isImporting: false,
          isImported: true,
          importError: xhr
        }),

        ...addedIds.map((id) => updateItem({
          section,
          id
        }))
      ]));
    });
  }
});

//
// Reducers

export const reducers = createHandleActions({

  [CANCEL_LOOKUP_CHANNEL]: function(state) {
    queue.splice(0, queue.length);

    const items = state.items.map((item) => {
      if (item.isQueued) {
        return {
          ...item,
          isQueued: false
        };
      }

      return item;
    });

    return Object.assign({}, state, {
      isLookingUpChannel: false,
      items
    });
  },

  [CLEAR_IMPORT_CHANNEL]: function(state) {
    if (abortCurrentLookup) {
      abortCurrentLookup();

      abortCurrentLookup = null;
    }

    queue.splice(0, queue.length);

    return Object.assign({}, state, defaultState);
  },

  [SET_IMPORT_CHANNEL_VALUE]: function(state, { payload }) {
    const newState = getSectionState(state, section);
    const items = newState.items;
    const index = items.findIndex((item) => item.id === payload.id);

    newState.items = [...items];

    if (index >= 0) {
      const item = items[index];

      newState.items.splice(index, 1, { ...item, ...payload });
    } else {
      newState.items.push({ ...payload });
    }

    return updateSectionState(state, section, newState);
  }

}, defaultState, section);
