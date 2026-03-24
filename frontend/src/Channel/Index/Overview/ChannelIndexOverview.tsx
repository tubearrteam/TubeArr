import classNames from 'classnames';
import React, { useCallback, useMemo, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import TextTruncate from 'react-text-truncate';
import { REFRESH_CHANNEL, DOWNLOAD_MONITORED } from 'Commands/commandNames';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import TagListConnector from 'Components/TagListConnector';
import { icons } from 'Helpers/Props';
import DeleteChannelModal from 'Channel/Delete/DeleteChannelModal';
import EditChannelModal from 'Channel/Edit/EditChannelModal';
import ChannelIndexProgressBar from 'Channel/Index/ProgressBar/ChannelIndexProgressBar';
import ChannelIndexPosterSelect from 'Channel/Index/Select/ChannelIndexPosterSelect';
import { Image, Statistics } from 'Channel/Channel';
import ChannelPoster from 'Channel/ChannelPoster';
import { executeCommand } from 'Store/Actions/commandActions';
import dimensions from 'Styles/Variables/dimensions';
import fonts from 'Styles/Variables/fonts';
import translate from 'Utilities/String/translate';
import createChannelIndexItemSelector from '../createChannelIndexItemSelector';
import selectOverviewOptions from './selectOverviewOptions';
import ChannelIndexOverviewInfo from './ChannelIndexOverviewInfo';
import styles from './ChannelIndexOverview.css';

const columnPadding = parseInt(dimensions.channelIndexColumnPadding);
const columnPaddingSmallScreen = parseInt(
  dimensions.channelIndexColumnPaddingSmallScreen
);
const defaultFontSize = parseInt(fonts.defaultFontSize);
const lineHeight = parseFloat(fonts.lineHeight);

// Hardcoded height based on line-height of 32 + bottom margin of 10.
// Less side-effecty than using react-measure.
const TITLE_HEIGHT = 42;

interface ChannelIndexOverviewProps {
  channelId: number;
  sortKey: string;
  posterWidth: number;
  posterHeight: number;
  rowHeight: number;
  isSelectMode: boolean;
  isSmallScreen: boolean;
}

function ChannelIndexOverview(props: ChannelIndexOverviewProps) {
  const {
    channelId: channelId,
    sortKey,
    posterWidth,
    posterHeight,
    rowHeight,
    isSelectMode,
    isSmallScreen,
  } = props;

  const {
    channel,
    qualityProfile,
    isRefreshingChannel,
    isSearchingChannel,
  } = useSelector(createChannelIndexItemSelector(channelId));

  const overviewOptions = useSelector(selectOverviewOptions);

  const {
    title,
    monitored,
    status,
    path,
    titleSlug,
    nextAiring,
    previousAiring,
    added,
    overview,
    statistics = {} as Statistics,
    images,
    tags,
    network,
  } = channel;
  const thumbnailUrl = (channel as typeof channel & { thumbnailUrl?: string })
    .thumbnailUrl;
  const avatarImages: Image[] = thumbnailUrl
    ? [{ coverType: 'poster', url: thumbnailUrl, remoteUrl: thumbnailUrl }]
    : images;

  const {
    playlistCount = 0,
    videoCount = 0,
    videoFileCount = 0,
    totalVideoCount = 0,
    sizeOnDisk = 0,
  } = statistics;

  const dispatch = useDispatch();
  const [isEditChannelModalOpen, setIsEditChannelModalOpen] = useState(false);
  const [isDeleteChannelModalOpen, setIsDeleteChannelModalOpen] = useState(false);

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

  const link = `/channels/${titleSlug}`;

  const elementStyle = {
    width: `${posterWidth}px`,
    height: `${posterHeight}px`,
  };

  const contentHeight = useMemo(() => {
    const padding = isSmallScreen ? columnPaddingSmallScreen : columnPadding;

    return rowHeight - padding;
  }, [rowHeight, isSmallScreen]);

  const overviewHeight = contentHeight - TITLE_HEIGHT;

  return (
    <div>
      <div className={styles.content}>
        <div className={styles.poster}>
          <div className={styles.posterContainer}>
            {isSelectMode ? (
              <ChannelIndexPosterSelect channelId={channelId} />
            ) : null}

            {status === 'ended' ? (
              <div
                className={classNames(styles.status, styles.ended)}
                title={translate('Ended')}
              />
            ) : null}

            {status === 'deleted' ? (
              <div
                className={classNames(styles.status, styles.deleted)}
                title={translate('Deleted')}
              />
            ) : null}

            <Link className={styles.link} style={elementStyle} to={link}>
              <ChannelPoster
                className={styles.poster}
                style={elementStyle}
                images={avatarImages}
                size={250}
                lazy={false}
                overflow={true}
              />
            </Link>
          </div>

          <ChannelIndexProgressBar
            channelId={channelId}
            monitored={monitored}
            status={status}
            videoCount={videoCount}
            videoFileCount={videoFileCount}
            totalVideoCount={totalVideoCount}
            width={posterWidth}
            detailedProgressBar={overviewOptions.detailedProgressBar}
            isStandalone={false}
          />
        </div>

        <div className={styles.info} style={{ maxHeight: contentHeight }}>
          <div className={styles.titleRow}>
            <Link className={styles.title} to={link}>
              {title}
            </Link>

            <div className={styles.actions}>
              <SpinnerIconButton
                name={icons.REFRESH}
                title={translate('RefreshChannel')}
                isSpinning={isRefreshingChannel}
                onPress={onRefreshPress}
              />

              {overviewOptions.showSearchAction ? (
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
            </div>
          </div>

          <div className={styles.details}>
            <div className={styles.overviewContainer}>
              <Link className={styles.overview} to={link}>
                <TextTruncate
                  line={Math.floor(
                    overviewHeight / (defaultFontSize * lineHeight)
                  )}
                  text={overview}
                />
              </Link>

              {overviewOptions.showTags ? (
                <div className={styles.tags}>
                  <TagListConnector tags={tags} />
                </div>
              ) : null}
            </div>
            <ChannelIndexOverviewInfo
              height={overviewHeight}
              monitored={monitored}
              network={network}
              nextAiring={nextAiring}
              previousAiring={previousAiring}
              added={added}
              playlistCount={playlistCount}
              qualityProfile={qualityProfile}
              sizeOnDisk={sizeOnDisk}
              path={path}
              sortKey={sortKey}
              {...overviewOptions}
            />
          </div>
        </div>
      </div>

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
    </div>
  );
}

export default ChannelIndexOverview;
