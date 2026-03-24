import React from 'react';
import { ChannelType } from 'Channel/Channel';
import VideoNumber, { VideoNumberProps } from './VideoNumber';

interface PlaylistVideoNumberProps extends VideoNumberProps {
  airDate?: string;
  channelType?: ChannelType;
}

function PlaylistVideoNumber(props: PlaylistVideoNumberProps) {
  const { airDate, channelType, ...otherProps } = props;

  if (channelType === 'daily' && airDate) {
    return <span>{airDate}</span>;
  }

  return (
    <VideoNumber
      channelType={channelType}
      showPlaylistNumber={true}
      {...otherProps}
    />
  );
}

export default PlaylistVideoNumber;
