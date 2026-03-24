import React from 'react';
import Modal from 'Components/Modal/Modal';
import VideoDetailsTab from 'Video/VideoDetailsTab';
import { VideoEntities } from 'Video/useVideo';
import { sizes } from 'Helpers/Props';
import VideoDetailsModalContent from './VideoDetailsModalContent';

interface VideoDetailsModalProps {
  isOpen: boolean;
  videoId: number;
  videoEntity: VideoEntities;
  channelId: number;
  videoTitle: string;
  isSaving?: boolean;
  showOpenChannelButton?: boolean;
  selectedTab?: VideoDetailsTab;
  startInteractiveSearch?: boolean;
  onModalClose(): void;
}

function VideoDetailsModal(props: VideoDetailsModalProps) {
  const { selectedTab, isOpen, onModalClose, ...otherProps } = props;

  return (
    <Modal
      isOpen={isOpen}
      size={sizes.EXTRA_EXTRA_LARGE}
      closeOnBackgroundClick={selectedTab !== 'search'}
      onModalClose={onModalClose}
    >
      <VideoDetailsModalContent
        {...otherProps}
        selectedTab={selectedTab}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default VideoDetailsModal;
