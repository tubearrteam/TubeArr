import moment from 'moment';
import { createAction } from 'redux-actions';
import { sortDirections } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import createHandleActions from './Creators/createHandleActions';
import createSetClientSideCollectionFilterReducer from './Creators/Reducers/createSetClientSideCollectionFilterReducer';
import createSetClientSideCollectionSortReducer from './Creators/Reducers/createSetClientSideCollectionSortReducer';
import createSetTableOptionReducer from './Creators/Reducers/createSetTableOptionReducer';
import { filterBuilderProps, filterPredicates, filters, sortPredicates } from './channelActions';

//
// Variables

export const section = 'channelIndex';

//
// State

export const defaultState = {
  sortKey: 'sortTitle',
  sortDirection: sortDirections.ASCENDING,
  secondarySortKey: 'sortTitle',
  secondarySortDirection: sortDirections.ASCENDING,
  view: 'posters',

  posterOptions: {
    detailedProgressBar: false,
    size: 'large',
    showTitle: false,
    showMonitored: true,
    showQualityProfile: true,
    showTags: false,
    showSearchAction: false
  },

  overviewOptions: {
    detailedProgressBar: false,
    size: 'medium',
    showMonitored: true,
    showNetwork: true,
    showQualityProfile: true,
    showPreviousAiring: false,
    showAdded: false,
    showPlaylistCount: true,
    showPath: false,
    showSizeOnDisk: false,
    showTags: false,
    showSearchAction: false
  },

  tableOptions: {
    showBanners: false,
    showSearchAction: false
  },

  columns: [
    {
      name: 'status',
      columnLabel: () => translate('Status'),
      isSortable: true,
      isVisible: true,
      isModifiable: false
    },
    {
      name: 'sortTitle',
      label: () => translate('ChannelTitle'),
      isSortable: true,
      isVisible: true,
      isModifiable: false
    },
    {
      name: 'channelType',
      label: () => translate('Type'),
      isSortable: true,
      isVisible: false
    },
    {
      name: 'network',
      label: () => translate('Network'),
      isSortable: true,
      isVisible: true
    },
    {
      name: 'qualityProfileId',
      label: () => translate('QualityProfile'),
      isSortable: true,
      isVisible: true
    },
    {
      name: 'nextAiring',
      label: () => translate('NextAiring'),
      isSortable: true,
      isVisible: true
    },
    {
      name: 'previousAiring',
      label: () => translate('PreviousAiring'),
      isSortable: true,
      isVisible: false
    },
    {
      name: 'originalLanguage',
      label: () => translate('OriginalLanguage'),
      isSortable: true,
      isVisible: false
    },
    {
      name: 'added',
      label: () => translate('Added'),
      isSortable: true,
      isVisible: false
    },
    {
      name: 'playlistCount',
      label: () => translate('Playlists'),
      isSortable: true,
      isVisible: true
    },
    {
      name: 'hasShortsTab',
      label: () => translate('HasShortsTab'),
      isSortable: true,
      isVisible: false
    },
    {
      name: 'playlistFolder',
      label: () => translate('PlaylistFolder'),
      isSortable: true,
      isVisible: false
    },
    {
      name: 'videoProgress',
      label: () => translate('Videos'),
      isSortable: true,
      isVisible: true
    },
    {
      name: 'videoCount',
      label: () => translate('VideoCount'),
      isSortable: true,
      isVisible: false
    },
    {
      name: 'latestPlaylist',
      label: () => translate('LatestPlaylist'),
      isSortable: true,
      isVisible: false
    },
    {
      name: 'year',
      label: () => translate('Year'),
      isSortable: true,
      isVisible: false
    },
    {
      name: 'path',
      label: () => translate('Path'),
      isSortable: true,
      isVisible: false
    },
    {
      name: 'sizeOnDisk',
      label: () => translate('SizeOnDisk'),
      isSortable: true,
      isVisible: false
    },
    {
      name: 'genres',
      label: () => translate('Genres'),
      isSortable: false,
      isVisible: false
    },
    {
      name: 'ratings',
      label: () => translate('Rating'),
      isSortable: true,
      isVisible: false
    },
    {
      name: 'certification',
      label: () => translate('Certification'),
      isSortable: false,
      isVisible: false
    },
    {
      name: 'releaseGroups',
      label: () => translate('ReleaseGroups'),
      isSortable: false,
      isVisible: false
    },
    {
      name: 'tags',
      label: () => translate('Tags'),
      isSortable: true,
      isVisible: false
    },
    {
      name: 'monitorNewItems',
      label: () => translate('MonitorNewPlaylists'),
      isSortable: true,
      isVisible: false
    },
    {
      name: 'actions',
      columnLabel: () => translate('Actions'),
      isVisible: true,
      isModifiable: false
    }
  ],

  sortPredicates: {
    ...sortPredicates,

    network: function(item) {
      const network = item.network;

      return network ? network.toLowerCase() : '';
    },

    nextAiring: function(item, direction) {
      const nextAiring = item.nextAiring;

      if (nextAiring) {
        return moment(nextAiring).unix();
      }

      if (direction === sortDirections.DESCENDING) {
        return 0;
      }

      return Number.MAX_VALUE;
    },

    previousAiring: function(item, direction) {
      const previousAiring = item.previousAiring;

      if (previousAiring) {
        return moment(previousAiring).unix();
      }

      if (direction === sortDirections.DESCENDING) {
        return -Number.MAX_VALUE;
      }

      return Number.MAX_VALUE;
    },

    videoProgress: function(item) {
      const { statistics = {} } = item;

      const {
        videoCount = 0,
        videoFileCount
      } = statistics;

      const progress = videoCount ? videoFileCount / videoCount * 100 : 100;

      return progress + videoCount / 1000000;
    },

    videoCount: function(item) {
      const { statistics = {} } = item;

      return statistics.totalVideoCount || 0;
    },

    playlistCount: function(item) {
      const { statistics = {} } = item;

      return statistics.playlistCount;
    },

    hasShortsTab: function(item) {
      return item.hasShortsTab === true ? 1 : 0;
    },

    originalLanguage: function(item) {
      const { originalLanguage = {} } = item;

      return originalLanguage.name;
    },

    ratings: function(item) {
      const { ratings = {} } = item;

      return ratings.value;
    },

    monitorNewItems: function(item) {
      return item.monitorNewItems === 'all' ? 1 : 0;
    }
  },

  selectedFilterKey: 'all',

  filters,

  filterPredicates,

  filterBuilderProps
};

export const persistState = [
  'channelIndex.sortKey',
  'channelIndex.sortDirection',
  'channelIndex.selectedFilterKey',
  'channelIndex.customFilters',
  'channelIndex.view',
  'channelIndex.columns',
  'channelIndex.posterOptions',
  'channelIndex.overviewOptions',
  'channelIndex.tableOptions'
];

//
// Actions Types

export const SET_CHANNEL_SORT = 'channelIndex/setChannelSort';
export const SET_CHANNEL_FILTER = 'channelIndex/setChannelFilter';
export const SET_CHANNEL_VIEW = 'channelIndex/setChannelView';
export const SET_CHANNEL_TABLE_OPTION = 'channelIndex/setChannelTableOption';
export const SET_CHANNEL_POSTER_OPTION = 'channelIndex/setChannelPosterOption';
export const SET_CHANNEL_OVERVIEW_OPTION = 'channelIndex/setChannelOverviewOption';

//
// Action Creators

export const setChannelSort = createAction(SET_CHANNEL_SORT);
export const setChannelFilter = createAction(SET_CHANNEL_FILTER);
export const setChannelView = createAction(SET_CHANNEL_VIEW);
export const setChannelTableOption = createAction(SET_CHANNEL_TABLE_OPTION);
export const setChannelPosterOption = createAction(SET_CHANNEL_POSTER_OPTION);
export const setChannelOverviewOption = createAction(SET_CHANNEL_OVERVIEW_OPTION);

//
// Reducers

export const reducers = createHandleActions({

  [SET_CHANNEL_SORT]: createSetClientSideCollectionSortReducer(section),
  [SET_CHANNEL_FILTER]: createSetClientSideCollectionFilterReducer(section),

  [SET_CHANNEL_VIEW]: function(state, { payload }) {
    return Object.assign({}, state, { view: payload.view });
  },

  [SET_CHANNEL_TABLE_OPTION]: createSetTableOptionReducer(section),

  [SET_CHANNEL_POSTER_OPTION]: function(state, { payload }) {
    const posterOptions = state.posterOptions;

    return {
      ...state,
      posterOptions: {
        ...posterOptions,
        ...payload
      }
    };
  },

  [SET_CHANNEL_OVERVIEW_OPTION]: function(state, { payload }) {
    const overviewOptions = state.overviewOptions;

    return {
      ...state,
      overviewOptions: {
        ...overviewOptions,
        ...payload
      }
    };
  }

}, defaultState, section);
