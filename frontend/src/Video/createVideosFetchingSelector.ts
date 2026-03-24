import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';

function createVideosFetchingSelector() {
  return createSelector(
    (state: AppState) => state.videos,
    (videos) => {
      return {
        isVideosFetching: videos.isFetching,
        isVideosPopulated: videos.isPopulated,
        videosError: videos.error,
      };
    }
  );
}

export default createVideosFetchingSelector;
