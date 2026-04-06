import './polyfills';
import 'Styles/globals.css';
import './index.css';

function normalizeUrlBase(value: string | undefined) {
  return value === '__URL_BASE__' ? '' : (value ?? '');
}

const preloaded = window.TubeArr ?? ({} as Window['TubeArr']);
const preloadedUrlBase = normalizeUrlBase(preloaded.urlBase);

const storedApiKey =
  typeof sessionStorage !== 'undefined' ? sessionStorage.getItem('tubeArrApiKey') ?? '' : '';

const needsInitializeFetch =
  !preloaded.apiRoot || preloaded.apiKeyRequired === true;

if (needsInitializeFetch) {
  const initHeaders: Record<string, string> = {};
  if (storedApiKey) initHeaders['X-Api-Key'] = storedApiKey;
  const initializeUrl = `${preloadedUrlBase}/initialize.json?t=${Date.now()}`;
  const response = await fetch(initializeUrl, { headers: initHeaders });
  const initialized = await response.json();

  let apiKey = '';
  if (initialized.apiKeyRequired === true) {
    const fromServer = initialized.apiKey as string | undefined;
    if (typeof fromServer === 'string' && fromServer !== '') {
      apiKey = fromServer;
      sessionStorage.setItem('tubeArrApiKey', fromServer);
    } else {
      if (storedApiKey) sessionStorage.removeItem('tubeArrApiKey');
      if (typeof window.prompt === 'function') {
        const entered = window.prompt(
          'Enter TubeArr API key (stored in this browser session only):'
        );
        if (entered?.trim()) {
          sessionStorage.setItem('tubeArrApiKey', entered.trim());
          location.reload();
        }
      }
    }
  } else {
    apiKey =
      (initialized.apiKey as string | undefined) ??
      preloaded.apiKey ??
      storedApiKey ??
      '';
  }

  window.TubeArr = {
    ...initialized,
    ...preloaded,
    urlBase: normalizeUrlBase(initialized.urlBase ?? preloaded.urlBase),
    apiKey
  };
} else {
  window.TubeArr = {
    ...preloaded,
    urlBase: preloadedUrlBase,
    apiKey: preloaded.apiKey ?? storedApiKey ?? ''
  };
}

/* eslint-disable no-undef, @typescript-eslint/ban-ts-comment */
// @ts-ignore 2304
__webpack_public_path__ = `${window.TubeArr.urlBase}/`;
/* eslint-enable no-undef, @typescript-eslint/ban-ts-comment */

const error = console.error;

// Monkey patch console.error to filter out some warnings from React
// TODO: Remove this after the great TypeScript migration

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function logError(...parameters: any[]) {
  const filter = parameters.find((parameter) => {
    return (
      typeof parameter === 'string' &&
      (parameter.includes(
        'Support for defaultProps will be removed from function components in a future major release'
      ) ||
        parameter.includes(
          'findDOMNode is deprecated and will be removed in the next major release'
        ))
    );
  });

  if (!filter) {
    error(...parameters);
  }
}

console.error = logError;

const { bootstrap } = await import('./bootstrap');

await bootstrap();
