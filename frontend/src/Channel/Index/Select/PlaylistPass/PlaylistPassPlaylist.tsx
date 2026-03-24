import classNames from 'classnames';
import React, { useCallback } from 'react';
import { useDispatch } from 'react-redux';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import formatPlaylist from 'Playlist/formatPlaylist';
import { Statistics } from 'Channel/Channel';
import { togglePlaylistMonitored } from 'Store/Actions/channelActions';
import translate from 'Utilities/String/translate';
import styles from './PlaylistPassPlaylist.css';

interface PlaylistPassPlaylistProps {
  channelId: number;
  playlistNumber: number;
  monitored: boolean;
  statistics: Statistics;
  isSaving: boolean;
}

function PlaylistPassPlaylist(props: PlaylistPassPlaylistProps) {
  const {
    channelId: channelId,
    playlistNumber,
    monitored,
    statistics = {
      videoFileCount: 0,
      totalVideoCount: 0,
      percentOfVideos: 0,
    },
    isSaving = false,
  } = props;

  const { videoFileCount, totalVideoCount, percentOfVideos } = statistics;

  const dispatch = useDispatch();
  const onPlaylistMonitoredPress = useCallback(() => {
    dispatch(
      togglePlaylistMonitored({
        channelId: channelId,
        playlistNumber,
        monitored: !monitored,
      })
    );
  }, [channelId, playlistNumber, monitored, dispatch]);

  return (
    <div className={styles.playlist}>
      <div className={styles.info}>
        <MonitorToggleButton
          monitored={monitored}
          isSaving={isSaving}
          onPress={onPlaylistMonitoredPress}
        />

        <span>{formatPlaylist(playlistNumber, true)}</span>
      </div>

      <div
        className={classNames(
          styles.videos,
          percentOfVideos === 100 && styles.allVideos
        )}
        title={translate('PlaylistPassVideosDownloaded', {
          videoFileCount,
          totalVideoCount,
        })}
      >
        {totalVideoCount === 0
          ? '0/0'
          : `${videoFileCount}/${totalVideoCount}`}
      </div>
    </div>
  );
}

export default PlaylistPassPlaylist;
