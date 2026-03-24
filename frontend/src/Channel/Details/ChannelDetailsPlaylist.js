import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Icon from 'Components/Icon';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import SelectInput from 'Components/Form/SelectInput';
import Menu from 'Components/Menu/Menu';
import MenuButton from 'Components/Menu/MenuButton';
import MenuContent from 'Components/Menu/MenuContent';
import MenuItem from 'Components/Menu/MenuItem';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import SpinnerIcon from 'Components/SpinnerIcon';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import TablePager from 'Components/Table/TablePager';
import Popover from 'Components/Tooltip/Popover';
import { align, icons, sortDirections, tooltipPositions } from 'Helpers/Props';
import OrganizePreviewModalConnector from 'Organize/OrganizePreviewModalConnector';
import isAfter from 'Utilities/Date/isAfter';
import isBefore from 'Utilities/Date/isBefore';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import getToggledRange from 'Utilities/Table/getToggledRange';
import VideoRowConnector from './VideoRowConnector';
import ChannelDetailsVideoGallery from './ChannelDetailsVideoGallery';
import PlaylistInfo from './PlaylistInfo';
import PlaylistProgressLabel from './PlaylistProgressLabel';
import styles from './ChannelDetailsPlaylist.css';

const PAGED_VIDEO_THRESHOLD = 20;
const DEFAULT_PAGE_SIZE = 50;
const PAGE_SIZE_OPTIONS = [20, 50, 100, 200, 500, 1000].map((size) => ({
  key: `${size}`,
  value: size
}));

function getDateUtc(video) {
  return video.uploadDateUtc ?? video.airDateUtc;
}

function getPlaylistStatistics(videos) {
  let videoCount = 0;
  let videoFileCount = 0;
  let totalVideoCount = 0;
  let monitoredVideoCount = 0;
  let hasMonitoredVideos = false;
  const sizeOnDisk = 0;

  videos.forEach((video) => {
    const dateUtc = getDateUtc(video);
    if (video.videoFileId || (video.monitored && isBefore(dateUtc))) {
      videoCount++;
    }

    if (video.videoFileId) {
      videoFileCount++;
    }

    if (video.monitored) {
      monitoredVideoCount++;
      hasMonitoredVideos = true;
    }

    totalVideoCount++;
  });

  return {
    videoCount,
    videoFileCount,
    totalVideoCount,
    monitoredVideoCount,
    hasMonitoredVideos,
    sizeOnDisk
  };
}

class ChannelDetailsPlaylist extends Component {

  //
  // Lifecycle
  //

  constructor(props, context) {
    super(props, context);

    this.state = {
      isOrganizeModalOpen: false,
      lastToggledVideo: null,
      page: 1,
      pageSize: DEFAULT_PAGE_SIZE
    };
  }

  componentDidMount() {
    this._expandByDefault();
  }

  componentDidUpdate(prevProps) {
    const {
      channelId: channelId,
      items
    } = this.props;

    if (prevProps.channelId !== channelId) {
      this._expandByDefault();
      this.setState({ page: 1, lastToggledVideo: null });
      return;
    }

    const totalPages = this.getTotalPages(items);
    if (this.state.page > totalPages) {
      this.setState({ page: totalPages });
      return;
    }

    if (
      getPlaylistStatistics(prevProps.items).videoFileCount > 0 &&
      getPlaylistStatistics(items).videoFileCount === 0
    ) {
      this.setState({
        isOrganizeModalOpen: false
      });
    }
  }

  //
  // Control
  //

  _expandByDefault() {
    const {
      playlistNumber,
      onExpandPress,
      items
    } = this.props;

    const expand = _.some(items, (item) => {
      const dateUtc = getDateUtc(item);
      return isAfter(dateUtc) || isAfter(dateUtc, { days: -30 });
    }) || items.every((item) => !getDateUtc(item));

    onExpandPress(playlistNumber, expand && playlistNumber > 0);
  }

  getTotalPages(items = this.props.items, pageSize = this.state.pageSize) {
    return Math.max(Math.ceil(items.length / pageSize), 1);
  }

  getVisibleItems() {
    const { items } = this.props;
    const { pageSize } = this.state;

    if (items.length <= PAGED_VIDEO_THRESHOLD) {
      return items;
    }

    const totalPages = this.getTotalPages(items, pageSize);
    const page = Math.min(this.state.page, totalPages);

    const start = (page - 1) * pageSize;
    return items.slice(start, start + pageSize);
  }

  //
  // Listeners
  //

  onOrganizePress = () => {
    this.setState({ isOrganizeModalOpen: true });
  };

  onOrganizeModalClose = () => {
    this.setState({ isOrganizeModalOpen: false });
  };

  onExpandPress = () => {
    const {
      playlistNumber,
      isExpanded
    } = this.props;

    this.props.onExpandPress(playlistNumber, !isExpanded);
  };

  onMonitorVideoPress = (videoId, monitored, { shiftKey }) => {
    const lastToggled = this.state.lastToggledVideo;
    const videoIds = [videoId];
    const visibleItems = this.getVisibleItems();

    if (shiftKey && lastToggled) {
      const { lower, upper } = getToggledRange(visibleItems, videoId, lastToggled);

      for (let i = lower; i < upper; i++) {
        videoIds.push(visibleItems[i].id);
      }
    }

    this.setState({ lastToggledVideo: videoId });

    this.props.onMonitorVideoPress(_.uniq(videoIds), monitored);
  };

  onFirstPagePress = () => {
    this.setState({ page: 1, lastToggledVideo: null });
  };

  onPreviousPagePress = () => {
    this.setState((state) => ({
      page: Math.max(state.page - 1, 1),
      lastToggledVideo: null
    }));
  };

  onNextPagePress = () => {
    this.setState((state, props) => ({
      page: Math.min(state.page + 1, this.getTotalPages(props.items)),
      lastToggledVideo: null
    }));
  };

  onLastPagePress = () => {
    this.setState({
      page: this.getTotalPages(),
      lastToggledVideo: null
    });
  };

  onPageSelect = (page) => {
    this.setState({
      page,
      lastToggledVideo: null
    });
  };

  onPageSizeChange = ({ value }) => {
    this.setState({
      page: 1,
      pageSize: parseInt(value, 10),
      lastToggledVideo: null
    });
  };

  //
  // Render
  //

  render() {
    const {
      channelId: channelId,
      path,
      monitored,
      playlistNumber,
      items,
      columns,
      sortKey,
      sortDirection,
      statistics,
      isSaving,
      isExpanded,
      isSearching,
      isPopulatingVideos,
      channelMonitored,
      channelType,
      contentView,
      isSmallScreen,
      onSortPress,
      onTableOptionChange,
      onMonitorPlaylistPress,
      onInvertMonitoredPress,
      onSearchPress,
      onDownloadVideoPress,
      allVideosMonitored
    } = this.props;

    const {
      sizeOnDisk = 0
    } = statistics;

    const {
      videoCount,
      videoFileCount,
      totalVideoCount,
      monitoredVideoCount,
      hasMonitoredVideos
    } = getPlaylistStatistics(items);

    const {
      isOrganizeModalOpen,
      page,
      pageSize
    } = this.state;
    const totalPages = this.getTotalPages(items);
    const currentPage = Math.min(page, totalPages);
    const visibleItems = this.getVisibleItems();
    const shouldPaginateVideos = items.length > PAGED_VIDEO_THRESHOLD;

    const title = (this.props.title != null && this.props.title !== '')
      ? this.props.title
      : (playlistNumber === 0 ? translate('Specials') : translate('PlaylistNumberToken', { playlistNumber }));
    const canSearch = hasMonitoredVideos && channelMonitored;
    const playlistSearchLabel = playlistNumber > 1
      ? translate('SearchForMonitoredVideosPlaylist')
      : translate('SearchForMonitoredVideos');
    const channelSearchLabel = translate('SearchForMonitoredVideos');

    return (
      <div
        className={styles.playlist}
      >
        <div className={styles.header}>
          <div className={styles.left}>
            <MonitorToggleButton
              monitored={allVideosMonitored}
              isDisabled={!channelMonitored || !items.length}
              isSaving={isSaving}
              size={24}
              onPress={onInvertMonitoredPress}
              title={allVideosMonitored ? translate('MonitorNoVideos') : translate('MonitorAllVideos')}
            />

            <span className={styles.playlistNumber}>
              {title}
            </span>

            <Popover
              className={styles.videoCountTooltip}
              canFlip={true}
              anchor={
                <PlaylistProgressLabel
                  channelId={channelId}
                  playlistNumber={playlistNumber}
                  monitored={monitored}
                  isPopulatingVideos={isPopulatingVideos}
                  videoCount={videoCount}
                  videoFileCount={videoFileCount}
                />
              }
              title={translate('PlaylistInformation')}
              body={
                <div>
                  <PlaylistInfo
                    totalVideoCount={totalVideoCount}
                    monitoredVideoCount={monitoredVideoCount}
                    videoFileCount={videoFileCount}
                    sizeOnDisk={sizeOnDisk}
                  />
                </div>
              }
              position={tooltipPositions.BOTTOM}
            />

            {
              sizeOnDisk ?
                <div className={styles.sizeOnDisk}>
                  {formatBytes(sizeOnDisk)}
                </div> :
                null
            }
          </div>

          <Link
            className={styles.expandButton}
            onPress={this.onExpandPress}
          >
            <Icon
              className={styles.expandButtonIcon}
              name={isExpanded ? icons.COLLAPSE : icons.EXPAND}
              title={isExpanded ? translate('HideVideos') : translate('ShowVideos')}
              size={24}
            />
            {
              !isSmallScreen &&
                <span>&nbsp;</span>
            }
          </Link>

          {
            isSmallScreen ?
              <Menu
                className={styles.actionsMenu}
                alignMenu={align.RIGHT}
                enforceMaxHeight={false}
              >
                <MenuButton>
                  <Icon
                    name={icons.ACTIONS}
                    size={22}
                  />
                </MenuButton>

                <MenuContent className={styles.actionsMenuContent}>
                  <MenuItem
                    isDisabled={isSearching || !canSearch}
                    onPress={() => onSearchPress('playlist')}
                  >
                    <SpinnerIcon
                      className={styles.actionMenuIcon}
                      name={icons.DOWNLOADING}
                      isSpinning={isSearching}
                    />

                    {playlistSearchLabel}
                  </MenuItem>

                  <MenuItem
                    isDisabled={isSearching || !canSearch}
                    onPress={() => onSearchPress('channel')}
                  >
                    <Icon
                      className={styles.actionMenuIcon}
                      name={icons.SEARCH}
                    />

                    {channelSearchLabel}
                  </MenuItem>

                  <MenuItem
                    onPress={this.onOrganizePress}
                    isDisabled={!videoFileCount}
                  >
                    <Icon
                      className={styles.actionMenuIcon}
                      name={icons.ORGANIZE}
                    />

                    {translate('PreviewRename')}
                  </MenuItem>

                  {null}
                </MenuContent>
              </Menu> :

              <div className={styles.actions}>
                <SpinnerIconButton
                  className={styles.actionButton}
                  name={icons.DOWNLOADING}
                  title={canSearch ? playlistSearchLabel : translate('NoMonitoredVideosPlaylist')}
                  size={24}
                  isSpinning={isSearching}
                  isDisabled={isSearching || !canSearch}
                  onPress={() => onSearchPress('playlist')}
                />

                <Menu
                  className={styles.actionsMenu}
                  alignMenu={align.RIGHT}
                  enforceMaxHeight={false}
                >
                  <MenuButton>
                    <Icon
                      name={icons.ACTIONS}
                      size={22}
                    />
                  </MenuButton>

                  <MenuContent className={styles.actionsMenuContent}>
                    <MenuItem
                      isDisabled={isSearching || !canSearch}
                      onPress={() => onSearchPress('playlist')}
                    >
                      <Icon
                        className={styles.actionMenuIcon}
                        name={icons.DOWNLOADING}
                      />

                      {playlistSearchLabel}
                    </MenuItem>

                    <MenuItem
                      isDisabled={isSearching || !canSearch}
                      onPress={() => onSearchPress('channel')}
                    >
                      <Icon
                        className={styles.actionMenuIcon}
                        name={icons.SEARCH}
                      />

                      {channelSearchLabel}
                    </MenuItem>
                  </MenuContent>
                </Menu>

                <IconButton
                  className={styles.actionButton}
                  name={icons.ORGANIZE}
                  title={translate('PreviewRenamePlaylist')}
                  size={24}
                  isDisabled={!videoFileCount}
                  onPress={this.onOrganizePress}
                />

                {null}
              </div>
          }

        </div>

        <div>
          {
            isExpanded &&
              <div className={styles.videos}>
                {
                  items.length ?
                    <div>
                      {
                        shouldPaginateVideos &&
                          <div className={styles.topPaginationControls}>
                            <div className={styles.pageSizeSelector}>
                              <span className={styles.pageSizeLabel}>
                                {translate('TablePageSize')}
                              </span>

                              <SelectInput
                                className={styles.pageSizeInput}
                                name="channelDetailsPageSize"
                                value={pageSize}
                                values={PAGE_SIZE_OPTIONS}
                                onChange={this.onPageSizeChange}
                              />
                            </div>

                            <div className={styles.topPager}>
                              <TablePager
                                page={currentPage}
                                totalPages={totalPages}
                                totalRecords={items.length}
                                onFirstPagePress={this.onFirstPagePress}
                                onPreviousPagePress={this.onPreviousPagePress}
                                onNextPagePress={this.onNextPagePress}
                                onLastPagePress={this.onLastPagePress}
                                onPageSelect={this.onPageSelect}
                              />
                            </div>
                          </div>
                      }

                      {
                        contentView === 'posters' ?
                          <ChannelDetailsVideoGallery
                            items={visibleItems}
                            channelId={channelId}
                            channelMonitored={channelMonitored}
                            channelType={channelType}
                            onMonitorVideoPress={this.onMonitorVideoPress}
                            onDownloadVideoPress={onDownloadVideoPress}
                          /> :
                          <Table
                            columns={columns}
                            sortKey={sortKey}
                            sortDirection={sortDirection}
                            onSortPress={onSortPress}
                            onTableOptionChange={onTableOptionChange}
                          >
                            <TableBody>
                              {
                                visibleItems.map((item) => {
                                  return (
                                    <VideoRowConnector
                                      key={item.id}
                                      columns={columns}
                                      {...item}
                                      onMonitorVideoPress={this.onMonitorVideoPress}
                                      onDownloadVideoPress={onDownloadVideoPress}
                                    />
                                  );
                                })
                              }
                            </TableBody>
                          </Table>
                      }

                      {
                        shouldPaginateVideos &&
                          <div className={styles.paginationControls}>
                            <TablePager
                              page={currentPage}
                              totalPages={totalPages}
                              totalRecords={items.length}
                              onFirstPagePress={this.onFirstPagePress}
                              onPreviousPagePress={this.onPreviousPagePress}
                              onNextPagePress={this.onNextPagePress}
                              onLastPagePress={this.onLastPagePress}
                              onPageSelect={this.onPageSelect}
                            />
                          </div>
                      }
                    </div> :

                    <div className={styles.noVideos}>
                      {isPopulatingVideos ? translate('PopulatingVideos') : translate('NoVideosInThisPlaylist')}
                    </div>
                }
                <div className={styles.collapseButtonContainer}>
                  <IconButton
                    iconClassName={styles.collapseButtonIcon}
                    name={icons.COLLAPSE}
                    size={20}
                    title={translate('HideVideos')}
                    onPress={this.onExpandPress}
                  />
                </div>
              </div>
          }
        </div>

        <OrganizePreviewModalConnector
          isOpen={isOrganizeModalOpen}
          channelId={channelId}
          playlistNumber={playlistNumber}
          onModalClose={this.onOrganizeModalClose}
        />

      </div>
    );
  }
}

ChannelDetailsPlaylist.propTypes = {
  channelId: PropTypes.number.isRequired,
  path: PropTypes.string.isRequired,
  monitored: PropTypes.bool.isRequired,
  playlistNumber: PropTypes.number.isRequired,
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  columns: PropTypes.arrayOf(PropTypes.object).isRequired,
  sortKey: PropTypes.string.isRequired,
  sortDirection: PropTypes.oneOf(sortDirections.all),
  statistics: PropTypes.object.isRequired,
  isSaving: PropTypes.bool,
  isExpanded: PropTypes.bool,
  isSearching: PropTypes.bool.isRequired,
  isPopulatingVideos: PropTypes.bool.isRequired,
  channelMonitored: PropTypes.bool.isRequired,
  channelType: PropTypes.string,
  contentView: PropTypes.oneOf(['table', 'posters']),
  isSmallScreen: PropTypes.bool.isRequired,
  onTableOptionChange: PropTypes.func.isRequired,
  onSortPress: PropTypes.func.isRequired,
  onMonitorPlaylistPress: PropTypes.func.isRequired,
  onExpandPress: PropTypes.func.isRequired,
  onMonitorVideoPress: PropTypes.func.isRequired,
  onSearchPress: PropTypes.func.isRequired,
  onDownloadVideoPress: PropTypes.func.isRequired
};

ChannelDetailsPlaylist.defaultProps = {
  statistics: {},
  channelType: 'standard',
  contentView: 'table'
};

export default ChannelDetailsPlaylist;

