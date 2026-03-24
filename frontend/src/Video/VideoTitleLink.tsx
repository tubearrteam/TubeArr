import React, { useCallback, useState } from 'react';
import Link from 'Components/Link/Link';
import VideoDetailsModal from 'Video/VideoDetailsModal';
import { VideoEntities } from 'Video/useVideo';
import FinaleType from './FinaleType';
import styles from './VideoTitleLink.css';

interface VideoTitleLinkProps {
  videoId: number;
  channelId: number;
  videoEntity: VideoEntities;
  videoTitle: string;
  finaleType?: string;
  showOpenChannelButton: boolean;
}

function VideoTitleLink(props: VideoTitleLinkProps) {
  const { videoTitle, finaleType, ...otherProps } = props;
  const [isDetailsModalOpen, setIsDetailsModalOpen] = useState(false);
  const handleLinkPress = useCallback(() => {
    setIsDetailsModalOpen(true);
  }, [setIsDetailsModalOpen]);
  const handleModalClose = useCallback(() => {
    setIsDetailsModalOpen(false);
  }, [setIsDetailsModalOpen]);

  return (
    <div className={styles.container}>
      <Link className={styles.link} onPress={handleLinkPress}>
        {videoTitle}
      </Link>

      {finaleType ? <FinaleType finaleType={finaleType} /> : null}

      <VideoDetailsModal
        isOpen={isDetailsModalOpen}
        videoTitle={videoTitle}
        {...otherProps}
        onModalClose={handleModalClose}
      />
    </div>
  );
}

export default VideoTitleLink;
