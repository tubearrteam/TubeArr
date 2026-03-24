import _ from 'lodash';
import moment from 'moment';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import TextTruncate from 'react-text-truncate';
import Alert from 'Components/Alert';
import HeartRating from 'Components/HeartRating';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import IconButton from 'Components/Link/IconButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Measure from 'Components/Measure';
import MetadataAttribution from 'Components/MetadataAttribution';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import PageToolbarSeparator from 'Components/Page/Toolbar/PageToolbarSeparator';
import Popover from 'Components/Tooltip/Popover';
import Tooltip from 'Components/Tooltip/Tooltip';
import { align, icons, kinds, sizes, sortDirections, tooltipPositions } from 'Helpers/Props';
import OrganizePreviewModalConnector from 'Organize/OrganizePreviewModalConnector';
import DeleteChannelModal from 'Channel/Delete/DeleteChannelModal';
import EditChannelModal from 'Channel/Edit/EditChannelModal';
import MonitoringOptionsModal from 'Channel/MonitoringOptions/MonitoringOptionsModal';
import ChannelGenres from 'Channel/ChannelGenres';
import ChannelPoster from 'Channel/ChannelPoster';
import { getChannelStatusDetails } from 'Channel/ChannelStatus';
import QualityProfileNameConnector from 'Settings/Profiles/Quality/QualityProfileNameConnector';
import fonts from 'Styles/Variables/fonts';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import selectAll from 'Utilities/Table/selectAll';
import toggleSelected from 'Utilities/Table/toggleSelected';
import ChannelAlternateTitles from './ChannelAlternateTitles';
import ChannelDetailsLinks from './ChannelDetailsLinks';
import ChannelDetailsPlaylistConnector from './ChannelDetailsPlaylistConnector';
import ChannelTagsConnector from './ChannelTagsConnector';
import styles from './ChannelDetails.css';

const defaultFontSize = parseInt(fonts.defaultFontSize);
const lineHeight = parseFloat(fonts.lineHeight);

function getFanartUrl(images) {
  return _.find(images, { coverType: 'fanart' })?.url;
}

function getExpandedState(newState) {
  return {
    allExpanded: newState.allSelected,
    allCollapsed: newState.allUnselected,
    expandedState: newState.selectedState
  };
}

function getDateYear(date) {
  const dateDate = moment.utc(date);

  return dateDate.format('YYYY');
}

class ChannelDetails extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      isOrganizeModalOpen: false,
      isEditChannelModalOpen: false,
      isDeleteChannelModalOpen: false,
      isMonitorOptionsModalOpen: false,
      isVideoDetailsConfirmModalOpen: false,
      detailsView: 'videos',
      contentView: 'table',
      allExpanded: false,
      allCollapsed: false,
      expandedState: {},
      overviewHeight: 0
    };
  }

  //
  // Listeners

  onOrganizePress = () => {
    this.setState({ isOrganizeModalOpen: true });
  };

  onOrganizeModalClose = () => {
    this.setState({ isOrganizeModalOpen: false });
  };

  onEditChannelPress = () => {
    this.setState({ isEditChannelModalOpen: true });
  };

  onEditChannelModalClose = () => {
    this.setState({ isEditChannelModalOpen: false });
  };

  onDeleteChannelPress = () => {
    this.setState({
      isEditChannelModalOpen: false,
      isDeleteChannelModalOpen: true
    });
  };

  onDeleteChannelModalClose = () => {
    this.setState({ isDeleteChannelModalOpen: false });
  };

  onMonitorOptionsPress = () => {
    this.setState({ isMonitorOptionsModalOpen: true });
  };

  onMonitorOptionsClose = () => {
    this.setState({ isMonitorOptionsModalOpen: false });
  };

  onGetVideoDetailsPress = () => {
    this.setState({ isVideoDetailsConfirmModalOpen: true });
  };

  onGetVideoDetailsCancel = () => {
    this.setState({ isVideoDetailsConfirmModalOpen: false });
  };

  onGetVideoDetailsConfirm = () => {
    this.setState({ isVideoDetailsConfirmModalOpen: false });
    this.props.onGetVideoDetailsPress();
  };

  onExpandAllPress = () => {
    const {
      allExpanded,
      expandedState
    } = this.state;

    this.setState(getExpandedState(selectAll(expandedState, !allExpanded)));
  };

  onExpandPress = (playlistNumber, isExpanded) => {
    this.setState((state) => {
      const convertedState = {
        allSelected: state.allExpanded,
        allUnselected: state.allCollapsed,
        selectedState: state.expandedState
      };

      const newState = toggleSelected(convertedState, [], playlistNumber, isExpanded, false);

      return getExpandedState(newState);
    });
  };

  onMeasure = ({ height }) => {
    this.setState({ overviewHeight: height });
  };

  onVideosViewPress = () => {
    this.setState({ detailsView: 'videos' });
  };

  onPlaylistsViewPress = () => {
    this.setState({ detailsView: 'playlists' });
  };

  onTableViewPress = () => {
    this.setState({ contentView: 'table' });
  };

  onPosterViewPress = () => {
    this.setState({ contentView: 'posters' });
  };

  //
  // Render

  render() {
    const {
      id,
      youtubeChannelId,
      title,
      runtime,
      ratings,
      path,
      statistics = {},
      qualityProfileId,
      monitored,
      status,
      network,
      originalLanguage,
      overview,
      images = [],
      playlists = [],
      alternateTitles = [],
      genres = [],
      tags = [],
      year,
      lastAired,
      added,
      isSaving,
      isRefreshing,
      isSearching,
      isRssSyncExecuting,
      isGettingVideoDetails,
      isFetching,
      isPopulated,
      videosError,
      videoFilesError,
      hasVideos,
      hasMonitoredVideos,
      hasVideoFiles,
      previousChannel,
      nextChannel,
      onMonitorTogglePress,
      onRefreshPress,
      onSearchPress,
      onRssSyncPress
    } = this.props;

    const channelId = id;
    const playlistsSafe = Array.isArray(playlists) ? playlists : [];
    const alternateTitlesSafe = Array.isArray(alternateTitles) ? alternateTitles : [];
    const tagsSafe = Array.isArray(tags) ? tags : [];

    const {
      videoFileCount = 0,
      sizeOnDisk = 0
    } = statistics;

    const {
      isOrganizeModalOpen,
      isEditChannelModalOpen,
      isDeleteChannelModalOpen,
      isMonitorOptionsModalOpen,
      isVideoDetailsConfirmModalOpen,
      detailsView,
      contentView,
      allExpanded,
      allCollapsed,
      expandedState,
      overviewHeight
    } = this.state;

    const statusDetails = getChannelStatusDetails(status);
    const addedAtMs = added ? new Date(added).getTime() : NaN;
    const isRecentlyAdded = Number.isFinite(addedAtMs) && (Date.now() - addedAtMs) < (5 * 60 * 1000);
    const showLoadingVideosMessage = !hasVideos && !videosError && !videoFilesError && (isFetching || isRefreshing || isRecentlyAdded);
    const hasYear = Number.isInteger(year) && year > 0;
    const runningYears = hasYear
      ? (status === 'ended' && lastAired ? `${year}-${getDateYear(lastAired)}` : `${year}-`)
      : null;

    let videoFilesCountMessage = translate('ChannelDetailsNoVideoFiles');

    if (videoFileCount === 1) {
      videoFilesCountMessage = translate('ChannelDetailsOneVideoFile');
    } else if (videoFileCount > 1) {
      videoFilesCountMessage = translate('ChannelDetailsCountVideoFiles', { videoFileCount });
    }

    let expandIcon = icons.EXPAND_INDETERMINATE;

    if (allExpanded) {
      expandIcon = icons.COLLAPSE;
    } else if (allCollapsed) {
      expandIcon = icons.EXPAND;
    }

    const fanartUrl = getFanartUrl(images);
    const videosPlaylist = playlistsSafe.find((p) => p.playlistNumber === 1);
    const effectiveVideosPlaylist = videosPlaylist || {
      playlistNumber: 1,
      monitored,
      statistics: statistics || {}
    };
    const visiblePlaylists = detailsView === 'videos'
      ? [effectiveVideosPlaylist]
      : playlistsSafe.filter((p) => p.playlistNumber !== 1);
    const hasVisiblePlaylists = visiblePlaylists.length > 0;

    return (
      <PageContent title={title}>
        <PageToolbar>
          <PageToolbarSection>
            <PageToolbarButton
              label={translate('RefreshAndScan')}
              iconName={icons.REFRESH}
              spinningName={icons.REFRESH}
              title={translate('RefreshAndScanTooltip')}
              isSpinning={isRefreshing}
              onPress={onRefreshPress}
            />

            <PageToolbarButton
              label={translate('SearchMonitored')}
              iconName={icons.SEARCH}
              isDisabled={!monitored || !hasMonitoredVideos || !hasVideos}
              isSpinning={isSearching}
              title={hasMonitoredVideos ? undefined : translate('NoMonitoredVideos')}
              onPress={onSearchPress}
            />

            <PageToolbarButton
              label={translate('RssSync')}
              iconName={icons.RSS}
              isSpinning={isRssSyncExecuting}
              title={translate('RssSyncChannelTooltip')}
              onPress={onRssSyncPress}
            />

            <PageToolbarButton
              label={translate('GetVideoDetails')}
              iconName={icons.INFO}
              isDisabled={!hasVideos}
              isSpinning={isGettingVideoDetails}
              title={translate('GetVideoDetailsTooltip')}
              onPress={this.onGetVideoDetailsPress}
            />

            <PageToolbarSeparator />

            <PageToolbarButton
              label={translate('PreviewRename')}
              iconName={icons.ORGANIZE}
              isDisabled={!hasVideoFiles}
              onPress={this.onOrganizePress}
            />

            <PageToolbarSeparator />

            <PageToolbarButton
              label={translate('ChannelMonitoring')}
              iconName={icons.MONITORED}
              onPress={this.onMonitorOptionsPress}
            />

            <PageToolbarButton
              label={translate('Edit')}
              iconName={icons.EDIT}
              onPress={this.onEditChannelPress}
            />

            <PageToolbarButton
              label={translate('Delete')}
              iconName={icons.DELETE}
              onPress={this.onDeleteChannelPress}
            />

          </PageToolbarSection>

          <PageToolbarSection alignContent={align.RIGHT}>
            <PageToolbarButton
              label={translate('Videos')}
              iconName={icons.VIDEO_FILE}
              isDisabled={detailsView === 'videos'}
              onPress={this.onVideosViewPress}
            />

            <PageToolbarButton
              label={translate('Playlists')}
              iconName={icons.OVERVIEW}
              isDisabled={detailsView === 'playlists'}
              onPress={this.onPlaylistsViewPress}
            />

            <PageToolbarSeparator />

            <PageToolbarButton
              label={translate('Table')}
              iconName={icons.TABLE}
              isDisabled={contentView === 'table'}
              onPress={this.onTableViewPress}
            />

            <PageToolbarButton
              label={translate('Posters')}
              iconName={icons.POSTER}
              isDisabled={contentView === 'posters'}
              onPress={this.onPosterViewPress}
            />

            <PageToolbarSeparator />

            <PageToolbarButton
              label={allExpanded ? translate('CollapseAll') : translate('ExpandAll')}
              iconName={expandIcon}
              onPress={this.onExpandAllPress}
            />
          </PageToolbarSection>
        </PageToolbar>

        <PageContentBody innerClassName={styles.innerContentBody}>
          <div className={styles.header}>
            <div
              className={styles.backdrop}
              style={
                fanartUrl ?
                  { backgroundImage: `url(${fanartUrl})` } :
                  null
              }
            >
              <div className={styles.backdropOverlay} />
            </div>

            <div className={styles.headerContent}>
              <ChannelPoster
                className={styles.poster}
                images={images}
                size={500}
                lazy={false}
              />

              <div className={styles.info}>
                <div className={styles.titleRow}>
                  <div className={styles.titleContainer}>
                    <div className={styles.toggleMonitoredContainer}>
                      <MonitorToggleButton
                        className={styles.monitorToggleButton}
                        monitored={monitored}
                        isSaving={isSaving}
                        size={40}
                        onPress={onMonitorTogglePress}
                      />
                    </div>

                    <div className={styles.title}>
                      {title}
                    </div>

                    {
                      !!alternateTitlesSafe.length &&
                        <div className={styles.alternateTitlesIconContainer}>
                          <Popover
                            anchor={
                              <Icon
                                name={icons.ALTERNATE_TITLES}
                                size={20}
                              />
                            }
                            title={translate('AlternateTitles')}
                            body={<ChannelAlternateTitles alternateTitles={alternateTitlesSafe} />}
                            position={tooltipPositions.BOTTOM}
                          />
                        </div>
                    }
                  </div>

                <div className={styles.channelNavigationButtons}>
                    <IconButton
                      className={styles.channelNavigationButton}
                      name={icons.ARROW_LEFT}
                      size={30}
                      title={translate('ChannelDetailsGoTo', { title: previousChannel.title })}
                      to={`/channels/${previousChannel.titleSlug}`}
                    />

                    <IconButton
                      className={styles.channelNavigationButton}
                      name={icons.ARROW_RIGHT}
                      size={30}
                      title={translate('ChannelDetailsGoTo', { title: nextChannel.title })}
                      to={`/channels/${nextChannel.titleSlug}`}
                    />
                  </div>
                </div>

                <div className={styles.details}>
                  <div>
                    {
                      !!runtime &&
                        <span className={styles.runtime}>
                          {translate('ChannelDetailsRuntime', { runtime })}
                        </span>
                    }

                    {
                      ratings?.value ?
                        <HeartRating
                          rating={ratings.value}
                          votes={ratings.votes}
                          iconSize={20}
                        /> :
                        null
                    }

                    <ChannelGenres className={styles.genres} genres={genres} />

                    {
                      runningYears != null ?
                        <span>
                          {runningYears}
                        </span> :
                        null
                    }
                  </div>
                </div>

                <div className={styles.detailsLabels}>
                  <Label
                    className={styles.detailsLabel}
                    size={sizes.LARGE}
                  >
                    <div>
                      <Icon
                        name={icons.FOLDER}
                        size={17}
                      />
                      <span className={styles.path}>
                        {path}
                      </span>
                    </div>
                  </Label>

                  <Tooltip
                    anchor={
                      <Label
                        className={styles.detailsLabel}
                        size={sizes.LARGE}
                      >
                        <div>
                          <Icon
                            name={icons.DRIVE}
                            size={17}
                          />

                          <span className={styles.sizeOnDisk}>
                            {formatBytes(sizeOnDisk)}
                          </span>
                        </div>
                      </Label>
                    }
                    tooltip={
                      <span>
                        {videoFilesCountMessage}
                      </span>
                    }
                    kind={kinds.INVERSE}
                    position={tooltipPositions.BOTTOM}
                  />

                  <Label
                    className={styles.detailsLabel}
                    title={translate('QualityProfile')}
                    size={sizes.LARGE}
                  >
                    <div>
                      <Icon
                        name={icons.PROFILE}
                        size={17}
                      />
                      <span className={styles.qualityProfileName}>
                        {
                          qualityProfileId > 0 ?
                            <QualityProfileNameConnector
                              qualityProfileId={qualityProfileId}
                            /> :
                            translate('NoQualityProfile')
                        }
                      </span>
                    </div>
                  </Label>

                  <Label
                    className={styles.detailsLabel}
                    size={sizes.LARGE}
                  >
                    <div>
                      <Icon
                        name={monitored ? icons.MONITORED : icons.UNMONITORED}
                        size={17}
                      />
                      <span className={styles.qualityProfileName}>
                        {monitored ? translate('Monitored') : translate('Unmonitored')}
                      </span>
                    </div>
                  </Label>

                  <Label
                    className={styles.detailsLabel}
                    title={statusDetails.message}
                    size={sizes.LARGE}
                    kind={status === 'deleted' ? kinds.INVERSE : undefined}
                  >
                    <div>
                      <Icon
                        name={statusDetails.icon}
                        size={17}
                      />
                      <span className={styles.statusName}>
                        {statusDetails.title}
                      </span>
                    </div>
                  </Label>

                  {
                    originalLanguage?.name ?
                      <Label
                        className={styles.detailsLabel}
                        title={translate('OriginalLanguage')}
                        size={sizes.LARGE}
                      >
                        <div>
                          <Icon
                            name={icons.LANGUAGE}
                            size={17}
                          />
                          <span className={styles.originalLanguageName}>
                            {originalLanguage.name}
                          </span>
                        </div>
                      </Label> :
                      null
                  }

                  {
                    network ?
                      <Label
                        className={styles.detailsLabel}
                        title={translate('Network')}
                        size={sizes.LARGE}
                      >
                        <div>
                          <Icon
                            name={icons.NETWORK}
                            size={17}
                          />
                          <span className={styles.network}>
                            {network}
                          </span>
                        </div>
                      </Label> :
                      null
                  }

                  <Tooltip
                    anchor={
                      <Label
                        className={styles.detailsLabel}
                        size={sizes.LARGE}
                      >
                        <div>
                          <Icon
                            name={icons.EXTERNAL_LINK}
                            size={17}
                          />
                          <span className={styles.links}>
                            {translate('Links')}
                          </span>
                        </div>
                      </Label>
                    }
                    tooltip={
                      <ChannelDetailsLinks
                        youtubeChannelId={youtubeChannelId}
                      />
                    }
                    kind={kinds.INVERSE}
                    position={tooltipPositions.BOTTOM}
                  />

                  {
                    !!tagsSafe.length &&
                      <Tooltip
                        anchor={
                          <Label
                            className={styles.detailsLabel}
                            size={sizes.LARGE}
                          >
                            <Icon
                              name={icons.TAGS}
                              size={17}
                            />

                            <span className={styles.tags}>
                              {translate('Tags')}
                            </span>
                          </Label>
                        }
                        tooltip={<ChannelTagsConnector channelId={channelId} />}
                        kind={kinds.INVERSE}
                        position={tooltipPositions.BOTTOM}
                      />

                  }
                </div>

                <Measure onMeasure={this.onMeasure}>
                  <div className={styles.overview}>
                    <TextTruncate
                      line={Math.floor(overviewHeight / (defaultFontSize * lineHeight)) - 1}
                      text={overview}
                    />
                  </div>
                </Measure>

                <MetadataAttribution />
              </div>
            </div>
          </div>

          <div className={styles.contentContainer}>
            {
              !isPopulated && !videosError && !videoFilesError &&
                <LoadingIndicator />
            }

            {
              !isFetching && videosError ?
                <Alert kind={kinds.DANGER}>
                  {translate('VideosLoadError')}
                </Alert> :
                null
            }

            {
              !isFetching && videoFilesError ?
                <Alert kind={kinds.DANGER}>
                  {translate('VideoFilesLoadError')}
                </Alert> :
                null
            }

            {
              isPopulated && hasVisiblePlaylists &&
                <div>
                  {
                    visiblePlaylists.slice(0).reverse().map((playlist) => {
                      return (
                        <ChannelDetailsPlaylistConnector
                          key={playlist.playlistNumber}
                          channelId={channelId}
                          {...playlist}
                          contentView={contentView}
                          isExpanded={expandedState[playlist.playlistNumber]}
                          onExpandPress={this.onExpandPress}
                        />
                      );
                    })
                  }
                </div>
            }

            {
              isPopulated && !hasVisiblePlaylists ?
                <Alert kind={kinds.WARNING}>
                  {detailsView === 'videos'
                    ? (showLoadingVideosMessage ? translate('LoadingChannelVideoDetails') : translate('NoVideoInformation'))
                    : translate('NoPlaylists')}
                </Alert> :
                null
            }

          </div>

          <OrganizePreviewModalConnector
            isOpen={isOrganizeModalOpen}
            channelId={channelId}
            onModalClose={this.onOrganizeModalClose}
          />

          <EditChannelModal
            isOpen={isEditChannelModalOpen}
            channelId={channelId}
            onModalClose={this.onEditChannelModalClose}
            onDeleteChannelPress={this.onDeleteChannelPress}
          />

          <DeleteChannelModal
            isOpen={isDeleteChannelModalOpen}
            channelId={channelId}
            onModalClose={this.onDeleteChannelModalClose}
          />

          <MonitoringOptionsModal
            isOpen={isMonitorOptionsModalOpen}
            channelId={channelId}
            onModalClose={this.onMonitorOptionsClose}
          />

          <ConfirmModal
            isOpen={isVideoDetailsConfirmModalOpen}
            title={translate('GetVideoDetailsConfirmTitle')}
            message={translate('GetVideoDetailsConfirmMessage')}
            confirmLabel={translate('Continue')}
            cancelLabel={translate('Cancel')}
            isSpinning={isGettingVideoDetails}
            onConfirm={this.onGetVideoDetailsConfirm}
            onCancel={this.onGetVideoDetailsCancel}
          />
        </PageContentBody>
      </PageContent>
    );
  }
}

ChannelDetails.propTypes = {
  id: PropTypes.number.isRequired,
  youtubeChannelId: PropTypes.string,
  title: PropTypes.string.isRequired,
  runtime: PropTypes.number.isRequired,
  ratings: PropTypes.object.isRequired,
  path: PropTypes.string.isRequired,
  statistics: PropTypes.object.isRequired,
  qualityProfileId: PropTypes.number.isRequired,
  monitored: PropTypes.bool.isRequired,
  monitor: PropTypes.string,
  status: PropTypes.string.isRequired,
  network: PropTypes.string,
  originalLanguage: PropTypes.object,
  overview: PropTypes.string.isRequired,
  images: PropTypes.arrayOf(PropTypes.object).isRequired,
  playlists: PropTypes.arrayOf(PropTypes.object).isRequired,
  alternateTitles: PropTypes.arrayOf(PropTypes.object).isRequired,
  genres: PropTypes.arrayOf(PropTypes.string).isRequired,
  tags: PropTypes.arrayOf(PropTypes.number).isRequired,
  year: PropTypes.number,
  lastAired: PropTypes.string,
  added: PropTypes.string,
  previousAiring: PropTypes.string,
  isSaving: PropTypes.bool.isRequired,
  saveError: PropTypes.object,
  isRefreshing: PropTypes.bool.isRequired,
  isSearching: PropTypes.bool.isRequired,
  isRssSyncExecuting: PropTypes.bool.isRequired,
  isGettingVideoDetails: PropTypes.bool.isRequired,
  isFetching: PropTypes.bool.isRequired,
  isPopulated: PropTypes.bool.isRequired,
  videosError: PropTypes.object,
  videoFilesError: PropTypes.object,
  hasVideos: PropTypes.bool.isRequired,
  hasMonitoredVideos: PropTypes.bool.isRequired,
  hasVideoFiles: PropTypes.bool.isRequired,
  previousChannel: PropTypes.object.isRequired,
  nextChannel: PropTypes.object.isRequired,
  onMonitorTogglePress: PropTypes.func.isRequired,
  onRefreshPress: PropTypes.func.isRequired,
  onSearchPress: PropTypes.func.isRequired,
  onRssSyncPress: PropTypes.func.isRequired,
  onGetVideoDetailsPress: PropTypes.func.isRequired
};

ChannelDetails.defaultProps = {
  statistics: {},
  tags: [],
  isSaving: false
};

export default ChannelDetails;
