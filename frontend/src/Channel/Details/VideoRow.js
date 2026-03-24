import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Icon from 'Components/Icon';
import IconButton from 'Components/Link/IconButton';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import RelativeDateCell from 'Components/Table/Cells/RelativeDateCell';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableRow from 'Components/Table/TableRow';
import Popover from 'Components/Tooltip/Popover';
import Tooltip from 'Components/Tooltip/Tooltip';
import VideoFormats from 'Video/VideoFormats';
import VideoNumber from 'Video/VideoNumber';
import VideoStatus from 'Video/VideoStatus';
import VideoTitleLink from 'Video/VideoTitleLink';
import VideoFileLanguages from 'VideoFile/VideoFileLanguages';
import MediaInfo from 'VideoFile/MediaInfo';
import * as mediaInfoTypes from 'VideoFile/mediaInfoTypes';
import { icons, kinds, tooltipPositions } from 'Helpers/Props';
import formatBytes from 'Utilities/Number/formatBytes';
import formatCustomFormatScore from 'Utilities/Number/formatCustomFormatScore';
import formatRuntime from 'Utilities/Number/formatRuntime';
import translate from 'Utilities/String/translate';
import styles from './VideoRow.css';

class VideoRow extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      isDetailsModalOpen: false
    };
  }

  //
  // Listeners

  onManualSearchPress = () => {
    this.setState({ isDetailsModalOpen: true });
  };

  onDetailsModalClose = () => {
    this.setState({ isDetailsModalOpen: false });
  };

  onMonitorVideoPress = (monitored, options) => {
    this.props.onMonitorVideoPress(this.props.id, monitored, options);
  };

  //
  // Render

  render() {
    const {
      id,
      channelId: channelId,
      videoFileId,
      monitored,
      playlistNumber,
      videoNumber,
      absoluteVideoNumber,
      airDateUtc,
      uploadDateUtc,
      runtime,
      finaleType,
      title,
      isSaving,
      channelMonitored,
      channelType: channelType,
      videoFilePath,
      videoFileRelativePath,
      videoFileSize,
      releaseGroup,
      customFormats,
      customFormatScore,
      indexerFlags,
      columns
    } = this.props;

    return (
      <TableRow>
        {
          columns.map((column) => {
            const {
              name,
              isVisible
            } = column;

            if (!isVisible) {
              return null;
            }

            if (name === 'monitored') {
              return (
                <TableRowCell
                  key={name}
                  className={styles.monitored}
                >
                  <MonitorToggleButton
                    monitored={monitored}
                    isDisabled={!channelMonitored}
                    isSaving={isSaving}
                    onPress={this.onMonitorVideoPress}
                  />
                </TableRowCell>
              );
            }

            if (name === 'videoNumber') {
              const isEpisodic = channelType === 'episodic';

              return (
                <TableRowCell
                  key={name}
                  className={isEpisodic ? styles.videoNumberEpisodic : styles.videoNumber}
                >
                  <VideoNumber
                    playlistNumber={playlistNumber}
                    videoNumber={videoNumber}
                    absoluteVideoNumber={absoluteVideoNumber}
                    channelType={channelType}
                  />
                </TableRowCell>
              );
            }

            if (name === 'title') {
              return (
                <TableRowCell
                  key={name}
                  className={styles.title}
                >
                  <VideoTitleLink
                    videoId={id}
                    channelId={channelId}
                    videoTitle={title}
                    videoEntity="videos"
                    finaleType={finaleType}
                    showOpenChannelButton={false}
                  />
                </TableRowCell>
              );
            }

            if (name === 'path') {
              return (
                <TableRowCell key={name}>
                  {
                    videoFilePath
                  }
                </TableRowCell>
              );
            }

            if (name === 'relativePath') {
              return (
                <TableRowCell key={name}>
                  {
                    videoFileRelativePath
                  }
                </TableRowCell>
              );
            }

            if (name === 'airDateUtc' || name === 'uploadDateUtc') {
              const date = uploadDateUtc ?? airDateUtc;
              return (
                <RelativeDateCell
                  key={name}
                  date={date}
                />
              );
            }

            if (name === 'runtime') {
              return (
                <TableRowCell
                  key={name}
                  className={styles.runtime}
                >
                  { formatRuntime(runtime) }
                </TableRowCell>
              );
            }

            if (name === 'customFormats') {
              return (
                <TableRowCell key={name}>
                  <VideoFormats
                    formats={customFormats}
                  />
                </TableRowCell>
              );
            }

            if (name === 'customFormatScore') {
              return (
                <TableRowCell
                  key={name}
                  className={styles.customFormatScore}
                >
                  <Tooltip
                    anchor={formatCustomFormatScore(
                      customFormatScore,
                      customFormats.length
                    )}
                    tooltip={<VideoFormats formats={customFormats} />}
                    position={tooltipPositions.LEFT}
                  />
                </TableRowCell>
              );
            }

            if (name === 'languages') {
              return (
                <TableRowCell
                  key={name}
                  className={styles.languages}
                >
                  <VideoFileLanguages
                    videoFileId={videoFileId}
                  />
                </TableRowCell>
              );
            }

            if (name === 'audioInfo') {
              return (
                <TableRowCell
                  key={name}
                  className={styles.audio}
                >
                  <MediaInfo
                    type={mediaInfoTypes.AUDIO}
                    videoFileId={videoFileId}
                  />
                </TableRowCell>
              );
            }

            if (name === 'audioLanguages') {
              return (
                <TableRowCell
                  key={name}
                  className={styles.audioLanguages}
                >
                  <MediaInfo
                    type={mediaInfoTypes.AUDIO_LANGUAGES}
                    videoFileId={videoFileId}
                  />
                </TableRowCell>
              );
            }

            if (name === 'subtitleLanguages') {
              return (
                <TableRowCell
                  key={name}
                  className={styles.subtitles}
                >
                  <MediaInfo
                    type={mediaInfoTypes.SUBTITLES}
                    videoFileId={videoFileId}
                  />
                </TableRowCell>
              );
            }

            if (name === 'videoCodec') {
              return (
                <TableRowCell
                  key={name}
                  className={styles.video}
                >
                  <MediaInfo
                    type={mediaInfoTypes.VIDEO}
                    videoFileId={videoFileId}
                  />
                </TableRowCell>
              );
            }

            if (name === 'videoDynamicRangeType') {
              return (
                <TableRowCell
                  key={name}
                  className={styles.videoDynamicRangeType}
                >
                  <MediaInfo
                    type={mediaInfoTypes.VIDEO_DYNAMIC_RANGE_TYPE}
                    videoFileId={videoFileId}
                  />
                </TableRowCell>
              );
            }

            if (name === 'size') {
              return (
                <TableRowCell
                  key={name}
                  className={styles.size}
                >
                  {!!videoFileSize && formatBytes(videoFileSize)}
                </TableRowCell>
              );
            }

            if (name === 'releaseGroup') {
              return (
                <TableRowCell
                  key={name}
                  className={styles.releaseGroup}
                >
                  {releaseGroup}
                </TableRowCell>
              );
            }

            if (name === 'indexerFlags') {
              return null;
            }

            if (name === 'status') {
              return (
                <TableRowCell
                  key={name}
                  className={styles.status}
                >
                  <VideoStatus
                    videoId={id}
                    videoFileId={videoFileId}
                  />
                </TableRowCell>
              );
            }

            if (name === 'actions') {
              return (
                <TableRowCell
                  key={name}
                  className={styles.actions}
                >
                  <IconButton
                    name={icons.DOWNLOAD}
                    title={translate('QueueVideoDownload')}
                    onPress={() => this.props.onDownloadVideoPress(id)}
                  />
                </TableRowCell>
              );
            }

            return null;
          })
        }
      </TableRow>
    );
  }
}

VideoRow.propTypes = {
  id: PropTypes.number.isRequired,
  channelId: PropTypes.number.isRequired,
  videoFileId: PropTypes.number,
  monitored: PropTypes.bool.isRequired,
  playlistNumber: PropTypes.number.isRequired,
  videoNumber: PropTypes.number.isRequired,
  absoluteVideoNumber: PropTypes.number,
  airDateUtc: PropTypes.string,
  uploadDateUtc: PropTypes.string,
  runtime: PropTypes.number,
  finaleType: PropTypes.string,
  title: PropTypes.string.isRequired,
  isSaving: PropTypes.bool,
  channelMonitored: PropTypes.bool.isRequired,
  channelType: PropTypes.string.isRequired,
  videoFilePath: PropTypes.string,
  videoFileRelativePath: PropTypes.string,
  videoFileSize: PropTypes.number,
  releaseGroup: PropTypes.string,
  customFormats: PropTypes.arrayOf(PropTypes.object),
  customFormatScore: PropTypes.number.isRequired,
  indexerFlags: PropTypes.number.isRequired,
  mediaInfo: PropTypes.object,
  columns: PropTypes.arrayOf(PropTypes.object).isRequired,
  onMonitorVideoPress: PropTypes.func.isRequired,
  onDownloadVideoPress: PropTypes.func.isRequired
};

VideoRow.defaultProps = {
  customFormats: [],
  indexerFlags: 0
};

export default VideoRow;
