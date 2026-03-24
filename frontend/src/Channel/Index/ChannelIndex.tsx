import React, {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { SelectProvider } from 'App/SelectContext';
import ClientSideCollectionAppState from 'App/State/ClientSideCollectionAppState';
import ChannelAppState, { ChannelIndexAppState } from 'App/State/ChannelAppState';
import { RSS_SYNC } from 'Commands/commandNames';
import Alert from 'Components/Alert';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageJumpBar from 'Components/Page/PageJumpBar';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import PageToolbarSeparator from 'Components/Page/Toolbar/PageToolbarSeparator';
import TableOptionsModalWrapper from 'Components/Table/TableOptions/TableOptionsModalWrapper';
import withScrollPosition from 'Components/withScrollPosition';
import { align, icons, kinds } from 'Helpers/Props';
import { DESCENDING } from 'Helpers/Props/sortDirections';
import ParseToolbarButton from 'Parse/ParseToolbarButton';
import NoChannel from 'Channel/NoChannel';
import { executeCommand } from 'Store/Actions/commandActions';
import { fetchQueueDetails } from 'Store/Actions/queueActions';
import { fetchChannels } from 'Store/Actions/channelActions';
import {
  setChannelFilter,
  setChannelSort,
  setChannelTableOption,
  setChannelView,
} from 'Store/Actions/channelIndexActions';
import scrollPositions from 'Store/scrollPositions';
import createCommandExecutingSelector from 'Store/Selectors/createCommandExecutingSelector';
import createDimensionsSelector from 'Store/Selectors/createDimensionsSelector';
import createChannelClientSideCollectionItemsSelector from 'Store/Selectors/createChannelClientSideCollectionItemsSelector';
import translate from 'Utilities/String/translate';
import ChannelIndexFilterMenu from './Menus/ChannelIndexFilterMenu';
import ChannelIndexSortMenu from './Menus/ChannelIndexSortMenu';
import ChannelIndexViewMenu from './Menus/ChannelIndexViewMenu';
import ChannelIndexOverviewOptionsModal from './Overview/Options/ChannelIndexOverviewOptionsModal';
import ChannelIndexOverviews from './Overview/ChannelIndexOverviews';
import ChannelIndexPosterOptionsModal from './Posters/Options/ChannelIndexPosterOptionsModal';
import ChannelIndexPosters from './Posters/ChannelIndexPosters';
import ChannelIndexSelectAllButton from './Select/ChannelIndexSelectAllButton';
import ChannelIndexSelectAllMenuItem from './Select/ChannelIndexSelectAllMenuItem';
import ChannelIndexSelectFooter from './Select/ChannelIndexSelectFooter';
import ChannelIndexSelectModeButton from './Select/ChannelIndexSelectModeButton';
import ChannelIndexSelectModeMenuItem from './Select/ChannelIndexSelectModeMenuItem';
import ChannelIndexFooter from './ChannelIndexFooter';
import ChannelIndexRefreshChannelButton from './ChannelIndexRefreshChannelButton';
import ChannelIndexTable from './Table/ChannelIndexTable';
import ChannelIndexTableOptions from './Table/ChannelIndexTableOptions';
import styles from './ChannelIndex.css';

function getViewComponent(view: string) {
  if (view === 'posters') {
    return ChannelIndexPosters;
  }

  if (view === 'overview') {
    return ChannelIndexOverviews;
  }

  return ChannelIndexTable;
}

interface ChannelIndexProps {
  initialScrollTop?: number;
}

const ChannelIndex = withScrollPosition((props: ChannelIndexProps) => {
  const {
    isFetching,
    isPopulated,
    error,
    totalItems,
    items,
    columns,
    selectedFilterKey,
    filters,
    customFilters,
    sortKey,
    sortDirection,
    view,
  }: ChannelAppState & ChannelIndexAppState & ClientSideCollectionAppState =
    useSelector(createChannelClientSideCollectionItemsSelector('channelIndex'));

  const isRssSyncExecuting = useSelector(
    createCommandExecutingSelector(RSS_SYNC)
  );
  const { isSmallScreen } = useSelector(createDimensionsSelector());
  const dispatch = useDispatch();
  const scrollerRef = useRef<HTMLDivElement>(null);
  const [isOptionsModalOpen, setIsOptionsModalOpen] = useState(false);
  const [jumpToCharacter, setJumpToCharacter] = useState<string | undefined>(
    undefined
  );
  const [isSelectMode, setIsSelectMode] = useState(false);

  useEffect(() => {
    dispatch(fetchChannels());
    dispatch(fetchQueueDetails({ all: true }));
  }, [dispatch]);

  const onRssSyncPress = useCallback(() => {
    dispatch(
      executeCommand({
        name: RSS_SYNC,
      })
    );
  }, [dispatch]);

  const onSelectModePress = useCallback(() => {
    setIsSelectMode(!isSelectMode);
  }, [isSelectMode, setIsSelectMode]);

  const onTableOptionChange = useCallback(
    (payload: unknown) => {
      dispatch(setChannelTableOption(payload));
    },
    [dispatch]
  );

  const onViewSelect = useCallback(
    (value: string) => {
      dispatch(setChannelView({ view: value }));

      if (scrollerRef.current) {
        scrollerRef.current.scrollTo(0, 0);
      }
    },
    [scrollerRef, dispatch]
  );

  const onSortSelect = useCallback(
    (value: string) => {
      dispatch(setChannelSort({ sortKey: value }));
    },
    [dispatch]
  );

  const onFilterSelect = useCallback(
    (value: string) => {
      dispatch(setChannelFilter({ selectedFilterKey: value }));
    },
    [dispatch]
  );

  const onOptionsPress = useCallback(() => {
    setIsOptionsModalOpen(true);
  }, [setIsOptionsModalOpen]);

  const onOptionsModalClose = useCallback(() => {
    setIsOptionsModalOpen(false);
  }, [setIsOptionsModalOpen]);

  const onJumpBarItemPress = useCallback(
    (character: string) => {
      setJumpToCharacter(character);
    },
    [setJumpToCharacter]
  );

  const onScroll = useCallback(
    ({ scrollTop }: { scrollTop: number }) => {
      setJumpToCharacter(undefined);
      scrollPositions.channelIndex = scrollTop;
    },
    [setJumpToCharacter]
  );

  const jumpBarItems = useMemo(() => {
    // Reset if not sorting by sortTitle
    if (sortKey !== 'sortTitle') {
      return {
        order: [],
      };
    }

    const characters = items.reduce((acc: Record<string, number>, item) => {
      const sortTitle = String(item.sortTitle ?? item.title ?? '').trim();

      if (!sortTitle) {
        return acc;
      }

      let char = sortTitle.charAt(0);

      if (!isNaN(Number(char))) {
        char = '#';
      }

      if (char in acc) {
        acc[char] = acc[char] + 1;
      } else {
        acc[char] = 1;
      }

      return acc;
    }, {});

    const order = Object.keys(characters).sort();

    // Reverse if sorting descending
    if (sortDirection === DESCENDING) {
      order.reverse();
    }

    return {
      characters,
      order,
    };
  }, [items, sortKey, sortDirection]);
  const ViewComponent = useMemo(() => getViewComponent(view), [view]);

  const isLoaded = !!(!error && isPopulated && items.length);
  const hasNoChannels = !totalItems;

  return (
    <SelectProvider items={items}>
      <PageContent>
        <PageToolbar>
          <PageToolbarSection>
            <ChannelIndexRefreshChannelButton
              isSelectMode={isSelectMode}
              selectedFilterKey={selectedFilterKey}
            />

            <PageToolbarButton
              label={translate('RssSync')}
              iconName={icons.RSS}
              isSpinning={isRssSyncExecuting}
              isDisabled={hasNoChannels}
              onPress={onRssSyncPress}
            />

            <PageToolbarSeparator />

            <ChannelIndexSelectModeButton
              label={
                isSelectMode
                  ? translate('StopSelecting')
                  : translate('SelectChannel')
              }
              iconName={isSelectMode ? icons.CHANNEL_ENDED : icons.CHECK}
              isSelectMode={isSelectMode}
              overflowComponent={ChannelIndexSelectModeMenuItem}
              onPress={onSelectModePress}
            />

            <ChannelIndexSelectAllButton
              label={translate('SelectAll')}
              isSelectMode={isSelectMode}
              overflowComponent={ChannelIndexSelectAllMenuItem}
            />

            <PageToolbarSeparator />
            <ParseToolbarButton />
          </PageToolbarSection>

          <PageToolbarSection
            alignContent={align.RIGHT}
            collapseButtons={false}
          >
            {view === 'table' ? (
              <TableOptionsModalWrapper
                columns={columns}
                optionsComponent={ChannelIndexTableOptions}
                onTableOptionChange={onTableOptionChange}
              >
                <PageToolbarButton
                  label={translate('Options')}
                  iconName={icons.TABLE}
                />
              </TableOptionsModalWrapper>
            ) : (
              <PageToolbarButton
                label={translate('Options')}
                iconName={view === 'posters' ? icons.POSTER : icons.OVERVIEW}
                isDisabled={hasNoChannels}
                onPress={onOptionsPress}
              />
            )}

            <PageToolbarSeparator />

            <ChannelIndexViewMenu
              view={view}
              isDisabled={hasNoChannels}
              onViewSelect={onViewSelect}
            />

            <ChannelIndexSortMenu
              sortKey={sortKey}
              sortDirection={sortDirection}
              isDisabled={hasNoChannels}
              onSortSelect={onSortSelect}
            />

            <ChannelIndexFilterMenu
              selectedFilterKey={selectedFilterKey}
              filters={filters}
              customFilters={customFilters}
              isDisabled={hasNoChannels}
              onFilterSelect={onFilterSelect}
            />
          </PageToolbarSection>
        </PageToolbar>
        <div className={styles.pageContentBodyWrapper}>
          <PageContentBody
            ref={scrollerRef}
            className={styles.contentBody}
            // eslint-disable-next-line @typescript-eslint/ban-ts-comment
            // @ts-ignore
            innerClassName={styles[`${view}InnerContentBody`]}
            initialScrollTop={props.initialScrollTop}
            onScroll={onScroll}
          >
            {isFetching && !isPopulated ? <LoadingIndicator /> : null}

            {!isFetching && !!error ? (
              <Alert kind={kinds.DANGER}>{translate('ChannelLoadError')}</Alert>
            ) : null}

            {isLoaded ? (
              <div className={styles.contentBodyContainer}>
                <ViewComponent
                  scrollerRef={scrollerRef}
                  items={items}
                  sortKey={sortKey}
                  sortDirection={sortDirection}
                  jumpToCharacter={jumpToCharacter}
                  isSelectMode={isSelectMode}
                  isSmallScreen={isSmallScreen}
                />

                <ChannelIndexFooter />
              </div>
            ) : null}

            {!error && isPopulated && !items.length ? (
              <NoChannel totalItems={totalItems} />
            ) : null}
          </PageContentBody>
          {isLoaded && !!jumpBarItems.order.length ? (
            <PageJumpBar
              items={jumpBarItems}
              onItemPress={onJumpBarItemPress}
            />
          ) : null}
        </div>

        {isSelectMode ? <ChannelIndexSelectFooter /> : null}

        {view === 'posters' ? (
          <ChannelIndexPosterOptionsModal
            isOpen={isOptionsModalOpen}
            onModalClose={onOptionsModalClose}
          />
        ) : null}
        {view === 'overview' ? (
          <ChannelIndexOverviewOptionsModal
            isOpen={isOptionsModalOpen}
            onModalClose={onOptionsModalClose}
          />
        ) : null}
      </PageContent>
    </SelectProvider>
  );
}, 'channelIndex');

export default ChannelIndex;
