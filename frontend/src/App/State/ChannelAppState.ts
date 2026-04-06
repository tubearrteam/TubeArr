import AppSectionState, {
  AppSectionDeleteState,
  AppSectionSaveState,
} from 'App/State/AppSectionState';
import Column from 'Components/Table/Column';
import { SortDirection } from 'Helpers/Props/sortDirections';
import Channel from 'Channel/Channel';
import { Filter, FilterBuilderProp } from './AppState';

export interface ChannelIndexAppState {
  sortKey: string;
  sortDirection: SortDirection;
  secondarySortKey: string;
  secondarySortDirection: SortDirection;
  view: string;

  posterOptions: {
    detailedProgressBar: boolean;
    size: string;
    showTitle: boolean;
    showMonitored: boolean;
    showQualityProfile: boolean;
    showTags: boolean;
    showSearchAction: boolean;
  };

  overviewOptions: {
    detailedProgressBar: boolean;
    size: string;
    showMonitored: boolean;
    showNetwork: boolean;
    showQualityProfile: boolean;
    showPreviousAiring: boolean;
    showAdded: boolean;
    showPlaylistCount: boolean;
    showPath: boolean;
    showSizeOnDisk: boolean;
    showTags: boolean;
    showSearchAction: boolean;
  };

  tableOptions: {
    showBanners: boolean;
    showSearchAction: boolean;
  };

  selectedFilterKey: string;
  filterBuilderProps: FilterBuilderProp<Channel>[];
  filters: Filter[];
  columns: Column[];
}

interface ChannelAppState
  extends AppSectionState<Channel>,
    AppSectionDeleteState,
    AppSectionSaveState {
  itemMap: Record<number, number>;

  /** Flat changes (legacy/bulk) or keyed by channel id for single-channel edit modal */
  pendingChanges: Partial<Channel> | Record<number, Partial<Channel>>;
}

export default ChannelAppState;
