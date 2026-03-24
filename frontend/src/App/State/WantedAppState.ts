import AppSectionState from 'App/State/AppSectionState';
import Video from 'Video/Video';

type WantedCutoffUnmetAppState = AppSectionState<Video>;

type WantedMissingAppState = AppSectionState<Video>;

interface WantedAppState {
  cutoffUnmet: WantedCutoffUnmetAppState;
  missing: WantedMissingAppState;
}

export default WantedAppState;
