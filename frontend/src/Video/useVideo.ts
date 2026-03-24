import { useSelector } from 'react-redux';
import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';
import Video from './Video';

export type VideoEntities =
  | 'calendar'
  | 'videos'
  | 'interactiveImport.videos'
  | 'wanted.cutoffUnmet'
  | 'wanted.missing';

function createVideoSelector(videoId?: number) {
  return createSelector(
    (state: AppState) => state.videos.items,
    (videos) => {
      return videos.find(({ id }) => id === videoId);
    }
  );
}

function createCalendarVideoSelector(videoId?: number) {
  return createSelector(
    (state: AppState) => state.calendar.items as Video[],
    (videos) => {
      return videos.find(({ id }) => id === videoId);
    }
  );
}

function createWantedCutoffUnmetVideoSelector(videoId?: number) {
  return createSelector(
    (state: AppState) => state.wanted.cutoffUnmet.items,
    (videos) => {
      return videos.find(({ id }) => id === videoId);
    }
  );
}

function createWantedMissingVideoSelector(videoId?: number) {
  return createSelector(
    (state: AppState) => state.wanted.missing.items,
    (videos) => {
      return videos.find(({ id }) => id === videoId);
    }
  );
}

function useVideo(
  videoId: number | undefined,
  videoEntity: VideoEntities
) {
  let selector = createVideoSelector;

  switch (videoEntity) {
    case 'calendar':
      selector = createCalendarVideoSelector;
      break;
    case 'wanted.cutoffUnmet':
      selector = createWantedCutoffUnmetVideoSelector;
      break;
    case 'wanted.missing':
      selector = createWantedMissingVideoSelector;
      break;
    default:
      break;
  }

  return useSelector(selector(videoId));
}

export default useVideo;
