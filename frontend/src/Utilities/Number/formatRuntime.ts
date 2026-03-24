import translate from 'Utilities/String/translate';

function formatRuntime(runtimeSeconds: number) {
  if (!runtimeSeconds) {
    return '';
  }

  const total = Math.max(0, Math.floor(runtimeSeconds));
  const hours = Math.floor(total / 3600);
  const minutes = Math.floor((total % 3600) / 60);
  const seconds = total % 60;
  const result: string[] = [];

  if (hours) {
    result.push(translate('FormatRuntimeHours', { hours }));
    if (minutes) {
      result.push(translate('FormatRuntimeMinutes', { minutes }));
    }
    if (seconds) {
      result.push(translate('FormatRuntimeSeconds', { seconds }));
    }
  } else if (minutes) {
    result.push(translate('FormatRuntimeMinutes', { minutes }));
    if (seconds) {
      result.push(translate('FormatRuntimeSeconds', { seconds }));
    }
  } else if (seconds) {
    result.push(translate('FormatRuntimeSeconds', { seconds }));
  }

  return result.join(' ');
}

export default formatRuntime;
