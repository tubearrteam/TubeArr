import PropTypes from 'prop-types';
import React, { Component } from 'react';
import HistoryDetails from 'Activity/History/Details/HistoryDetails';
import HistoryEventTypeCell from 'Activity/History/HistoryEventTypeCell';
import Icon from 'Components/Icon';
import IconButton from 'Components/Link/IconButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import RelativeDateCell from 'Components/Table/Cells/RelativeDateCell';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableRow from 'Components/Table/TableRow';
import Popover from 'Components/Tooltip/Popover';
import VideoFormats from 'Video/VideoFormats';
import VideoLanguages from 'Video/VideoLanguages';
import VideoNumber from 'Video/VideoNumber';
import VideoQuality from 'Video/VideoQuality';
import PlaylistVideoNumber from 'Video/PlaylistVideoNumber';
import { icons, kinds, tooltipPositions } from 'Helpers/Props';
import formatCustomFormatScore from 'Utilities/Number/formatCustomFormatScore';
import translate from 'Utilities/String/translate';
import styles from './ChannelHistoryRow.css';

function getTitle(eventType) {
  switch (eventType) {
    case 'grabbed': return 'Grabbed';
    case 'channelFolderImported': return 'Channel Folder Imported';
    case 'downloadFolderImported': return 'Download Folder Imported';
    case 'downloadFailed': return 'Download Failed';
    case 'videoFileDeleted': return 'Video File Deleted';
    case 'videoFileRenamed': return 'Video File Renamed';
    default: return 'Unknown';
  }
}

class ChannelHistoryRow extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      isMarkAsFailedModalOpen: false
    };
  }

  //
  // Listeners

  onMarkAsFailedPress = () => {
    this.setState({ isMarkAsFailedModalOpen: true });
  };

  onConfirmMarkAsFailed = () => {
    this.props.onMarkAsFailedPress(this.props.id);
    this.setState({ isMarkAsFailedModalOpen: false });
  };

  onMarkAsFailedModalClose = () => {
    this.setState({ isMarkAsFailedModalOpen: false });
  };

  //
  // Render

  render() {
    const {
      eventType,
      sourceTitle,
      languages,
      quality,
      qualityCutoffNotMet,
      customFormats,
      date,
      data,
      downloadId,
      isFullChannelHistory,
      channel,
      video,
      customFormatScore
    } = this.props;

    const {
      isMarkAsFailedModalOpen
    } = this.state;

    const VideoNumberComponent = isFullChannelHistory ? PlaylistVideoNumber : VideoNumber;

    if (!channel || !video) {
      return null;
    }

    return (
      <TableRow>
        <HistoryEventTypeCell
          eventType={eventType}
          data={data}
        />

        <TableRowCell key={name}>
          <VideoNumberComponent
            playlistNumber={video.playlistNumber}
            videoNumber={video.videoNumber}
            absoluteVideoNumber={video.absoluteVideoNumber}
            channelType={channel.channelType}
          />
        </TableRowCell>

        <TableRowCell className={styles.sourceTitle}>
          {sourceTitle}
        </TableRowCell>

        <TableRowCell>
          <VideoLanguages languages={languages} />
        </TableRowCell>

        <TableRowCell>
          <VideoQuality
            quality={quality}
            isCutoffNotMet={qualityCutoffNotMet}
          />
        </TableRowCell>

        <TableRowCell>
          <VideoFormats formats={customFormats} />
        </TableRowCell>

        <TableRowCell>
          {formatCustomFormatScore(customFormatScore, customFormats.length)}
        </TableRowCell>

        <RelativeDateCell
          date={date}
          includeSeconds={true}
          includeTime={true}
        />

        <TableRowCell className={styles.actions}>
          <Popover
            anchor={
              <Icon
                name={icons.INFO}
              />
            }
            title={getTitle(eventType)}
            body={
              <HistoryDetails
                eventType={eventType}
                sourceTitle={sourceTitle}
                data={data}
                downloadId={downloadId}
              />
            }
            position={tooltipPositions.LEFT}
          />

          {
            eventType === 'grabbed' &&
              <IconButton
                title={translate('MarkAsFailed')}
                name={icons.REMOVE}
                size={14}
                onPress={this.onMarkAsFailedPress}
              />
          }
        </TableRowCell>

        <ConfirmModal
          isOpen={isMarkAsFailedModalOpen}
          kind={kinds.DANGER}
          title={translate('MarkAsFailed')}
          message={translate('MarkAsFailedConfirmation', { sourceTitle })}
          confirmLabel={translate('MarkAsFailed')}
          onConfirm={this.onConfirmMarkAsFailed}
          onCancel={this.onMarkAsFailedModalClose}
        />
      </TableRow>
    );
  }
}

ChannelHistoryRow.propTypes = {
  id: PropTypes.number.isRequired,
  eventType: PropTypes.string.isRequired,
  sourceTitle: PropTypes.string.isRequired,
  languages: PropTypes.arrayOf(PropTypes.object),
  quality: PropTypes.object.isRequired,
  qualityCutoffNotMet: PropTypes.bool.isRequired,
  customFormats: PropTypes.arrayOf(PropTypes.object),
  date: PropTypes.string.isRequired,
  data: PropTypes.object.isRequired,
  downloadId: PropTypes.string,
  isFullChannelHistory: PropTypes.bool.isRequired,
  channel: PropTypes.object.isRequired,
  video: PropTypes.object.isRequired,
  customFormatScore: PropTypes.number.isRequired,
  onMarkAsFailedPress: PropTypes.func.isRequired
};

export default ChannelHistoryRow;
