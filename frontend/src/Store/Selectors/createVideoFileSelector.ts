import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';

function createVideoFileSelector() {
  return createSelector(
    (_: AppState, { videoFileId }: { videoFileId: number }) =>
      videoFileId,
    (state: AppState) => state.videoFiles,
    (videoFileId, videoFiles) => {
      if (!videoFileId) {
        return;
      }

      return videoFiles.items.find(
        (videoFile) => videoFile.id === videoFileId
      );
    }
  );
}

export default createVideoFileSelector;
