import { createAction } from 'redux-actions';
import { handleThunks } from 'Store/thunks';
import createHandleActions from './Creators/createHandleActions';
import autoTaggings from './Settings/autoTaggings';
import autoTaggingSpecifications from './Settings/autoTaggingSpecifications';
import customFormats from './Settings/customFormats';
import customFormatSpecifications from './Settings/customFormatSpecifications';
import delayProfiles from './Settings/delayProfiles';
import general from './Settings/general';
import importListExclusions from './Settings/importListExclusions';
import importListOptions from './Settings/importListOptions';
import importLists from './Settings/importLists';
import languages from './Settings/languages';
import mediaManagement from './Settings/mediaManagement';
import metadata from './Settings/metadata';
import naming from './Settings/naming';
import namingExamples from './Settings/namingExamples';
import notifications from './Settings/notifications';
import plexProvider from './Settings/plexProvider';
import qualityDefinitions from './Settings/qualityDefinitions';
import qualityProfiles from './Settings/qualityProfiles';
import remotePathMappings from './Settings/remotePathMappings';
import ui from './Settings/ui';
import ytdlp from './Settings/ytdlp';
import ffmpeg from './Settings/ffmpeg';
import youtube from './Settings/youtube';

export * from './Settings/autoTaggingSpecifications';
export * from './Settings/autoTaggings';
export * from './Settings/customFormatSpecifications.js';
export * from './Settings/customFormats';
export * from './Settings/delayProfiles';
export * from './Settings/general';
export * from './Settings/importListOptions';
export * from './Settings/importLists';
export * from './Settings/importListExclusions';
export * from './Settings/languages';
export * from './Settings/mediaManagement';
export * from './Settings/metadata';
export * from './Settings/naming';
export * from './Settings/namingExamples';
export * from './Settings/notifications';
export * from './Settings/plexProvider';
export * from './Settings/qualityDefinitions';
export * from './Settings/qualityProfiles';
export * from './Settings/remotePathMappings';
export * from './Settings/ui';
export * from './Settings/ytdlp';
export * from './Settings/ffmpeg';
export * from './Settings/youtube';

//
// Variables

export const section = 'settings';

//
// State

export const defaultState = {
  advancedSettings: false,
  autoTaggingSpecifications: autoTaggingSpecifications.defaultState,
  autoTaggings: autoTaggings.defaultState,
  customFormatSpecifications: customFormatSpecifications.defaultState,
  customFormats: customFormats.defaultState,
  delayProfiles: delayProfiles.defaultState,
  general: general.defaultState,
  importLists: importLists.defaultState,
  importListExclusions: importListExclusions.defaultState,
  importListOptions: importListOptions.defaultState,
  languages: languages.defaultState,
  mediaManagement: mediaManagement.defaultState,
  metadata: metadata.defaultState,
  naming: naming.defaultState,
  namingExamples: namingExamples.defaultState,
  notifications: notifications.defaultState,
  plexProvider: plexProvider.defaultState,
  qualityDefinitions: qualityDefinitions.defaultState,
  qualityProfiles: qualityProfiles.defaultState,
  remotePathMappings: remotePathMappings.defaultState,
  ui: ui.defaultState,
  ytdlp: ytdlp.defaultState,
  ffmpeg: ffmpeg.defaultState,
  youtube: youtube.defaultState
};

export const persistState = [
  'settings.advancedSettings',
  'settings.importListExclusions.pageSize'
];

//
// Actions Types

export const TOGGLE_ADVANCED_SETTINGS = 'settings/toggleAdvancedSettings';

//
// Action Creators

export const toggleAdvancedSettings = createAction(TOGGLE_ADVANCED_SETTINGS);

//
// Action Handlers

export const actionHandlers = handleThunks({
  ...autoTaggingSpecifications.actionHandlers,
  ...autoTaggings.actionHandlers,
  ...customFormatSpecifications.actionHandlers,
  ...customFormats.actionHandlers,
  ...delayProfiles.actionHandlers,
  ...general.actionHandlers,
  ...importLists.actionHandlers,
  ...importListExclusions.actionHandlers,
  ...importListOptions.actionHandlers,
  ...languages.actionHandlers,
  ...mediaManagement.actionHandlers,
  ...metadata.actionHandlers,
  ...naming.actionHandlers,
  ...namingExamples.actionHandlers,
  ...notifications.actionHandlers,
  ...plexProvider.actionHandlers,
  ...qualityDefinitions.actionHandlers,
  ...qualityProfiles.actionHandlers,
  ...remotePathMappings.actionHandlers,
  ...ui.actionHandlers,
  ...ytdlp.actionHandlers,
  ...ffmpeg.actionHandlers,
  ...youtube.actionHandlers
});

//
// Reducers

export const reducers = createHandleActions({

  [TOGGLE_ADVANCED_SETTINGS]: (state, { payload }) => {
    return Object.assign({}, state, { advancedSettings: !state.advancedSettings });
  },

  ...autoTaggingSpecifications.reducers,
  ...autoTaggings.reducers,
  ...customFormatSpecifications.reducers,
  ...customFormats.reducers,
  ...delayProfiles.reducers,
  ...general.reducers,
  ...importLists.reducers,
  ...importListExclusions.reducers,
  ...importListOptions.reducers,
  ...languages.reducers,
  ...mediaManagement.reducers,
  ...metadata.reducers,
  ...naming.reducers,
  ...namingExamples.reducers,
  ...notifications.reducers,
  ...plexProvider.reducers,
  ...qualityDefinitions.reducers,
  ...qualityProfiles.reducers,
  ...remotePathMappings.reducers,
  ...ui.reducers,
  ...ytdlp.reducers,
  ...ffmpeg.reducers,
  ...youtube.reducers

}, defaultState, section);
