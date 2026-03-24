import React from 'react';
import VideoLanguages from 'Video/VideoLanguages';
import useVideoFile from './useVideoFile';

interface VideoFileLanguagesProps {
  videoFileId: number;
}

function VideoFileLanguages({ videoFileId }: VideoFileLanguagesProps) {
  const videoFile = useVideoFile(videoFileId);

  return <VideoLanguages languages={videoFile?.languages ?? []} />;
}

export default VideoFileLanguages;
