declare module '*.module.css';

declare module '*.css' {
  const classes: { [key: string]: string };
  export default classes;
}

interface Window {
  TubeArr: {
    apiKey: string;
    apiRoot: string;
    instanceName: string;
    theme: string;
    urlBase: string;
    version: string;
    isProduction: boolean;
  };
}
