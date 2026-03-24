import { useSelector } from 'react-redux';
import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';

function createVideoFileSelector(videoFileId?: number) {
  return createSelector(
    (state: AppState) => state.videoFiles.items,
    (videoFiles) => {
      return videoFiles.find(({ id }) => id === videoFileId);
    }
  );
}

function useVideoFile(videoFileId: number | undefined) {
  return useSelector(createVideoFileSelector(videoFileId));
}

export default useVideoFile;
