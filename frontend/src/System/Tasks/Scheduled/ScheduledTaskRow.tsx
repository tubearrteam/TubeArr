import moment from 'moment';
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import AppState from 'App/State/AppState';
import NumberInput from 'Components/Form/NumberInput';
import IconButton from 'Components/Link/IconButton';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableRow from 'Components/Table/TableRow';
import usePrevious from 'Helpers/Hooks/usePrevious';
import { icons } from 'Helpers/Props';
import { executeCommand } from 'Store/Actions/commandActions';
import { fetchTask, fetchTasks } from 'Store/Actions/systemActions';
import createCommandSelector from 'Store/Selectors/createCommandSelector';
import createUISettingsSelector from 'Store/Selectors/createUISettingsSelector';
import { isCommandExecuting } from 'Utilities/Command';
import createAjaxRequest from 'Utilities/createAjaxRequest';
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
  defaultInterval?: number;
  intervalOverride?: number | null;
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
    defaultInterval: defaultIntervalProp,
    intervalOverride,
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

  const defaultInterval =
    defaultIntervalProp != null && defaultIntervalProp > 0
      ? defaultIntervalProp
      : interval;

  const displayName = useMemo(() => {
    const t = translate(taskName);
    return t !== taskName ? t : name;
  }, [taskName, name]);

  const [time, setTime] = useState(Date.now());
  const [editMinutes, setEditMinutes] = useState(interval > 0 ? interval : 1);
  const [isSavingInterval, setIsSavingInterval] = useState(false);

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

  const editDurationHint = useMemo(() => {
    if (editMinutes == null || editMinutes < 1) {
      return '';
    }
    return moment
      .duration(editMinutes, 'minutes')
      .humanize()
      .replace(/an?(?=\s)/, '1');
  }, [editMinutes]);

  const isIntervalDirty =
    !isDisabled &&
    editMinutes != null &&
    editMinutes >= 1 &&
    editMinutes !== interval;

  const { lastExecutionTime, nextExecutionTime } = useMemo(() => {
    const rowDisabled = interval === 0;

    if (!hasLastExecution) {
      if (showRelativeDates && time) {
        return {
          lastExecutionTime: translate('NotRunYet'),
          nextExecutionTime:
            rowDisabled || !nextExecutionValid
              ? '-'
              : moment(nextExecution).fromNow(),
        };
      }

      return {
        lastExecutionTime: translate('NotRunYet'),
        nextExecutionTime:
          rowDisabled || !nextExecutionValid
            ? '-'
            : formatDate(nextExecution!, shortDateFormat),
      };
    }

    if (showRelativeDates && time) {
      return {
        lastExecutionTime: moment(lastExecution!).fromNow(),
        nextExecutionTime:
          rowDisabled || !nextExecutionValid
            ? '-'
            : moment(nextExecution).fromNow(),
      };
    }

    return {
      lastExecutionTime: formatDate(lastExecution!, shortDateFormat),
      nextExecutionTime:
        rowDisabled || !nextExecutionValid
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

  const persistInterval = useCallback(
    (minutes: number) => {
      setIsSavingInterval(true);
      const { request } = createAjaxRequest({
        url: `/system/task/${id}/interval`,
        method: 'PUT',
        contentType: 'application/json',
        dataType: 'json',
        data: JSON.stringify({ interval: minutes }),
      });

      request
        .done(() => {
          dispatch(fetchTasks());
        })
        .fail(() => {
          window.alert(translate('ScheduledTaskIntervalSaveError'));
          dispatch(fetchTask({ id }));
        })
        .always(() => {
          setIsSavingInterval(false);
        });
    },
    [id, dispatch]
  );

  const handleSaveInterval = useCallback(() => {
    if (isDisabled || editMinutes == null || editMinutes < 1) {
      return;
    }
    const clamped = Math.min(40320, Math.max(1, editMinutes));
    const clearOverride = clamped === defaultInterval;
    persistInterval(clearOverride ? 0 : clamped);
  }, [isDisabled, editMinutes, defaultInterval, persistInterval]);

  const handleResetInterval = useCallback(() => {
    persistInterval(0);
  }, [persistInterval]);

  useEffect(() => {
    if (!isDisabled) {
      setEditMinutes(interval);
    }
  }, [id, interval, isDisabled]);

  useEffect(() => {
    if (!isExecuting && wasExecuting) {
      setTimeout(() => {
        dispatch(fetchTask({ id }));
      }, 1000);
    }
  }, [id, isExecuting, wasExecuting, dispatch]);

  useEffect(() => {
    const tick = setInterval(() => setTime(Date.now()), 1000);
    return () => {
      clearInterval(tick);
    };
  }, [setTime]);

  const lastExecTitle =
    hasLastExecution && lastExecution
      ? formatDateTime(lastExecution, longDateFormat, timeFormat) || undefined
      : undefined;

  return (
    <TableRow>
      <TableRowCell>{displayName}</TableRowCell>
      <TableRowCell className={styles.interval}>
        {isDisabled ? (
          translate('Disabled')
        ) : (
          <div className={styles.intervalEdit}>
            <NumberInput
              className={styles.intervalInput}
              name={`scheduledTaskInterval-${id}`}
              value={editMinutes}
              min={1}
              max={40320}
              onChange={({ value }) => {
                const n = typeof value === 'number' && !Number.isNaN(value) && value >= 1 ? value : interval;
                setEditMinutes(n);
              }}
            />
            {editDurationHint ? (
              <span className={styles.intervalHint} title={editDurationHint}>
                {editDurationHint}
              </span>
            ) : null}
            {intervalOverride != null ? (
              <IconButton
                name={icons.RESTART}
                title={translate('Default')}
                isDisabled={isSavingInterval}
                onPress={handleResetInterval}
              />
            ) : null}
            <SpinnerIconButton
              name={icons.SAVE}
              spinningName={icons.SPINNER}
              title={translate('Save')}
              isDisabled={!isIntervalDirty || isSavingInterval}
              isSpinning={isSavingInterval}
              onPress={handleSaveInterval}
            />
          </div>
        )}
      </TableRowCell>

      <TableRowCell
        className={styles.lastExecution}
        title={lastExecTitle}
      >
        {lastExecutionTime}
      </TableRowCell>

      {hasLastExecution && lastDuration ? (
        <TableRowCell
          className={styles.lastDuration}
          title={
            hasLastStartTime && lastStartTime
              ? formatDateTime(lastStartTime, longDateFormat, timeFormat, {
                  includeSeconds: true,
                }) || lastDuration
              : lastDuration
          }
        >
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
