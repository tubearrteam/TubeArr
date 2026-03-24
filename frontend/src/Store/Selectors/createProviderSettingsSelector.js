import _ from 'lodash';
import { createSelector } from 'reselect';
import selectSettings from 'Store/Selectors/selectSettings';

function selector(id, section) {
  if (!section) {
    return {
      isFetching: false,
      isPopulated: false,
      error: null,
      isSaving: false,
      saveError: null,
      isTesting: false,
      pendingChanges: {},
      schema: null,
      settings: {},
      item: {},
      validationErrors: [],
      validationWarnings: [],
      hasPendingChanges: false,
      hasSettings: false
    };
  }

  if (!id) {
    const item = _.isArray(section.schema) ? section.selectedSchema : section.schema;
    const settings = selectSettings(Object.assign({ name: '' }, item ?? {}), section.pendingChanges ?? {}, section.saveError);

    const {
      isSchemaFetching: isFetching,
      isSchemaPopulated: isPopulated,
      schemaError: error,
      isSaving,
      saveError,
      isTesting,
      pendingChanges
    } = section;

    return {
      isFetching,
      isPopulated,
      error,
      isSaving,
      saveError,
      isTesting,
      pendingChanges,
      schema: section.schema,
      ...settings,
      item: settings.settings
    };
  }

  const {
    isFetching,
    isPopulated,
    error,
    isSaving,
    saveError,
    isTesting,
    pendingChanges
  } = section;

  const items = section.items;
  const found = Array.isArray(items) ? _.find(items, { id }) : undefined;
  const settings = selectSettings(found ?? {}, pendingChanges ?? {}, saveError);

  return {
    isFetching,
    isPopulated,
    error,
    isSaving,
    saveError,
    isTesting,
    schema: section.schema,
    ...settings,
    item: settings.settings
  };
}

export default function createProviderSettingsSelector(sectionName) {
  return createSelector(
    (state, { id }) => id,
    (state) => state.settings[sectionName],
    (id, section) => {
    try {
      return selector(id, section);
    } catch (err) {
      if (process.env.NODE_ENV !== 'production') {
        console.error('createProviderSettingsSelector failed', err);
      }
      return {
        isFetching: false,
        isPopulated: false,
        error: null,
        isSaving: false,
        saveError: null,
        isTesting: false,
        pendingChanges: {},
        schema: null,
        settings: {},
        item: {},
        validationErrors: [],
        validationWarnings: [],
        hasPendingChanges: false,
        hasSettings: false
      };
    }
  }
  );
}

export function createProviderSettingsSelectorHook(sectionName, id) {
  return createSelector(
    (state) => state.settings[sectionName],
    (section) => {
    try {
      return selector(id, section);
    } catch (err) {
      if (process.env.NODE_ENV !== 'production') {
        console.error('createProviderSettingsSelectorHook failed', err);
      }
      return {
        isFetching: false,
        isPopulated: false,
        error: null,
        isSaving: false,
        saveError: null,
        isTesting: false,
        pendingChanges: {},
        schema: null,
        settings: {},
        item: {},
        validationErrors: [],
        validationWarnings: [],
        hasPendingChanges: false,
        hasSettings: false
      };
    }
  }
  );
}

