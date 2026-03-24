import classNames from 'classnames';
import React, { useCallback, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { REFRESH_CHANNEL, DOWNLOAD_MONITORED } from 'Commands/commandNames';
import Label from 'Components/Label';
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
import createUISettingsSelector from 'Store/Selectors/createUISettingsSelector';
import formatDateTime from 'Utilities/Date/formatDateTime';
import getRelativeDate from 'Utilities/Date/getRelativeDate';
import translate from 'Utilities/String/translate';
import createChannelIndexItemSelector from '../createChannelIndexItemSelector';
import selectPosterOptions from './selectPosterOptions';
import ChannelIndexPosterInfo from './ChannelIndexPosterInfo';
import styles from './ChannelIndexPoster.css';

interface ChannelIndexPosterProps {
  channelId: number;
  sortKey: string;
  isSelectMode: boolean;
  posterWidth: number;
  posterHeight: number;
}

function ChannelIndexPoster(props: ChannelIndexPosterProps) {
  const { channelId: channelId, sortKey, isSelectMode, posterWidth, posterHeight } = props;

  const {
    channel,
    qualityProfile,
    isRefreshingChannel,
    isSearchingChannel,
  } = useSelector(createChannelIndexItemSelector(channelId));

  const {
    detailedProgressBar,
    showTitle,
    showMonitored,
    showQualityProfile,
    showTags,
    showSearchAction,
  } = useSelector(selectPosterOptions);

  const { showRelativeDates, shortDateFormat, longDateFormat, timeFormat } =
    useSelector(createUISettingsSelector());

  const {
    title,
    monitored,
    status,
    path,
    titleSlug,
    originalLanguage,
    network,
    nextAiring,
    previousAiring,
    added,
    statistics = {} as Statistics,
    images,
    tags,
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
  const [hasPosterError, setHasPosterError] = useState(false);
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

  const onPosterLoadError = useCallback(() => {
    setHasPosterError(true);
  }, [setHasPosterError]);

  const onPosterLoad = useCallback(() => {
    setHasPosterError(false);
  }, [setHasPosterError]);

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

  return (
    <div className={styles.content}>
      <div className={styles.posterContainer} title={title}>
        {isSelectMode ? <ChannelIndexPosterSelect channelId={channelId} /> : null}

        <Label className={styles.controls}>
          <SpinnerIconButton
            className={styles.action}
            name={icons.REFRESH}
            title={translate('RefreshChannel')}
            isSpinning={isRefreshingChannel}
            onPress={onRefreshPress}
          />

          {showSearchAction ? (
            <SpinnerIconButton
              className={styles.action}
              name={icons.SEARCH}
              title={translate('SearchForMonitoredVideos')}
              isSpinning={isSearchingChannel}
              onPress={onSearchPress}
            />
          ) : null}

          <IconButton
            className={styles.action}
            name={icons.EDIT}
            title={translate('EditChannel')}
            onPress={onEditChannelPress}
          />
        </Label>

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
            style={elementStyle}
            images={avatarImages}
            size={250}
            lazy={false}
            overflow={true}
            onError={onPosterLoadError}
            onLoad={onPosterLoad}
          />

          {hasPosterError ? (
            <div className={styles.overlayTitle}>{title}</div>
          ) : null}
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
        detailedProgressBar={detailedProgressBar}
        isStandalone={false}
      />

      {showTitle ? (
        <div className={styles.title} title={title}>
          {title}
        </div>
      ) : null}

      {showMonitored ? (
        <div className={styles.title}>
          {monitored ? translate('Monitored') : translate('Unmonitored')}
        </div>
      ) : null}

      {showQualityProfile && !!qualityProfile?.name ? (
        <div className={styles.title} title={translate('QualityProfile')}>
          {qualityProfile.name}
        </div>
      ) : null}

      {nextAiring ? (
        <div
          className={styles.nextAiring}
          title={`${translate('NextAiring')}: ${formatDateTime(
            nextAiring,
            longDateFormat,
            timeFormat
          )}`}
        >
          {getRelativeDate({
            date: nextAiring,
            shortDateFormat,
            showRelativeDates,
            timeFormat,
            timeForToday: true,
          })}
        </div>
      ) : null}

      {showTags && tags.length ? (
        <div className={styles.tags}>
          <div className={styles.tagsList}>
            <TagListConnector tags={tags} />
          </div>
        </div>
      ) : null}

      <ChannelIndexPosterInfo
        originalLanguage={originalLanguage}
        network={network}
        previousAiring={previousAiring}
        added={added}
        playlistCount={playlistCount}
        sizeOnDisk={sizeOnDisk}
        path={path}
        qualityProfile={qualityProfile}
        showQualityProfile={showQualityProfile}
        showRelativeDates={showRelativeDates}
        sortKey={sortKey}
        shortDateFormat={shortDateFormat}
        longDateFormat={longDateFormat}
        timeFormat={timeFormat}
        tags={tags}
        showTags={showTags}
      />

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

export default ChannelIndexPoster;
