import React, { useMemo } from 'react';
import {
  CommandStatus,
  MetadataProgress,
  MetadataProgressStage,
} from 'Commands/Command';
import ProgressBar from 'Components/ProgressBar';
import translate from 'Utilities/String/translate';
import styles from './QueuedTaskRowMetadataProgress.css';

function formatPercent(percent: number) {
  if (!Number.isFinite(percent)) {
    return '0%';
  }

  if (percent >= 10 || Number.isInteger(percent)) {
    return `${percent.toFixed(0)}%`;
  }

  return `${percent.toFixed(1)}%`;
}

function isStaleInProgressDetail(detail: string | undefined) {
  if (!detail) {
    return false;
  }

  const d = detail.toLowerCase();
  return (
    (d.includes('saving ') && d.includes('video')) ||
    d.includes('checking which need')
  );
}

function normalizeStageForCompletedCommand(
  stage: MetadataProgressStage
): MetadataProgressStage {
  const errors = stage.errors ?? [];
  if (errors.length > 0) {
    return stage;
  }

  const { completed, total, detail } = stage;

  if (total > 0 && completed < total) {
    return {
      ...stage,
      completed: total,
      percent: 100,
      detail: undefined,
    };
  }

  if (total === 0) {
    const nextDetail = isStaleInProgressDetail(detail) ? undefined : detail;
    return {
      ...stage,
      percent: 100,
      detail: nextDetail,
    };
  }

  if (isStaleInProgressDetail(detail)) {
    return { ...stage, detail: undefined };
  }

  return stage;
}

function formatProgressText(
  stage: MetadataProgressStage,
  commandCompleted: boolean
) {
  const errors = stage.errors ?? [];
  if (
    commandCompleted &&
    errors.length === 0 &&
    stage.total === 0 &&
    stage.completed === 0
  ) {
    return translate('Completed');
  }

  if (stage.total > 0) {
    return `${stage.completed}/${stage.total} (${formatPercent(stage.percent)})`;
  }

  return stage.completed > 0 ? `${stage.completed}` : '0';
}

interface QueuedTaskRowMetadataProgressProps {
  metadataProgress: MetadataProgress;
  commandStatus?: CommandStatus;
}

export default function QueuedTaskRowMetadataProgress({
  metadataProgress,
  commandStatus,
}: QueuedTaskRowMetadataProgressProps) {
  const stages = metadataProgress.stages ?? [];
  const commandCompleted = commandStatus === 'completed';

  const displayStages = useMemo(() => {
    const list = metadataProgress.stages ?? [];
    if (!commandCompleted) {
      return list;
    }

    return list.map(normalizeStageForCompletedCommand);
  }, [commandCompleted, metadataProgress.stages]);

  if (!stages.length) {
    return null;
  }

  return (
    <div className={styles.metadataProgress}>
      {displayStages.map((stage) => {
        const errors = stage.errors ?? [];

        return (
          <div key={stage.key} className={styles.stage}>
            <div className={styles.stageHeader}>
              <span className={styles.stageLabel}>{stage.label}</span>
              <span className={styles.stageCount}>
                {formatProgressText(stage, commandCompleted)}
              </span>
            </div>

            <ProgressBar
              className={styles.progressBar}
              containerClassName={styles.progressBarContainer}
              progress={stage.percent}
              size="small"
              title={formatProgressText(stage, commandCompleted)}
            />

            {stage.detail ? (
              <div className={styles.stageDetail}>{stage.detail}</div>
            ) : null}

            {errors.length ? (
              <ul className={styles.errorList}>
                {errors.map((error, index) => {
                  return (
                    <li key={`${stage.key}-${index}`} className={styles.errorItem}>
                      {error}
                    </li>
                  );
                })}
              </ul>
            ) : null}
          </div>
        );
      })}
    </div>
  );
}
