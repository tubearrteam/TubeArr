import React from 'react';
import { MetadataProgress, MetadataProgressStage } from 'Commands/Command';
import ProgressBar from 'Components/ProgressBar';
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

function formatProgressText(stage: MetadataProgressStage) {
  if (stage.total > 0) {
    return `${stage.completed}/${stage.total} (${formatPercent(stage.percent)})`;
  }

  return stage.completed > 0 ? `${stage.completed}` : '0';
}

interface QueuedTaskRowMetadataProgressProps {
  metadataProgress: MetadataProgress;
}

export default function QueuedTaskRowMetadataProgress({
  metadataProgress,
}: QueuedTaskRowMetadataProgressProps) {
  const stages = metadataProgress.stages ?? [];

  if (!stages.length) {
    return null;
  }

  return (
    <div className={styles.metadataProgress}>
      {stages.map((stage) => {
        const errors = stage.errors ?? [];

        return (
          <div key={stage.key} className={styles.stage}>
            <div className={styles.stageHeader}>
              <span className={styles.stageLabel}>{stage.label}</span>
              <span className={styles.stageCount}>{formatProgressText(stage)}</span>
            </div>

            <ProgressBar
              className={styles.progressBar}
              containerClassName={styles.progressBarContainer}
              progress={stage.percent}
              size="small"
              title={formatProgressText(stage)}
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
