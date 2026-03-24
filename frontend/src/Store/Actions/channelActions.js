import _ from 'lodash';
import { createAction } from 'redux-actions';
import { batchActions } from 'redux-batched-actions';
import { filterBuilderTypes, filterBuilderValueTypes, filterTypePredicates, filterTypes, sortDirections } from 'Helpers/Props';
import { createThunk, handleThunks } from 'Store/thunks';
import sortByProp from 'Utilities/Array/sortByProp';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import dateFilterPredicate from 'Utilities/Date/dateFilterPredicate';
import translate from 'Utilities/String/translate';
import { set, updateItem } from './baseActions';
import createFetchHandler from './Creators/createFetchHandler';
import createHandleActions from './Creators/createHandleActions';
import createRemoveItemHandler from './Creators/createRemoveItemHandler';
import createSaveProviderHandler from './Creators/createSaveProviderHandler';
import { fetchVideos } from './videoActions';

//
// Local

const MONITOR_TIMEOUT = 1000;
const playlistsToUpdate = {};
const playlistMonitorToggleTimeouts = {};

//
// Variables

export const section = 'channels';

export const filters = [
  {
    key: 'all',
    label: () => translate('All'),
    filters: []
  },
  {
    key: 'monitored',
    label: () => translate('MonitoredOnly'),
    filters: [
      {
        key: 'monitored',
        value: true,
        type: filterTypes.EQUAL
      }
    ]
  },
  {
    key: 'unmonitored',
    label: () => translate('UnmonitoredOnly'),
    filters: [
      {
        key: 'monitored',
        value: false,
        type: filterTypes.EQUAL
      }
    ]
  },
  {
    key: 'continuing',
    label: () => translate('ContinuingOnly'),
    filters: [
      {
        key: 'status',
        value: 'continuing',
        type: filterTypes.EQUAL
      }
    ]
  },
  {
    key: 'ended',
    label: () => translate('EndedOnly'),
    filters: [
      {
        key: 'status',
        value: 'ended',
        type: filterTypes.EQUAL
      }
    ]
  },
  {
    key: 'missing',
    label: () => translate('MissingVideos'),
    filters: [
      {
        key: 'missing',
        value: true,
        type: filterTypes.EQUAL
      }
    ]
  }
];

export const filterPredicates = {
  videoProgress: function(item, filterValue, type) {
    const { statistics = {} } = item;

    const {
      videoCount = 0,
      videoFileCount
    } = statistics;

    const progress = videoCount ?
      videoFileCount / videoCount * 100 :
      100;

    const predicate = filterTypePredicates[type];

    return predicate(progress, filterValue);
  },

  missing: function(item) {
    const { statistics = {} } = item;

    return statistics.videoCount - statistics.videoFileCount > 0;
  },

  nextAiring: function(item, filterValue, type) {
    return dateFilterPredicate(item.nextAiring, filterValue, type);
  },

  previousAiring: function(item, filterValue, type) {
    return dateFilterPredicate(item.previousAiring, filterValue, type);
  },

  added: function(item, filterValue, type) {
    return dateFilterPredicate(item.added, filterValue, type);
  },

  ratings: function(item, filterValue, type) {
    const predicate = filterTypePredicates[type];
    const { value = 0 } = item.ratings;

    return predicate(value * 10, filterValue);
  },

  ratingVotes: function(item, filterValue, type) {
    const predicate = filterTypePredicates[type];
    const { votes = 0 } = item.ratings;

    return predicate(votes, filterValue);
  },

  originalLanguage: function(item, filterValue, type) {
    const predicate = filterTypePredicates[type];
    const { originalLanguage } = item;

    return predicate(originalLanguage ? originalLanguage.name : '', filterValue);
  },

  releaseGroups: function(item, filterValue, type) {
    const { statistics = {} } = item;

    const {
      releaseGroups = []
    } = statistics;

    const predicate = filterTypePredicates[type];

    return predicate(releaseGroups, filterValue);
  },

  playlistCount: function(item, filterValue, type) {
    const predicate = filterTypePredicates[type];
    const playlistCount = item.statistics ? item.statistics.playlistCount : 0;

    return predicate(playlistCount, filterValue);
  },

  sizeOnDisk: function(item, filterValue, type) {
    const predicate = filterTypePredicates[type];
    const sizeOnDisk = item.statistics && item.statistics.sizeOnDisk ?
      item.statistics.sizeOnDisk :
      0;

    return predicate(sizeOnDisk, filterValue);
  },

  hasMissingPlaylist: function(item, filterValue, type) {
    const predicate = filterTypePredicates[type];
    const playlists = item.playlists ?? [];

    const hasMissingPlaylist = playlists.some((playlist) => {
      const {
        playlistNumber,
        statistics = {}
      } = playlist;

      const {
        videoFileCount = 0,
        videoCount = 0,
        totalVideoCount = 0
      } = statistics;

      return (
        playlistNumber > 0 &&
        totalVideoCount > 0 &&
        videoCount === totalVideoCount &&
        videoFileCount === 0
      );
    });

    return predicate(hasMissingPlaylist, filterValue);
  },

  playlistsMonitoredStatus: function(item, filterValue, type) {
    const predicate = filterTypePredicates[type];
    const playlists = item.playlists ?? [];

    const { monitoredCount, unmonitoredCount } = playlists.reduce((acc, { playlistNumber, monitored }) => {
      if (playlistNumber <= 0) {
        return acc;
      }

      if (monitored) {
        acc.monitoredCount++;
      } else {
        acc.unmonitoredCount++;
      }

      return acc;
    }, { monitoredCount: 0, unmonitoredCount: 0 });

    let playlistsMonitoredStatus = 'partial';

    if (monitoredCount === 0) {
      playlistsMonitoredStatus = 'none';
    } else if (unmonitoredCount === 0) {
      playlistsMonitoredStatus = 'all';
    }

    return predicate(playlistsMonitoredStatus, filterValue);
  }
};

export const filterBuilderProps = [
  {
    name: 'monitored',
    label: () => translate('Monitored'),
    type: filterBuilderTypes.EXACT,
    valueType: filterBuilderValueTypes.BOOL
  },
  {
    name: 'status',
    label: () => translate('Status'),
    type: filterBuilderTypes.EXACT,
    valueType: filterBuilderValueTypes.CHANNEL_STATUS
  },
  {
    name: 'channelType',
    label: () => translate('Type'),
    type: filterBuilderTypes.EXACT,
    valueType: filterBuilderValueTypes.CHANNEL_TYPES
  },
  {
    name: 'title',
    label: () => translate('Title'),
    type: filterBuilderTypes.STRING
  },
  {
    name: 'network',
    label: () => translate('Network'),
    type: filterBuilderTypes.ARRAY,
    optionsSelector: function(items) {
      const tagList = items.reduce((acc, channel) => {
        if (channel.network) {
          acc.push({
            id: channel.network,
            name: channel.network
          });
        }

        return acc;
      }, []);

      return tagList.sort(sortByProp('name'));
    }
  },
  {
    name: 'qualityProfileId',
    label: () => translate('QualityProfile'),
    type: filterBuilderTypes.EXACT,
    valueType: filterBuilderValueTypes.QUALITY_PROFILE
  },
  {
    name: 'nextAiring',
    label: () => translate('NextAiring'),
    type: filterBuilderTypes.DATE,
    valueType: filterBuilderValueTypes.DATE
  },
  {
    name: 'previousAiring',
    label: () => translate('PreviousAiring'),
    type: filterBuilderTypes.DATE,
    valueType: filterBuilderValueTypes.DATE
  },
  {
    name: 'added',
    label: () => translate('Added'),
    type: filterBuilderTypes.DATE,
    valueType: filterBuilderValueTypes.DATE
  },
  {
    name: 'playlistCount',
    label: () => translate('PlaylistCount'),
    type: filterBuilderTypes.NUMBER
  },
  {
    name: 'videoProgress',
    label: () => translate('VideoProgress'),
    type: filterBuilderTypes.NUMBER
  },
  {
    name: 'path',
    label: () => translate('Path'),
    type: filterBuilderTypes.STRING
  },
  {
    name: 'rootFolderPath',
    label: () => translate('RootFolderPath'),
    type: filterBuilderTypes.EXACT
  },
  {
    name: 'sizeOnDisk',
    label: () => translate('SizeOnDisk'),
    type: filterBuilderTypes.NUMBER,
    valueType: filterBuilderValueTypes.BYTES
  },
  {
    name: 'genres',
    label: () => translate('Genres'),
    type: filterBuilderTypes.ARRAY,
    optionsSelector: function(items) {
      const tagList = items.reduce((acc, channel) => {
        channel.genres.forEach((genre) => {
          acc.push({
            id: genre,
            name: genre
          });
        });

        return acc;
      }, []);

      return tagList.sort(sortByProp('name'));
    }
  },
  {
    name: 'originalLanguage',
    label: () => translate('OriginalLanguage'),
    type: filterBuilderTypes.EXACT,
    optionsSelector: function(items) {
      const languageList = items.reduce((acc, channel) => {
        if (channel.originalLanguage) {
          acc.push({
            id: channel.originalLanguage.name,
            name: channel.originalLanguage.name
          });
        }

        return acc;
      }, []);

      return languageList.sort(sortByProp('name'));
    }
  },
  {
    name: 'releaseGroups',
    label: () => translate('ReleaseGroups'),
    type: filterBuilderTypes.ARRAY
  },
  {
    name: 'ratings',
    label: () => translate('Rating'),
    type: filterBuilderTypes.NUMBER
  },
  {
    name: 'ratingVotes',
    label: () => translate('RatingVotes'),
    type: filterBuilderTypes.NUMBER
  },
  {
    name: 'certification',
    label: () => translate('Certification'),
    type: filterBuilderTypes.EXACT
  },
  {
    name: 'tags',
    label: () => translate('Tags'),
    type: filterBuilderTypes.ARRAY,
    valueType: filterBuilderValueTypes.TAG
  },
  {
    name: 'hasMissingPlaylist',
    label: () => translate('HasMissingPlaylist'),
    type: filterBuilderTypes.EXACT,
    valueType: filterBuilderValueTypes.BOOL
  },
  {
    name: 'playlistsMonitoredStatus',
    label: () => translate('PlaylistsMonitoredStatus'),
    type: filterBuilderTypes.EXACT,
    valueType: filterBuilderValueTypes.PLAYLISTS_MONITORED_STATUS
  },
  {
    name: 'year',
    label: () => translate('Year'),
    type: filterBuilderTypes.NUMBER
  }
];

export const sortPredicates = {
  status: function(item) {
    let result = 0;

    if (item.monitored) {
      result += 2;
    }

    if (item.status === 'continuing') {
      result++;
    }

    return result;
  },

  sizeOnDisk: function(item) {
    const { statistics = {} } = item;

    return statistics.sizeOnDisk || 0;
  }
};

//
// State

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  error: null,
  isSaving: false,
  saveError: null,
  isDeleting: false,
  deleteError: null,
  items: [],
  sortKey: 'sortTitle',
  sortDirection: sortDirections.ASCENDING,
  pendingChanges: {},
  deleteOptions: {
    addImportListExclusion: false
  }
};

export const persistState = [
  'channels.deleteOptions'
];

//
// Actions Types

export const FETCH_CHANNELS = 'channels/fetchChannels';
export const SET_CHANNEL_VALUE = 'channels/setChannelValue';
export const SAVE_CHANNEL = 'channels/saveChannel';
export const DELETE_CHANNEL = 'channels/deleteChannel';

export const TOGGLE_CHANNEL_MONITORED = 'channels/toggleChannelMonitored';
export const TOGGLE_PLAYLIST_MONITORED = 'channels/togglePlaylistMonitored';
export const UPDATE_CHANNEL_MONITOR = 'channels/updateChannelMonitor';
export const SAVE_CHANNEL_EDITOR = 'channels/saveChannelEditor';
export const BULK_DELETE_CHANNELS = 'channels/bulkDeleteChannels';

export const SET_DELETE_OPTION = 'channels/setDeleteOption';

//
// Action Creators

export const fetchChannels = createThunk(FETCH_CHANNELS);
export const saveChannel = createThunk(SAVE_CHANNEL, (payload) => {
  const newPayload = {
    ...payload
  };

  if (payload.moveFiles) {
    newPayload.queryParams = {
      moveFiles: true
    };
  }

  delete newPayload.moveFiles;

  return newPayload;
});

export const deleteChannel = createThunk(DELETE_CHANNEL, (payload) => {
  return {
    ...payload,
    queryParams: {
      deleteFiles: payload.deleteFiles,
      addImportListExclusion: payload.addImportListExclusion
    }
  };
});

export const toggleChannelMonitored = createThunk(TOGGLE_CHANNEL_MONITORED);
export const togglePlaylistMonitored = createThunk(TOGGLE_PLAYLIST_MONITORED);
export const updateChannelMonitor = createThunk(UPDATE_CHANNEL_MONITOR);
export const saveChannelEditor = createThunk(SAVE_CHANNEL_EDITOR);
export const bulkDeleteChannels = createThunk(BULK_DELETE_CHANNELS);

export const setChannelValue = createAction(SET_CHANNEL_VALUE, (payload) => {
  return {
    section,
    ...payload
  };
});

export const setDeleteOption = createAction(SET_DELETE_OPTION);

//
// Helpers

function getSaveAjaxOptions({ ajaxOptions, payload }) {
  if (payload.moveFolder) {
    ajaxOptions.url = `${ajaxOptions.url}?moveFolder=true`;
  }

  return ajaxOptions;
}

//
// Action Handlers

export const actionHandlers = handleThunks({

  [FETCH_CHANNELS]: createFetchHandler(section, '/channels'),
  [SAVE_CHANNEL]: createSaveProviderHandler(section, '/channels', { getAjaxOptions: getSaveAjaxOptions }),
  [DELETE_CHANNEL]: createRemoveItemHandler(section, '/channels'),

  [TOGGLE_CHANNEL_MONITORED]: (getState, payload, dispatch) => {
    const {
      channelId: id,
      monitored
    } = payload;

    const channel = _.find(getState().channels.items, { id });

    dispatch(updateItem({
      id,
      section,
      isSaving: true,
      monitored
    }));

    const promise = createAjaxRequest({
      url: `/channels/${id}`,
      method: 'PUT',
      data: JSON.stringify({
        ...channel,
        monitored
      }),
      dataType: 'json'
    }).request;

    promise.done((data) => {
      dispatch(updateItem({
        id,
        section,
        isSaving: false,
        monitored
      }));
    });

    promise.fail((xhr) => {
      dispatch(updateItem({
        id,
        section,
        isSaving: false,
        monitored: !monitored
      }));
    });
  },

  [TOGGLE_PLAYLIST_MONITORED]: function(getState, payload, dispatch) {
    const {
      channelId: id,
      playlistNumber,
      monitored
    } = payload;

    const playlistMonitorToggleTimeout = playlistMonitorToggleTimeouts[id];

    if (playlistMonitorToggleTimeout) {
      clearTimeout(playlistMonitorToggleTimeout);
      delete playlistMonitorToggleTimeouts[id];
    }

    const channel = getState().channels.items.find((c) => c.id === id);
    const playlists = _.cloneDeep(channel.playlists ?? []);
    const playlist = playlists.find((p) => p.playlistNumber === playlistNumber);

    playlist.monitored = monitored;
    playlist.isSaving = true;

    dispatch(updateItem({
      id,
      section,
      playlists
    }));

    playlistsToUpdate[playlistNumber] = monitored;

    playlistMonitorToggleTimeouts[id] = setTimeout(() => {
      createAjaxRequest({
        url: `/channels/${id}`,
        method: 'PUT',
        data: JSON.stringify({
          ...channel,
          playlists
        }),
        dataType: 'json'
      }).request.then(
        (data) => {
          const changedPlaylists = [];

          data.playlists.forEach((s) => {
            if (playlistsToUpdate.hasOwnProperty(s.playlistNumber)) {
              if (s.monitored === playlistsToUpdate[s.playlistNumber]) {
                changedPlaylists.push(s);
              } else {
                s.isSaving = true;
              }
            }
          });

          const videosToUpdate = getState().videos.items.reduce((acc, video) => {
            if (video.channelId !== data.id) {
              return acc;
            }

            const changedPlaylist = changedPlaylists.find((s) => s.playlistNumber === video.playlistNumber);

            if (!changedPlaylist) {
              return acc;
            }

            acc.push(updateItem({
              id: video.id,
              section: 'videos',
              monitored: changedPlaylist.monitored
            }));

            return acc;
          }, []);

          dispatch(batchActions([
            updateItem({
              id,
              section,
              ...data
            }),

            ...videosToUpdate
          ]));

          changedPlaylists.forEach((s) => {
            delete playlistsToUpdate[s.playlistNumber];
          });
        },
        (xhr) => {
          dispatch(updateItem({
            id,
            section,
            playlists: channel.playlists
          }));

          Object.keys(playlistsToUpdate).forEach((s) => {
            delete playlistsToUpdate[s];
          });
        });
    }, MONITOR_TIMEOUT);
  },

  [UPDATE_CHANNEL_MONITOR]: function(getState, payload, dispatch) {
    const {
      channelIds,
      monitor,
      roundRobinLatestVideoCount,
      shouldFetchVideosAfterUpdate = false
    } = payload;

    if (!channelIds?.length || !monitor || monitor === 'noChange') {
      return;
    }

    if (monitor === 'roundRobin') {
      const cap = Number(roundRobinLatestVideoCount);
      if (!Number.isFinite(cap) || cap <= 0) {
        return;
      }
    }

    dispatch(set({
      section,
      isSaving: true
    }));

    const body = {
      channelIds,
      monitor,
      ...(monitor === 'roundRobin'
        ? { roundRobinLatestVideoCount: Number(roundRobinLatestVideoCount) }
        : {})
    };

    const promise = createAjaxRequest({
      url: '/channels/bulk/monitoring',
      method: 'POST',
      data: JSON.stringify(body),
      dataType: 'json'
    }).request;

    promise.done(() => {
      dispatch(fetchChannels());
      if (shouldFetchVideosAfterUpdate && channelIds.length === 1) {
        dispatch(fetchVideos({ channelId: channelIds[0] }));
      } else {
        dispatch(fetchVideos());
      }

      dispatch(set({
        section,
        isSaving: false,
        saveError: null
      }));
    });

    promise.fail((xhr) => {
      dispatch(set({
        section,
        isSaving: false,
        saveError: xhr
      }));
    });
  },

  [SAVE_CHANNEL_EDITOR]: function(getState, payload, dispatch) {
    dispatch(set({
      section,
      isSaving: true
    }));

    const promise = createAjaxRequest({
      url: '/channels/editor',
      method: 'PUT',
      data: JSON.stringify(payload),
      dataType: 'json'
    }).request;

    promise.done((data) => {
      dispatch(batchActions([
        ...data.map((channel) => {

          const {
            alternateTitles,
            images,
            rootFolderPath,
            statistics,
            ...propsToUpdate
          } = channel;

          return updateItem({
            id: channel.id,
            section: 'channels',
            ...propsToUpdate
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
  },

  [BULK_DELETE_CHANNELS]: function(getState, payload, dispatch) {
    dispatch(set({
      section,
      isDeleting: true
    }));

    const promise = createAjaxRequest({
      url: '/channels/editor',
      method: 'DELETE',
      data: JSON.stringify(payload),
      dataType: 'json'
    }).request;

    promise.done(() => {
      // SignaR will take care of removing the channel from the collection

      dispatch(set({
        section,
        isDeleting: false,
        deleteError: null
      }));
    });

    promise.fail((xhr) => {
      dispatch(set({
        section,
        isDeleting: false,
        deleteError: xhr
      }));
    });
  }
});

//
// Reducers

export const reducers = createHandleActions({

  [SET_CHANNEL_VALUE]: (state, { payload }) => {
    if (section !== payload.section) return state;
    const { name, value, id: channelId } = payload;
    const newState = Object.assign({}, state);
    const prevPending = newState.pendingChanges || {};
    const item = channelId != null ? _.find(state.items, { id: channelId }) : null;
    const currentValue = item && item[name] !== undefined ? item[name] : null;
    let parsedValue = value;
    if (_.isNumber(currentValue) && value != null) {
      parsedValue = parseInt(value, 10);
    } else if (value != null && typeof value === 'string' && /^\d+$/.test(value) && (name === 'qualityProfileId' || name === 'monitorNewItems')) {
      parsedValue = parseInt(value, 10);
    }
    if (channelId != null) {
      const keyed = Object.assign({}, prevPending[channelId]);
      if (currentValue === parsedValue) {
        delete keyed[name];
      } else {
        keyed[name] = parsedValue;
      }
      const nextPending = Object.assign({}, prevPending);
      if (Object.keys(keyed).length === 0) {
        delete nextPending[channelId];
      } else {
        nextPending[channelId] = keyed;
      }
      newState.pendingChanges = nextPending;
    } else {
      const nextPending = Object.assign({}, prevPending);
      if (currentValue === parsedValue) {
        delete nextPending[name];
      } else {
        nextPending[name] = parsedValue;
      }
      newState.pendingChanges = nextPending;
    }
    return newState;
  },

  [SET_DELETE_OPTION]: (state, { payload }) => {
    return {
      ...state,
      deleteOptions: {
        ...payload
      }
    };
  }

}, defaultState, section);
