import classNames from 'classnames';
import React, { useCallback, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { useSelect } from 'App/SelectContext';
import { REFRESH_CHANNEL, DOWNLOAD_MONITORED } from 'Commands/commandNames';
import CheckInput from 'Components/Form/CheckInput';
import HeartRating from 'Components/HeartRating';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import RelativeDateCell from 'Components/Table/Cells/RelativeDateCell';
import VirtualTableRowCell from 'Components/Table/Cells/VirtualTableRowCell';
import VirtualTableSelectCell from 'Components/Table/Cells/VirtualTableSelectCell';
import Column from 'Components/Table/Column';
import TagListConnector from 'Components/TagListConnector';
import { icons } from 'Helpers/Props';
import DeleteChannelModal from 'Channel/Delete/DeleteChannelModal';
import EditChannelModal from 'Channel/Edit/EditChannelModal';
import createChannelIndexItemSelector from 'Channel/Index/createChannelIndexItemSelector';
import { Statistics } from 'Channel/Channel';
import ChannelBanner from 'Channel/ChannelBanner';
import ChannelTitleLink from 'Channel/ChannelTitleLink';
import { executeCommand } from 'Store/Actions/commandActions';
import { SelectStateInputProps } from 'typings/props';
import formatBytes from 'Utilities/Number/formatBytes';
import titleCase from 'Utilities/String/titleCase';
import translate from 'Utilities/String/translate';
import ChannelIndexProgressBar from '../ProgressBar/ChannelIndexProgressBar';
import hasGrowableColumns from './hasGrowableColumns';
import PlaylistsCell from './PlaylistsCell';
import selectTableOptions from './selectTableOptions';
import ChannelStatusCell from './ChannelStatusCell';
import styles from './ChannelIndexRow.css';

interface ChannelIndexRowProps {
  channelId: number;
  sortKey: string;
  columns: Column[];
  isSelectMode: boolean;
}

function ChannelIndexRow(props: ChannelIndexRowProps) {
  const { channelId: channelId, columns, isSelectMode } = props;

  const {
    channel,
    qualityProfile,
    latestPlaylist,
    isRefreshingChannel,
    isSearchingChannel,
  } = useSelector(createChannelIndexItemSelector(channelId));

  const { showBanners, showSearchAction } = useSelector(selectTableOptions);

  const {
    title,
    monitored,
    monitorNewItems,
    status,
    path,
    titleSlug,
    nextAiring,
    previousAiring,
    added,
    statistics = {} as Statistics,
    playlistFolder,
    images,
    channelType: channelType,
    network,
    originalLanguage,
    certification,
    year,
    genres = [],
    ratings,
    playlists = [],
    tags = [],
    isSaving = false,
    hasShortsTab: hasShortsTabHeuristic,
  } = channel;

  const {
    playlistCount = 0,
    videoCount = 0,
    videoFileCount = 0,
    totalVideoCount = 0,
    sizeOnDisk = 0,
    releaseGroups = [],
  } = statistics;

  const dispatch = useDispatch();
  const [hasBannerError, setHasBannerError] = useState(false);
  const [isEditChannelModalOpen, setIsEditChannelModalOpen] = useState(false);
  const [isDeleteChannelModalOpen, setIsDeleteChannelModalOpen] = useState(false);
  const [selectState, selectDispatch] = useSelect();

  const onRefreshPress = useCallback(() => {
    dispatch(
      executeCommand({
        name: REFRESH_CHANNEL,
        channelId: channelId,
      })
    );
  }, [channelId, dispatch]);

  const onSearchPress = useCallback(() => {
    dispatch(
      executeCommand({
        name: DOWNLOAD_MONITORED,
        channelId: channelId,
      })
    );
  }, [channelId, dispatch]);

  const onBannerLoadError = useCallback(() => {
    setHasBannerError(true);
  }, [setHasBannerError]);

  const onBannerLoad = useCallback(() => {
    setHasBannerError(false);
  }, [setHasBannerError]);

  const onEditChannelPress = useCallback(() => {
    setIsEditChannelModalOpen(true);
  }, [setIsEditChannelModalOpen]);

  const onEditChannelModalClose = useCallback(() => {
    setIsEditChannelModalOpen(false);
  }, [setIsEditChannelModalOpen]);

  const onDeleteChannelPress = useCallback(() => {
    setIsEditChannelModalOpen(false);
    setIsDeleteChannelModalOpen(true);
  }, [setIsDeleteChannelModalOpen]);

  const onDeleteChannelModalClose = useCallback(() => {
    setIsDeleteChannelModalOpen(false);
  }, [setIsDeleteChannelModalOpen]);

  const checkInputCallback = useCallback(() => {
    // Mock handler to satisfy `onChange` being required for `CheckInput`.
  }, []);

  const onSelectedChange = useCallback(
    ({ id, value, shiftKey }: SelectStateInputProps) => {
      selectDispatch({
        type: 'toggleSelected',
        id,
        isSelected: value,
        shiftKey,
      });
    },
    [selectDispatch]
  );

  return (
    <>
      {isSelectMode ? (
        <VirtualTableSelectCell
          id={channelId}
          isSelected={selectState.selectedState[channelId]}
          isDisabled={false}
          onSelectedChange={onSelectedChange}
        />
      ) : null}

      {columns.map((column) => {
        const { name, isVisible } = column;

        if (!isVisible) {
          return null;
        }

        if (name === 'status') {
          return (
            <ChannelStatusCell
              key={name}
              className={styles[name]}
              channelId={channelId}
              monitored={monitored}
              status={status}
              isSelectMode={isSelectMode}
              isSaving={isSaving}
              component={VirtualTableRowCell}
            />
          );
        }

        if (name === 'sortTitle') {
          return (
            <VirtualTableRowCell
              key={name}
              className={classNames(
                styles[name],
                showBanners && styles.banner,
                showBanners && !hasGrowableColumns(columns) && styles.bannerGrow
              )}
            >
              {showBanners ? (
                <Link className={styles.link} to={`/channels/${titleSlug}`}>
                  <ChannelBanner
                    className={styles.bannerImage}
                    images={images}
                    lazy={false}
                    overflow={true}
                    onError={onBannerLoadError}
                    onLoad={onBannerLoad}
                  />

                  {hasBannerError && (
                    <div className={styles.overlayTitle}>{title}</div>
                  )}
                </Link>
              ) : (
                <ChannelTitleLink titleSlug={titleSlug} title={title} />
              )}
            </VirtualTableRowCell>
          );
        }

        if (name === 'channelType') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              {titleCase(channelType)}
            </VirtualTableRowCell>
          );
        }

        if (name === 'network') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              {network}
            </VirtualTableRowCell>
          );
        }

        if (name === 'originalLanguage') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              {originalLanguage.name}
            </VirtualTableRowCell>
          );
        }

        if (name === 'qualityProfileId') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              {qualityProfile?.name ?? ''}
            </VirtualTableRowCell>
          );
        }

        if (name === 'nextAiring') {
          return (
            // eslint-disable-next-line @typescript-eslint/ban-ts-comment
            // @ts-ignore ts(2739)
            <RelativeDateCell
              key={name}
              className={styles[name]}
              date={nextAiring}
              component={VirtualTableRowCell}
            />
          );
        }

        if (name === 'previousAiring') {
          return (
            // eslint-disable-next-line @typescript-eslint/ban-ts-comment
            // @ts-ignore ts(2739)
            <RelativeDateCell
              key={name}
              className={styles[name]}
              date={previousAiring}
              component={VirtualTableRowCell}
            />
          );
        }

        if (name === 'added') {
          return (
            // eslint-disable-next-line @typescript-eslint/ban-ts-comment
            // @ts-ignore ts(2739)
            <RelativeDateCell
              key={name}
              className={styles[name]}
              date={added}
              component={VirtualTableRowCell}
            />
          );
        }

        if (name === 'playlistCount') {
          return (
            <PlaylistsCell
              key={name}
              className={styles[name]}
              channelId={channelId}
              playlistCount={playlistCount}
              playlists={playlists}
              isSelectMode={isSelectMode}
            />
          );
        }

        if (name === 'playlistFolder') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              <CheckInput
                name="playlistFolder"
                value={playlistFolder}
                isDisabled={true}
                onChange={checkInputCallback}
              />
            </VirtualTableRowCell>
          );
        }

        if (name === 'videoProgress') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              <ChannelIndexProgressBar
                channelId={channelId}
                monitored={monitored}
                status={status}
                videoCount={videoCount}
                videoFileCount={videoFileCount}
                totalVideoCount={totalVideoCount}
                width={125}
                detailedProgressBar={true}
                isStandalone={true}
              />
            </VirtualTableRowCell>
          );
        }

        if (name === 'latestPlaylist') {
          if (!latestPlaylist) {
            return <VirtualTableRowCell key={name} className={styles[name]} />;
          }

          const playlistStatistics = latestPlaylist.statistics || {};

          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              <ChannelIndexProgressBar
                channelId={channelId}
                playlistNumber={latestPlaylist.playlistNumber}
                monitored={monitored}
                status={status}
                videoCount={playlistStatistics.videoCount}
                videoFileCount={playlistStatistics.videoFileCount}
                totalVideoCount={playlistStatistics.totalVideoCount}
                width={125}
                detailedProgressBar={true}
                isStandalone={true}
              />
            </VirtualTableRowCell>
          );
        }

        if (name === 'videoCount') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              {totalVideoCount}
            </VirtualTableRowCell>
          );
        }

        if (name === 'year') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              {year}
            </VirtualTableRowCell>
          );
        }

        if (name === 'path') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              {path}
            </VirtualTableRowCell>
          );
        }

        if (name === 'sizeOnDisk') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              {formatBytes(sizeOnDisk)}
            </VirtualTableRowCell>
          );
        }

        if (name === 'genres') {
          const joinedGenres = genres.join(', ');

          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              <span title={joinedGenres}>{joinedGenres}</span>
            </VirtualTableRowCell>
          );
        }

        if (name === 'ratings') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              <HeartRating rating={ratings.value} votes={ratings.votes} />
            </VirtualTableRowCell>
          );
        }

        if (name === 'certification') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              {certification}
            </VirtualTableRowCell>
          );
        }

        if (name === 'releaseGroups') {
          const joinedReleaseGroups = releaseGroups.join(', ');
          const truncatedReleaseGroups =
            releaseGroups.length > 3
              ? `${releaseGroups.slice(0, 3).join(', ')}...`
              : joinedReleaseGroups;

          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              <span title={joinedReleaseGroups}>{truncatedReleaseGroups}</span>
            </VirtualTableRowCell>
          );
        }

        if (name === 'tags') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              <TagListConnector tags={tags} />
            </VirtualTableRowCell>
          );
        }

        if (name === 'monitorNewItems') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              {monitorNewItems === 'all'
                ? translate('PlaylistsMonitoredAll')
                : translate('PlaylistsMonitoredNone')}
            </VirtualTableRowCell>
          );
        }

        if (name === 'hasShortsTab') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              {hasShortsTabHeuristic === true
                ? translate('Yes')
                : translate('Unknown')}
            </VirtualTableRowCell>
          );
        }

        if (name === 'actions') {
          return (
            <VirtualTableRowCell key={name} className={styles[name]}>
              <SpinnerIconButton
                name={icons.REFRESH}
                title={translate('RefreshChannel')}
                isSpinning={isRefreshingChannel}
                onPress={onRefreshPress}
              />

              {showSearchAction ? (
                <SpinnerIconButton
                  name={icons.SEARCH}
                  title={translate('SearchForMonitoredVideos')}
                  isSpinning={isSearchingChannel}
                  onPress={onSearchPress}
                />
              ) : null}

              <IconButton
                name={icons.EDIT}
                title={translate('EditChannel')}
                onPress={onEditChannelPress}
              />
            </VirtualTableRowCell>
          );
        }

        return null;
      })}

      <EditChannelModal
        isOpen={isEditChannelModalOpen}
        channelId={channelId}
        onModalClose={onEditChannelModalClose}
        onDeleteChannelPress={onDeleteChannelPress}
      />

      <DeleteChannelModal
        isOpen={isDeleteChannelModalOpen}
        channelId={channelId}
        onModalClose={onDeleteChannelModalClose}
      />
    </>
  );
}

export default ChannelIndexRow;
