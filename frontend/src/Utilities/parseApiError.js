/**
 * @param {import('jquery').jqXHR} xhr
 * @returns {{ message: string, code: string | null, details: unknown }}
 */
export function parseApiError(xhr) {
  const json = xhr && xhr.responseJSON;
  if (json == null || json === '') {
    return {
      message: (xhr && xhr.statusText) || 'Request failed',
      code: null,
      details: null,
    };
  }
  if (typeof json === 'string') {
    return { message: json, code: null, details: null };
  }
  const message =
    json.message ||
    (Array.isArray(json.errors)
      ? json.errors.map((e) => (typeof e === 'string' ? e : e?.errorMessage || '')).filter(Boolean).join(' ')
      : null) ||
    (xhr && xhr.statusText) ||
    'Request failed';
  return {
    message,
    code: json.code != null ? String(json.code) : null,
    details: json.details != null ? json.details : json.errors != null ? json.errors : null,
  };
}

/**
 * @param {import('jquery').jqXHR} xhr
 * @returns {string}
 */
export function getApiErrorMessage(xhr) {
  return parseApiError(xhr).message;
}
