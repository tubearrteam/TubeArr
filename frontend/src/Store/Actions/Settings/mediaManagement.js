import { batchActions } from 'redux-batched-actions';
import { createAction } from 'redux-actions';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import getSectionState from 'Utilities/State/getSectionState';
import createFetchHandler from 'Store/Actions/Creators/createFetchHandler';
import createSetSettingValueReducer from 'Store/Actions/Creators/Reducers/createSetSettingValueReducer';
import { set, update } from '../baseActions';
import { createThunk } from 'Store/thunks';

//
// Variables

const section = 'settings.mediaManagement';

//
// Action Types

export const FETCH_MEDIA_MANAGEMENT_SETTINGS = 'settings/mediaManagement/fetchMediaManagementSettings';
export const SAVE_MEDIA_MANAGEMENT_SETTINGS = 'settings/mediaManagement/saveMediaManagementSettings';
export const SET_MEDIA_MANAGEMENT_SETTINGS_VALUE = 'settings/mediaManagement/setMediaManagementSettingsValue';
export const REMOVE_MANAGED_NFOS_FROM_LIBRARY = 'settings/mediaManagement/removeManagedNfosFromLibrary';

//
// Action Creators

export const fetchMediaManagementSettings = createThunk(FETCH_MEDIA_MANAGEMENT_SETTINGS);
export const saveMediaManagementSettings = createThunk(SAVE_MEDIA_MANAGEMENT_SETTINGS);
export const removeManagedNfosFromLibrary = createThunk(REMOVE_MANAGED_NFOS_FROM_LIBRARY);
export const setMediaManagementSettingsValue = createAction(SET_MEDIA_MANAGEMENT_SETTINGS_VALUE, (payload) => {
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
    item: {},
    isRemovingManagedNfos: false,
    managedNfoRemovalError: null,
    lastManagedNfoRemoval: null
  },

  //
  // Action Handlers

  actionHandlers: {
    [FETCH_MEDIA_MANAGEMENT_SETTINGS]: createFetchHandler(section, '/config/mediamanagement'),

    [SAVE_MEDIA_MANAGEMENT_SETTINGS](getState, payload, dispatch) {
      const stripAfter = payload && payload.removeManagedNfosAfterSave === true;
      dispatch(set({ section, isSaving: true }));

      const state = getSectionState(getState(), section, true);
      const saveData = Object.assign({}, state.item, state.pendingChanges);

      const { request } = createAjaxRequest({
        url: '/config/mediamanagement',
        method: 'PUT',
        dataType: 'json',
        data: JSON.stringify(saveData)
      });

      request.done((data) => {
        dispatch(batchActions([
          update({ section, data }),
          set({
            section,
            isSaving: false,
            saveError: null,
            pendingChanges: {}
          })
        ]));

        if (stripAfter) {
          const { request: postReq } = createAjaxRequest({
            url: '/config/mediamanagement/remove-managed-nfos',
            method: 'POST',
            contentType: 'application/json',
            data: '{}'
          });
          postReq.done((result) => {
            dispatch(set({ section, lastManagedNfoRemoval: result }));
          });
          postReq.fail((xhr) => {
            dispatch(set({ section, managedNfoRemovalError: xhr }));
          });
        }
      });

      request.fail((xhr) => {
        dispatch(set({
          section,
          isSaving: false,
          saveError: xhr
        }));
      });
    },

    [REMOVE_MANAGED_NFOS_FROM_LIBRARY](getState, payload, dispatch) {
      dispatch(set({ section, isRemovingManagedNfos: true, managedNfoRemovalError: null }));

      const { request } = createAjaxRequest({
        url: '/config/mediamanagement/remove-managed-nfos',
        method: 'POST',
        contentType: 'application/json',
        data: '{}'
      });

      request.done((data) => {
        dispatch(set({
          section,
          isRemovingManagedNfos: false,
          lastManagedNfoRemoval: data
        }));
      });

      request.fail((xhr) => {
        dispatch(set({
          section,
          isRemovingManagedNfos: false,
          managedNfoRemovalError: xhr
        }));
      });
    }
  },

  //
  // Reducers

  reducers: {
    [SET_MEDIA_MANAGEMENT_SETTINGS_VALUE]: createSetSettingValueReducer(section)
  }

};
