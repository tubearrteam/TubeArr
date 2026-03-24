import React, { useMemo } from 'react';
import { Playlist } from 'Channel/Channel';
import translate from 'Utilities/String/translate';
import PlaylistPassPlaylist from './PlaylistPassPlaylist';
import styles from './PlaylistDetails.css';

interface PlaylistDetailsProps {
  channelId: number;
  playlists: Playlist[];
}

function PlaylistDetails(props: PlaylistDetailsProps) {
  const { channelId: channelId, playlists } = props;

  const latestPlaylists = useMemo(() => {
    return playlists.slice(Math.max(playlists.length - 25, 0));
  }, [playlists]);

  return (
    <div className={styles.playlists}>
      {latestPlaylists.map((playlist) => {
        const {
          playlistNumber,
          monitored,
          statistics,
          isSaving = false,
        } = playlist;

        return (
          <PlaylistPassPlaylist
            key={playlistNumber}
            channelId={channelId}
            playlistNumber={playlistNumber}
            monitored={monitored}
            statistics={statistics}
            isSaving={isSaving}
          />
        );
      })}

      {latestPlaylists.length < playlists.length ? (
        <div className={styles.truncated}>
          {translate('PlaylistPassTruncated')}
        </div>
      ) : null}
    </div>
  );
}

export default PlaylistDetails;
