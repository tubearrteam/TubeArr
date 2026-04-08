import React, { useCallback, useEffect } from 'react';
import { useDispatch } from 'react-redux';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import Link from 'Components/Link/Link';
import Column from 'Components/Table/Column';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import Video from 'Video/Video';
import useVideo, { VideoEntities } from 'Video/useVideo';
import useVideoFile from 'VideoFile/useVideoFile';
import { icons, kinds, sizes } from 'Helpers/Props';
import Channel from 'Channel/Channel';
import useChannel from 'Channel/useChannel';
import QualityProfileNameConnector from 'Settings/Profiles/Quality/QualityProfileNameConnector';
import {
  deleteVideoFile,
  fetchVideoFile,
} from 'Store/Actions/videoFileActions';
import translate from 'Utilities/String/translate';
import VideoAiring from './VideoAiring';
import VideoFileRow from './VideoFileRow';
import VideoSlskdPanel from './VideoSlskdPanel';
import styles from './VideoSummary.css';

const COLUMNS: Column[] = [
  {
    name: 'path',
    label: () => translate('Path'),
    isSortable: false,
    isVisible: true,
  },
  {
    name: 'size',
    label: () => translate('Size'),
    isSortable: false,
    isVisible: true,
  },
  {
    name: 'languages',
    label: () => translate('Languages'),
    isSortable: false,
    isVisible: true,
  },
  {
    name: 'quality',
    label: () => translate('Quality'),
    isSortable: false,
    isVisible: true,
  },
  {
    name: 'customFormats',
    label: () => translate('Formats'),
    isSortable: false,
    isVisible: true,
  },
  {
    name: 'customFormatScore',
    label: React.createElement(Icon, {
      name: icons.SCORE,
      title: () => translate('CustomFormatScore'),
    }),
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'actions',
    label: '',
    isSortable: false,
    isVisible: true,
  },
];

interface VideoSummaryProps {
  channelId: number;
  videoId: number;
  videoEntity: VideoEntities;
  videoFileId?: number;
}

function VideoSummary(props: VideoSummaryProps) {
  const { channelId, videoId, videoEntity, videoFileId } = props;

  const dispatch = useDispatch();

  const { qualityProfileId, network } = useChannel(channelId) as Channel;

  const { airDateUtc, overview, thumbnailUrl, youtubeVideoId, title } = useVideo(
    videoId,
    videoEntity
  ) as Video;

  const {
    path,
    mediaInfo,
    size,
    languages,
    quality,
    qualityCutoffNotMet,
    customFormats,
    customFormatScore,
  } = useVideoFile(videoFileId) || {};

  const handleDeleteVideoFile = useCallback(() => {
    dispatch(
      deleteVideoFile({
        id: videoFileId,
        videoEntity,
      })
    );
  }, [videoFileId, videoEntity, dispatch]);

  useEffect(() => {
    if (videoFileId && !path) {
      dispatch(fetchVideoFile({ id: videoFileId }));
    }
  }, [videoFileId, path, dispatch]);

  const hasOverview = !!overview;
  const displayThumbnailUrl =
    thumbnailUrl ||
    (youtubeVideoId
      ? `https://i.ytimg.com/vi/${youtubeVideoId}/hqdefault.jpg`
      : undefined);
  const youtubeUrl = youtubeVideoId
    ? `https://www.youtube.com/watch?v=${youtubeVideoId}`
    : undefined;

  return (
    <div>
      {(displayThumbnailUrl || youtubeUrl) ? (
        <div className={styles.header}>
          {displayThumbnailUrl ? (
            <img
              className={styles.thumbnail}
              src={displayThumbnailUrl}
              alt={title}
            />
          ) : null}

          {youtubeUrl ? (
            <div className={styles.linkRow}>
              <Link
                className={styles.youtubeLink}
                to={youtubeUrl}
              >
                <Icon className={styles.youtubeLinkIcon} name={icons.EXTERNAL_LINK} />
                {translate('OpenOnYouTube')}
              </Link>
            </div>
          ) : null}
        </div>
      ) : null}

      <div>
        <span className={styles.infoTitle}>{translate('Airs')}</span>

        <VideoAiring airDateUtc={airDateUtc} network={network} />
      </div>

      <div>
        <span className={styles.infoTitle}>{translate('QualityProfile')}</span>

        <Label kind={kinds.PRIMARY} size={sizes.MEDIUM}>
          {qualityProfileId != null && qualityProfileId > 0 ? (
            <QualityProfileNameConnector qualityProfileId={qualityProfileId} />
          ) : (
            translate('NoQualityProfile')
          )}
        </Label>
      </div>

      <div className={styles.overview}>
        {hasOverview ? overview : translate('NoVideoOverview')}
      </div>

      {path ? (
        <Table columns={COLUMNS}>
          <TableBody>
            <VideoFileRow
              path={path}
              size={size!}
              languages={languages!}
              quality={quality!}
              qualityCutoffNotMet={qualityCutoffNotMet!}
              customFormats={customFormats!}
              customFormatScore={customFormatScore!}
              mediaInfo={mediaInfo!}
              columns={COLUMNS}
              onDeleteVideoFile={handleDeleteVideoFile}
            />
          </TableBody>
        </Table>
      ) : null}

      <VideoSlskdPanel videoId={videoId} />
    </div>
  );
}

export default VideoSummary;
