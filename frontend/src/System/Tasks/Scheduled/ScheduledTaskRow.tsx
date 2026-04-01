import moment from 'moment';
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import AppState from 'App/State/AppState';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableRow from 'Components/Table/TableRow';
import usePrevious from 'Helpers/Hooks/usePrevious';
import { icons } from 'Helpers/Props';
import { executeCommand } from 'Store/Actions/commandActions';
import { fetchTask } from 'Store/Actions/systemActions';
import createCommandSelector from 'Store/Selectors/createCommandSelector';
import createUISettingsSelector from 'Store/Selectors/createUISettingsSelector';
import { isCommandExecuting } from 'Utilities/Command';
import formatDate from 'Utilities/Date/formatDate';
import formatDateTime from 'Utilities/Date/formatDateTime';
import formatTimeSpan from 'Utilities/Date/formatTimeSpan';
import translate from 'Utilities/String/translate';
import styles from './ScheduledTaskRow.css';

interface ScheduledTaskRowProps {
  id: number;
  taskName: string;
  name: string;
  interval: number;
  lastExecution?: string | null;
  lastStartTime?: string | null;
  lastDuration?: string | null;
  nextExecution?: string | null;
}

function ScheduledTaskRow(props: ScheduledTaskRowProps) {
  const {
    id,
    taskName,
    name,
    interval,
    lastExecution,
    lastStartTime,
    lastDuration,
    nextExecution,
  } = props;

  const dispatch = useDispatch();

  const { showRelativeDates, longDateFormat, shortDateFormat, timeFormat } =
    useSelector(createUISettingsSelector());
  const command = useSelector(createCommandSelector(taskName));
  useSelector(
    (state: AppState) => state.app.translations?.isPopulated ?? false
  );

  const displayName = useMemo(() => {
    const t = translate(taskName);
    return t !== taskName ? t : name;
  }, [taskName, name]);

  const [time, setTime] = useState(Date.now());

  const isQueued = !!(command && command.status === 'queued');
  const isExecuting = isCommandExecuting(command);
  const wasExecuting = usePrevious(isExecuting);
  const isDisabled = interval === 0;
  const hasMeaningfulTimestamp = (iso?: string | null) => {
    if (!iso) {
      return false;
    }
    const m = moment(iso);
    return m.isValid() && m.isAfter('2010-01-01');
  };
  const hasLastExecution = hasMeaningfulTimestamp(lastExecution);
  const nextExecutionValid = hasMeaningfulTimestamp(nextExecution);
  const executeNow =
    !isDisabled &&
    nextExecutionValid &&
    moment().isAfter(nextExecution!);
  const hasNextExecutionTime = !isDisabled && !executeNow;
  const hasLastStartTime = hasMeaningfulTimestamp(lastStartTime);

  const duration = useMemo(() => {
    return moment
      .duration(interval, 'minutes')
      .humanize()
      .replace(/an?(?=\s)/, '1');
  }, [interval]);

  const { lastExecutionTime, nextExecutionTime } = useMemo(() => {
    const isDisabled = interval === 0;

    if (!hasLastExecution) {
      if (showRelativeDates && time) {
        return {
          lastExecutionTime: translate('NotRunYet'),
          nextExecutionTime:
            isDisabled || !nextExecutionValid
              ? '-'
              : moment(nextExecution).fromNow(),
        };
      }

      return {
        lastExecutionTime: translate('NotRunYet'),
        nextExecutionTime:
          isDisabled || !nextExecutionValid
            ? '-'
            : formatDate(nextExecution!, shortDateFormat),
      };
    }

    if (showRelativeDates && time) {
      return {
        lastExecutionTime: moment(lastExecution!).fromNow(),
        nextExecutionTime:
          isDisabled || !nextExecutionValid
            ? '-'
            : moment(nextExecution).fromNow(),
      };
    }

    return {
      lastExecutionTime: formatDate(lastExecution!, shortDateFormat),
      nextExecutionTime:
        isDisabled || !nextExecutionValid
          ? '-'
          : formatDate(nextExecution!, shortDateFormat),
    };
  }, [
    time,
    interval,
    lastExecution,
    nextExecution,
    showRelativeDates,
    shortDateFormat,
    hasLastExecution,
    nextExecutionValid,
  ]);

  const handleExecutePress = useCallback(() => {
    dispatch(
      executeCommand({
        name: taskName,
      })
    );
  }, [taskName, dispatch]);

  useEffect(() => {
    if (!isExecuting && wasExecuting) {
      setTimeout(() => {
        dispatch(fetchTask({ id }));
      }, 1000);
    }
  }, [id, isExecuting, wasExecuting, dispatch]);

  useEffect(() => {
    const interval = setInterval(() => setTime(Date.now()), 1000);
    return () => {
      clearInterval(interval);
    };
  }, [setTime]);

  return (
    <TableRow>
      <TableRowCell>{displayName}</TableRowCell>
      <TableRowCell className={styles.interval}>
        {isDisabled ? translate('Disabled') : duration}
      </TableRowCell>

      <TableRowCell
        className={styles.lastExecution}
        title={
          hasLastExecution && lastExecution
            ? formatDateTime(lastExecution, longDateFormat, timeFormat) || undefined
            : undefined
        }
      >
        {lastExecutionTime}
      </TableRowCell>

      {hasLastStartTime && lastDuration ? (
        <TableRowCell className={styles.lastDuration} title={lastDuration}>
          {formatTimeSpan(lastDuration)}
        </TableRowCell>
      ) : (
        <TableRowCell className={styles.lastDuration}>-</TableRowCell>
      )}

      {isDisabled ? (
        <TableRowCell className={styles.nextExecution}>-</TableRowCell>
      ) : null}

      {executeNow && isQueued ? (
        <TableRowCell className={styles.nextExecution}>{translate('Queued')}</TableRowCell>
      ) : null}

      {executeNow && !isQueued ? (
        <TableRowCell className={styles.nextExecution}>{translate('Now')}</TableRowCell>
      ) : null}

      {hasNextExecutionTime ? (
        <TableRowCell
          className={styles.nextExecution}
          title={
            nextExecutionValid
              ? formatDateTime(nextExecution!, longDateFormat, timeFormat, {
                  includeSeconds: true,
                }) || undefined
              : undefined
          }
        >
          {nextExecutionValid ? nextExecutionTime : '-'}
        </TableRowCell>
      ) : null}

      <TableRowCell className={styles.actions}>
        <SpinnerIconButton
          name={icons.REFRESH}
          spinningName={icons.REFRESH}
          isDisabled={isDisabled}
          isSpinning={isExecuting}
          onPress={handleExecutePress}
        />
      </TableRowCell>
    </TableRow>
  );
}

export default ScheduledTaskRow;
