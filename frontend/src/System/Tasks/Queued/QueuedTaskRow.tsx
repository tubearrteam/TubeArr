import moment from 'moment';
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { CommandBody, CommandStatus } from 'Commands/Command';
import Icon, { IconProps } from 'Components/Icon';
import IconButton from 'Components/Link/IconButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableRow from 'Components/Table/TableRow';
import useModalOpenState from 'Helpers/Hooks/useModalOpenState';
import { icons, kinds, messageTypes } from 'Helpers/Props';
import { showMessage } from 'Store/Actions/appActions';
import { cancelCommand } from 'Store/Actions/commandActions';
import createUISettingsSelector from 'Store/Selectors/createUISettingsSelector';
import formatDate from 'Utilities/Date/formatDate';
import formatDateTime from 'Utilities/Date/formatDateTime';
import formatTimeSpan from 'Utilities/Date/formatTimeSpan';
import titleCase from 'Utilities/String/titleCase';
import translate from 'Utilities/String/translate';
import QueuedTaskRowNameCell from './QueuedTaskRowNameCell';
import styles from './QueuedTaskRow.css';

function getStatusIconProps(
  status: string,
  message: string | undefined
): IconProps {
  const title = titleCase(status);

  switch (status) {
    case 'queued':
      return {
        name: icons.PENDING,
        title,
      };

    case 'started':
      return {
        name: icons.REFRESH,
        isSpinning: true,
        title,
      };

    case 'completed':
      return {
        name: icons.CHECK,
        kind: kinds.SUCCESS,
        title: message === 'Completed' ? title : `${title}: ${message}`,
      };

    case 'failed':
      return {
        name: icons.FATAL,
        kind: kinds.DANGER,
        title: `${title}: ${message}`,
      };

    default:
      return {
        name: icons.UNKNOWN,
        title,
      };
  }
}

function getFormattedDates(
  queued: string,
  started: string | undefined,
  ended: string | undefined,
  showRelativeDates: boolean,
  shortDateFormat: string
) {
  if (showRelativeDates) {
    return {
      queuedAt: moment(queued).fromNow(),
      startedAt: started ? moment(started).fromNow() : '-',
      endedAt: ended ? moment(ended).fromNow() : '-',
    };
  }

  return {
    queuedAt: formatDate(queued, shortDateFormat),
    startedAt: started ? formatDate(started, shortDateFormat) : '-',
    endedAt: ended ? formatDate(ended, shortDateFormat) : '-',
  };
}

function isTerminalStatus(status: string) {
  return (
    status === 'completed' ||
    status === 'failed' ||
    status === 'aborted' ||
    status === 'cancelled' ||
    status === 'orphaned'
  );
}

interface QueuedTimes {
  queuedAt: string;
  startedAt: string;
  endedAt: string;
}

export interface QueuedTaskRowProps {
  id: number;
  trigger: string;
  commandName: string;
  queued: string;
  started?: string;
  ended?: string;
  status: CommandStatus;
  duration?: string;
  message?: string;
  body: CommandBody;
  clientUserAgent?: string;
  variant?: 'default' | 'metadata';
}

export default function QueuedTaskRow(props: QueuedTaskRowProps) {
  const {
    id,
    trigger,
    commandName,
    queued,
    started,
    ended,
    status,
    duration,
    message,
    body,
    clientUserAgent,
    variant = 'default',
  } = props;

  const dispatch = useDispatch();
  const { longDateFormat, shortDateFormat, showRelativeDates, timeFormat } =
    useSelector(createUISettingsSelector());

  const updateTimeTimeoutId = useRef<ReturnType<typeof setTimeout> | null>(
    null
  );
  const [times, setTimes] = useState<QueuedTimes>(
    getFormattedDates(
      queued,
      started,
      ended,
      showRelativeDates,
      shortDateFormat
    )
  );

  const [
    isCancelConfirmModalOpen,
    openCancelConfirmModal,
    closeCancelConfirmModal,
  ] = useModalOpenState(false);

  const [isCanceling, setIsCanceling] = useState(false);

  const handleCancelPress = useCallback(() => {
    setIsCanceling(true);
    const xhr = dispatch(cancelCommand({ id })) as {
      fail?: (cb: (jqXHR: { responseJSON?: { message?: string } }) => void) => unknown;
      always?: (cb: () => void) => unknown;
    };

    const finish = () => {
      setIsCanceling(false);
      closeCancelConfirmModal();
    };

    if (xhr && typeof xhr.always === 'function') {
      if (typeof xhr.fail === 'function') {
        xhr.fail((jqXHR) => {
          const apiMessage = jqXHR.responseJSON?.message;
          dispatch(
            showMessage({
              id: `cancel-command-${id}`,
              name: 'Command',
              message: apiMessage ?? translate('CancelCommandFailed'),
              type: messageTypes.ERROR,
              hideAfter: 10,
            })
          );
        });
      }
      xhr.always(finish);
    } else {
      finish();
    }
  }, [id, dispatch, closeCancelConfirmModal]);

  useEffect(() => {
    updateTimeTimeoutId.current = setTimeout(() => {
      setTimes(
        getFormattedDates(
          queued,
          started,
          ended,
          showRelativeDates,
          shortDateFormat
        )
      );
    }, 30000);

    return () => {
      if (updateTimeTimeoutId.current) {
        clearTimeout(updateTimeTimeoutId.current);
      }
    };
  }, [queued, started, ended, showRelativeDates, shortDateFormat, setTimes]);

  const { queuedAt, startedAt, endedAt } = times;
  const showEnded = isTerminalStatus(status);
  const endedDisplay = showEnded ? endedAt : '-';
  const endedTitle = showEnded ? formatDateTime(ended, longDateFormat, timeFormat) : undefined;
  const durationDisplay = showEnded ? formatTimeSpan(duration) : '';

  let triggerIcon = icons.QUICK;

  if (trigger === 'manual') {
    triggerIcon = icons.INTERACTIVE;
  } else if (trigger === 'scheduled') {
    triggerIcon = icons.SCHEDULED;
  }

  if (variant === 'metadata') {
    return (
      <TableRow>
        <TableRowCell className={styles.trigger}>
          <span className={styles.triggerContent}>
            <Icon name={triggerIcon} title={titleCase(trigger)} />

            <Icon {...getStatusIconProps(status, message)} />
          </span>
        </TableRowCell>

        <QueuedTaskRowNameCell
          className={styles.metadataName}
          commandName={commandName}
          body={body}
          clientUserAgent={clientUserAgent}
          commandStatus={status}
        />

        <TableRowCell className={styles.metadataMethod}>
          {Array.isArray(body?.acquisitionMethods) && body.acquisitionMethods.length > 0
            ? body.acquisitionMethods.join(', ')
            : '—'}
        </TableRowCell>

        <TableRowCell
          className={styles.metadataStarted}
          title={formatDateTime(started, longDateFormat, timeFormat)}
        >
          {startedAt}
        </TableRowCell>

        <TableRowCell className={styles.metadataDuration}>
          {durationDisplay}
        </TableRowCell>

        <TableRowCell className={styles.metadataActions}>
          {(status === 'queued' || status === 'started') && (
            <IconButton
              title={translate('RemovedFromTaskQueue')}
              name={icons.REMOVE}
              onPress={openCancelConfirmModal}
            />
          )}
        </TableRowCell>

        <ConfirmModal
          isOpen={isCancelConfirmModalOpen}
          kind={kinds.DANGER}
          title={translate('Cancel')}
          message={translate('CancelPendingTask')}
          confirmLabel={translate('YesCancel')}
          cancelLabel={translate('NoLeaveIt')}
          isSpinning={isCanceling}
          onConfirm={handleCancelPress}
          onCancel={closeCancelConfirmModal}
        />
      </TableRow>
    );
  }

  return (
    <TableRow>
      <TableRowCell className={styles.trigger}>
        <span className={styles.triggerContent}>
          <Icon name={triggerIcon} title={titleCase(trigger)} />

          <Icon {...getStatusIconProps(status, message)} />
        </span>
      </TableRowCell>

      <QueuedTaskRowNameCell
        commandName={commandName}
        body={body}
        clientUserAgent={clientUserAgent}
        commandStatus={status}
      />

      <TableRowCell
        className={styles.queued}
        title={formatDateTime(queued, longDateFormat, timeFormat)}
      >
        {queuedAt}
      </TableRowCell>

      <TableRowCell
        className={styles.started}
        title={formatDateTime(started, longDateFormat, timeFormat)}
      >
        {startedAt}
      </TableRowCell>

      <TableRowCell
        className={styles.ended}
        title={endedTitle}
      >
        {endedDisplay}
      </TableRowCell>

      <TableRowCell className={styles.duration}>
        {durationDisplay}
      </TableRowCell>

      <TableRowCell className={styles.actions}>
        {(status === 'queued' || status === 'started') && (
          <IconButton
            title={translate('RemovedFromTaskQueue')}
            name={icons.REMOVE}
            onPress={openCancelConfirmModal}
          />
        )}
      </TableRowCell>

      <ConfirmModal
        isOpen={isCancelConfirmModalOpen}
        kind={kinds.DANGER}
        title={translate('Cancel')}
        message={translate('CancelPendingTask')}
        confirmLabel={translate('YesCancel')}
        cancelLabel={translate('NoLeaveIt')}
        isSpinning={isCanceling}
        onConfirm={handleCancelPress}
        onCancel={closeCancelConfirmModal}
      />
    </TableRow>
  );
}
