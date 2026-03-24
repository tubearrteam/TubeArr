import React from 'react';
import VirtualTableRowCell from 'Components/Table/Cells/VirtualTableRowCell';
import Popover from 'Components/Tooltip/Popover';
import PlaylistDetails from 'Channel/Index/Select/PlaylistPass/PlaylistDetails';
import { Playlist } from 'Channel/Channel';
import translate from 'Utilities/String/translate';
import styles from './PlaylistsCell.css';

interface PlaylistsCellProps {
  className: string;
  channelId: number;
  playlistCount: number;
  playlists: Playlist[];
  isSelectMode: boolean;
}

function PlaylistsCell(props: PlaylistsCellProps) {
  const {
    className,
    channelId: channelId,
    playlistCount,
    playlists,
    isSelectMode,
    ...otherProps
  } = props;

  return (
    <VirtualTableRowCell className={className} {...otherProps}>
      {isSelectMode ? (
        <Popover
          className={styles.playlistCount}
          anchor={playlistCount}
          title={translate('PlaylistDetails')}
          body={<PlaylistDetails channelId={channelId} playlists={playlists} />}
          position="left"
        />
      ) : (
        playlistCount
      )}
    </VirtualTableRowCell>
  );
}

export default PlaylistsCell;
