import React from 'react';
import Label from 'Components/Label';
import { kinds } from 'Helpers/Props';
import { QualityModel } from 'Quality/Quality';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';

function getTooltip(
  title: string,
  quality: QualityModel,
  size: number | undefined
) {
  if (!title) {
    return;
  }

  const revision = quality.revision;

  if (revision.real && revision.real > 0) {
    title += ' [REAL]';
  }

  if (revision.version && revision.version > 1) {
    title += ' [PROPER]';
  }

  if (size) {
    title += ` - ${formatBytes(size)}`;
  }

  return title;
}

function revisionLabel(
  className: string | undefined,
  quality: QualityModel,
  showRevision: boolean
) {
  if (!showRevision) {
    return;
  }

  if (quality.revision.isRepack) {
    return (
      <Label
        className={className}
        kind={kinds.PRIMARY}
        title={translate('Repack')}
      >
        R
      </Label>
    );
  }

  if (quality.revision.version && quality.revision.version > 1) {
    return (
      <Label
        className={className}
        kind={kinds.PRIMARY}
        title={translate('Proper')}
      >
        P
      </Label>
    );
  }

  return null;
}

interface VideoQualityProps {
  className?: string;
  title?: string;
  quality: QualityModel;
  size?: number;
  isCutoffNotMet?: boolean;
  showRevision?: boolean;
  /** When set (e.g. from ffprobe stream width), shown instead of quality profile name. */
  statusLabelOverride?: string;
}

function VideoQuality(props: VideoQualityProps) {
  const {
    className,
    title = '',
    quality,
    size,
    isCutoffNotMet,
    showRevision = false,
    statusLabelOverride,
  } = props;

  if (!quality) {
    return null;
  }

  const displayName = statusLabelOverride || quality.quality.name;

  return (
    <span>
      <Label
        className={className}
        kind={isCutoffNotMet ? kinds.INVERSE : kinds.DEFAULT}
        title={getTooltip(title, quality, size)}
      >
        {displayName}
      </Label>
      {revisionLabel(className, quality, showRevision)}
    </span>
  );
}

export default VideoQuality;
