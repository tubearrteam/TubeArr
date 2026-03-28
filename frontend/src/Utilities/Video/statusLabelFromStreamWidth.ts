/** Parse width from ffprobe-style `1920x1080` in `mediaInfo.resolution`. */
export function parseVideoStreamWidth(
  resolution: string | undefined | null
): number | null {
  if (!resolution || typeof resolution !== 'string') {
    return null;
  }
  const m = /^(\d+)\s*x\s*(\d+)/i.exec(resolution.trim());
  if (!m) {
    return null;
  }
  const w = parseInt(m[1], 10);
  return Number.isFinite(w) && w > 0 ? w : null;
}

/** Map video stream width (px) to a compact status label. */
export function statusLabelFromVideoWidth(width: number): string {
  if (width >= 7680) {
    return '8K';
  }
  if (width >= 3840) {
    return '4K';
  }
  if (width >= 2560) {
    return '1440p';
  }
  if (width >= 1920) {
    return '1080p';
  }
  if (width >= 1280) {
    return '720p';
  }
  if (width >= 854) {
    return '480p';
  }
  if (width > 0) {
    return 'SD';
  }
  return '';
}
