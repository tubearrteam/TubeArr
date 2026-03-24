import React, { useCallback } from 'react';
import Icon from 'Components/Icon';
import IconButton from 'Components/Link/IconButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import Column from 'Components/Table/Column';
import TableRow from 'Components/Table/TableRow';
import Popover from 'Components/Tooltip/Popover';
import VideoFormats from 'Video/VideoFormats';
import VideoLanguages from 'Video/VideoLanguages';
import VideoQuality from 'Video/VideoQuality';
import { VideoFile } from 'VideoFile/VideoFile';
import useModalOpenState from 'Helpers/Hooks/useModalOpenState';
import { icons, kinds, tooltipPositions } from 'Helpers/Props';
import formatBytes from 'Utilities/Number/formatBytes';
import formatCustomFormatScore from 'Utilities/Number/formatCustomFormatScore';
import translate from 'Utilities/String/translate';
import MediaInfo from './MediaInfo';
import styles from './VideoFileRow.css';

interface VideoFileRowProps
  extends Pick<
    VideoFile,
    | 'path'
    | 'size'
    | 'languages'
    | 'quality'
    | 'customFormats'
    | 'customFormatScore'
    | 'qualityCutoffNotMet'
    | 'mediaInfo'
  > {
  columns: Column[];
  onDeleteVideoFile(): void;
}

function VideoFileRow(props: VideoFileRowProps) {
  const {
    path,
    size,
    languages,
    quality,
    customFormats,
    customFormatScore,
    qualityCutoffNotMet,
    mediaInfo,
    columns,
    onDeleteVideoFile,
  } = props;

  const [
    isRemoveVideoFileModalOpen,
    setRemoveVideoFileModalOpen,
    setRemoveVideoFileModalClosed,
  ] = useModalOpenState(false);

  const handleRemoveVideoFilePress = useCallback(() => {
    onDeleteVideoFile();

    setRemoveVideoFileModalClosed();
  }, [onDeleteVideoFile, setRemoveVideoFileModalClosed]);

  return (
    <TableRow>
      {columns.map(({ name, isVisible }) => {
        if (!isVisible) {
          return null;
        }

        if (name === 'path') {
          return <TableRowCell key={name}>{path}</TableRowCell>;
        }

        if (name === 'size') {
          return <TableRowCell key={name}>{formatBytes(size)}</TableRowCell>;
        }

        if (name === 'languages') {
          return (
            <TableRowCell key={name} className={styles.languages}>
              <VideoLanguages languages={languages} />
            </TableRowCell>
          );
        }

        if (name === 'quality') {
          return (
            <TableRowCell key={name} className={styles.quality}>
              <VideoQuality
                quality={quality}
                isCutoffNotMet={qualityCutoffNotMet}
              />
            </TableRowCell>
          );
        }

        if (name === 'customFormats') {
          return (
            <TableRowCell key={name} className={styles.customFormats}>
              <VideoFormats formats={customFormats} />
            </TableRowCell>
          );
        }

        if (name === 'customFormatScore') {
          return (
            <TableRowCell key={name} className={styles.customFormatScore}>
              {formatCustomFormatScore(customFormatScore, customFormats.length)}
            </TableRowCell>
          );
        }

        if (name === 'actions') {
          return (
            <TableRowCell key={name} className={styles.actions}>
              {mediaInfo ? (
                <Popover
                  anchor={<Icon name={icons.MEDIA_INFO} />}
                  title={translate('MediaInfo')}
                  body={<MediaInfo {...mediaInfo} />}
                  position={tooltipPositions.LEFT}
                />
              ) : null}

              <IconButton
                title={translate('DeleteVideoFromDisk')}
                name={icons.REMOVE}
                onPress={setRemoveVideoFileModalOpen}
              />
            </TableRowCell>
          );
        }

        return null;
      })}

      <ConfirmModal
        isOpen={isRemoveVideoFileModalOpen}
        kind={kinds.DANGER}
        title={translate('DeleteVideoFile')}
        message={translate('DeleteVideoFileMessage', { path })}
        confirmLabel={translate('Delete')}
        onConfirm={handleRemoveVideoFilePress}
        onCancel={setRemoveVideoFileModalClosed}
      />
    </TableRow>
  );
}

export default VideoFileRow;
