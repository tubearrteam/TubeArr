import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import createVideoFileSelector from 'Store/Selectors/createVideoFileSelector';
import createChannelSelector from 'Store/Selectors/createChannelSelector';
import VideoRow from './VideoRow';

function createMapStateToProps() {
  return createSelector(
    createChannelSelector(),
    createVideoFileSelector(),
    (_, props) => props.runtime,
    (channel = {}, videoFile, videoRuntimeSeconds) => {
      const fileDur = videoFile && videoFile.fileDurationSeconds != null && videoFile.fileDurationSeconds > 0
        ? videoFile.fileDurationSeconds
        : null;
      return {
        channelMonitored: channel.monitored,
        channelType: channel.channelType,
        videoFilePath: videoFile ? videoFile.path : null,
        videoFileRelativePath: videoFile ? videoFile.relativePath : null,
        videoFileSize: videoFile ? videoFile.size : null,
        releaseGroup: videoFile ? videoFile.releaseGroup : null,
        customFormats: videoFile ? videoFile.customFormats : [],
        customFormatScore: videoFile ? videoFile.customFormatScore : 0,
        indexerFlags: videoFile ? videoFile.indexerFlags : 0,
        runtime: fileDur != null ? fileDur : videoRuntimeSeconds
      };
    }
  );
}
export default connect(createMapStateToProps)(VideoRow);
