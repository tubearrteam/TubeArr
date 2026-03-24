import AppSectionState, {
  AppSectionDeleteState,
} from 'App/State/AppSectionState';
import { VideoFile } from 'VideoFile/VideoFile';

interface VideoFilesAppState
  extends AppSectionState<VideoFile>,
    AppSectionDeleteState {}

export default VideoFilesAppState;
