import React, { useCallback, useEffect } from 'react';
import { useDispatch } from 'react-redux';
import Button from 'Components/Link/Button';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import VideoDetailsTab from 'Video/VideoDetailsTab';
import videoEntities from 'Video/videoEntities';
import useVideo, { VideoEntities } from 'Video/useVideo';
import Channel from 'Channel/Channel';
import useChannel from 'Channel/useChannel';
import { toggleVideoMonitored } from 'Store/Actions/videoActions';
import {
  cancelFetchReleases,
  clearReleases,
} from 'Store/Actions/releaseActions';
import translate from 'Utilities/String/translate';
import Video from 'Video/Video';
import VideoSearch from './Search/VideoSearch';
import PlaylistVideoNumber from './PlaylistVideoNumber';
import VideoSummary from './Summary/VideoSummary';
import styles from './VideoDetailsModalContent.css';

export interface VideoDetailsModalContentProps {
  videoId: number;
  videoEntity: VideoEntities;
  channelId: number;
  videoTitle: string;
  isSaving?: boolean;
  showOpenChannelButton?: boolean;
  selectedTab?: VideoDetailsTab;
  onModalClose(): void;
}

function VideoDetailsModalContent(props: VideoDetailsModalContentProps) {
  const {
    videoId,
    videoEntity = videoEntities.VIDEOS,
    channelId,
    videoTitle,
    isSaving = false,
    showOpenChannelButton = false,
    selectedTab = 'details',
    onModalClose,
  } = props;

  const dispatch = useDispatch();

  const {
    title: channelTitle,
    titleSlug,
    monitored: channelMonitored,
    channelType,
  } = useChannel(channelId) as Channel;

  const {
    videoFileId,
    playlistNumber,
    videoNumber,
    absoluteVideoNumber,
    airDate,
    monitored,
  } = useVideo(videoId, videoEntity) as Video;

  const handleMonitorVideoPress = useCallback(
    (monitored: boolean) => {
      dispatch(
        toggleVideoMonitored({
          videoEntity,
          videoId,
          monitored,
        })
      );
    },
    [videoEntity, videoId, dispatch]
  );

  useEffect(() => {
    return () => {
      // Clear pending releases here, so we can reshow the search
      // results even after switching tabs.
      dispatch(cancelFetchReleases());
      dispatch(clearReleases());
    };
  }, [dispatch]);

  const channelLink = `/channels/${titleSlug}`;
  const isSearchMode = selectedTab === 'search';

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        <MonitorToggleButton
          monitored={monitored}
          size={18}
          isDisabled={!channelMonitored}
          isSaving={isSaving}
          onPress={handleMonitorVideoPress}
        />

        <span className={styles.channelTitle}>{channelTitle}</span>

        <span className={styles.separator}>-</span>

        <PlaylistVideoNumber
          playlistNumber={playlistNumber}
          videoNumber={videoNumber}
          absoluteVideoNumber={absoluteVideoNumber}
          airDate={airDate}
          channelType={channelType}
        />

        <span className={styles.separator}>-</span>

        {videoTitle}
      </ModalHeader>

      <ModalBody>
        {isSearchMode ? (
          <VideoSearch
            videoId={videoId}
            onModalClose={onModalClose}
          />
        ) : (
          <div className={styles.content}>
            <VideoSummary
              videoId={videoId}
              videoEntity={videoEntity}
              videoFileId={videoFileId}
              channelId={channelId}
            />
          </div>
        )}
      </ModalBody>

      <ModalFooter>
        {showOpenChannelButton && (
          <Button
            className={styles.openChannelButton}
            to={channelLink}
            onPress={onModalClose}
          >
            {translate('OpenChannel')}
          </Button>
        )}

        <Button onPress={onModalClose}>{translate('Close')}</Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default VideoDetailsModalContent;
