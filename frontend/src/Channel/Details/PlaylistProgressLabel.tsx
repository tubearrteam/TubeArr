import React from 'react';
import { useSelector } from 'react-redux';
import Label from 'Components/Label';
import { kinds, sizes } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import createChannelQueueDetailsSelector, {
  ChannelQueueDetails,
} from 'Channel/Index/createChannelQueueDetailsSelector';

function getVideoCountKind(
  isPopulatingVideos: boolean,
  monitored: boolean,
  videoFileCount: number,
  videoCount: number,
  isDownloading: boolean
) {
  if (isPopulatingVideos) {
    return kinds.PURPLE;
  }

  if (isDownloading) {
    return kinds.PURPLE;
  }

  if (videoFileCount === videoCount && videoCount > 0) {
    return kinds.SUCCESS;
  }

  if (!monitored) {
    return kinds.WARNING;
  }

  return kinds.DANGER;
}

interface PlaylistProgressLabelProps {
  channelId: number;
  playlistNumber: number;
  monitored: boolean;
  isPopulatingVideos: boolean;
  videoCount: number;
  videoFileCount: number;
}

function PlaylistProgressLabel({
  channelId: channelId,
  playlistNumber,
  monitored,
  isPopulatingVideos,
  videoCount,
  videoFileCount,
}: PlaylistProgressLabelProps) {
  const queueDetails: ChannelQueueDetails = useSelector(
    createChannelQueueDetailsSelector(channelId, playlistNumber)
  );

  const newDownloads = queueDetails.count - queueDetails.videosWithFiles;
  const text = isPopulatingVideos
    ? translate('PopulatingVideos')
    : newDownloads
      ? `${videoFileCount} + ${newDownloads} / ${videoCount}`
      : `${videoFileCount} / ${videoCount}`;

  return (
    <Label
      kind={getVideoCountKind(
        isPopulatingVideos,
        monitored,
        videoFileCount,
        videoCount,
        queueDetails.count > 0
      )}
      size={sizes.LARGE}
    >
      <span>{text}</span>
    </Label>
  );
}

export default PlaylistProgressLabel;
