import './polyfills';
import 'Styles/globals.css';
import './index.css';

function normalizeUrlBase(value: string | undefined) {
  return value === '__URL_BASE__' ? '' : (value ?? '');
}

const preloaded = window.TubeArr ?? ({} as Window['TubeArr']);
const preloadedUrlBase = normalizeUrlBase(preloaded.urlBase);

if (!preloaded.apiRoot) {
  const initializeUrl = `${preloadedUrlBase}/initialize.json?t=${Date.now()}`;
  const response = await fetch(initializeUrl);
  const initialized = await response.json();
  window.TubeArr = {
    ...initialized,
    ...preloaded,
    urlBase: normalizeUrlBase(initialized.urlBase ?? preloaded.urlBase),
    apiKey: preloaded.apiKey ?? ''
  };
} else {
  window.TubeArr = {
    ...preloaded,
    urlBase: preloadedUrlBase,
    apiKey: preloaded.apiKey ?? ''
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
