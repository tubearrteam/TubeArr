import PropTypes from 'prop-types';
import React from 'react';
import Icon from 'Components/Icon';
import IconButton from 'Components/Link/IconButton';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import VideoTitleLink from 'Video/VideoTitleLink';
import VideoStatus from 'Video/VideoStatus';
import PlaylistVideoNumber from 'Video/PlaylistVideoNumber';
import { icons } from 'Helpers/Props';
import formatRuntime from 'Utilities/Number/formatRuntime';
import translate from 'Utilities/String/translate';
import styles from './ChannelDetailsVideoGallery.css';

function getThumbnailUrl(item) {
  if (item.thumbnailUrl) {
    return item.thumbnailUrl;
  }

  if (item.youtubeVideoId) {
    return `https://i.ytimg.com/vi/${item.youtubeVideoId}/hqdefault.jpg`;
  }

  return null;
}

function ChannelDetailsVideoGallery(props) {
  const {
    items,
    channelId,
    channelMonitored,
    channelType,
    onMonitorVideoPress,
    onDownloadVideoPress
  } = props;

  return (
    <div className={styles.gallery}>
      {items.map((item) => {
        const thumbnailUrl = getThumbnailUrl(item);

        return (
          <div key={item.id} className={styles.card}>
            <div className={styles.posterContainer}>
              {
                thumbnailUrl ?
                  <img
                    className={styles.poster}
                    src={thumbnailUrl}
                    alt={item.title}
                  /> :
                  <div className={styles.posterFallback}>
                    <Icon name={icons.VIDEO_FILE} />
                  </div>
              }

              <div className={styles.cardActions}>
                <IconButton
                  name={icons.DOWNLOAD}
                  title={translate('QueueVideoDownload')}
                  onPress={() => onDownloadVideoPress(item.id)}
                />

                <MonitorToggleButton
                  monitored={item.monitored}
                  isDisabled={!channelMonitored}
                  isSaving={item.isSaving}
                  onPress={(monitored, options) => onMonitorVideoPress(item.id, monitored, options)}
                />
              </div>

              <div className={styles.cardStatus}>
                <VideoStatus
                  videoId={item.id}
                  videoEntity="videos"
                  videoFileId={item.videoFileId}
                />
              </div>
            </div>

            <div className={styles.cardBody}>
              <div className={styles.videoNumber}>
                <PlaylistVideoNumber
                  playlistNumber={item.playlistNumber}
                  videoNumber={item.videoNumber}
                  absoluteVideoNumber={item.absoluteVideoNumber}
                  airDate={item.airDate}
                  channelType={channelType}
                />
              </div>

              <div className={styles.title}>
                <VideoTitleLink
                  videoId={item.id}
                  channelId={channelId}
                  videoTitle={item.title}
                  videoEntity="videos"
                  finaleType={item.finaleType}
                  showOpenChannelButton={false}
                />
              </div>

              <div className={styles.meta}>
                <span>{item.airDate || translate('Tba')}</span>
                {item.runtime ? (
                  <span>{formatRuntime(item.runtime)}</span>
                ) : null}
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
}

ChannelDetailsVideoGallery.propTypes = {
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  channelId: PropTypes.number.isRequired,
  channelMonitored: PropTypes.bool.isRequired,
  channelType: PropTypes.string,
  onMonitorVideoPress: PropTypes.func.isRequired,
  onDownloadVideoPress: PropTypes.func.isRequired
};

ChannelDetailsVideoGallery.defaultProps = {
  channelType: 'standard'
};

export default ChannelDetailsVideoGallery;
