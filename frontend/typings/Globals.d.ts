declare module '*.module.css';

declare module '*.css' {
  const classes: { [key: string]: string };
  export default classes;
}

interface Window {
  TubeArr: {
    apiKey: string;
    /** True when the server requires API key auth for /api/v1; key may be omitted from HTML until /initialize.json is called with a valid key. */
    apiKeyRequired?: boolean;
    apiRoot: string;
    instanceName: string;
    theme: string;
    urlBase: string;
    version: string;
    isProduction: boolean;
    /** Boolean flags from server config (TubeArr:Features:*), when present. */
    featureFlags?: Record<string, boolean>;
  };
}
