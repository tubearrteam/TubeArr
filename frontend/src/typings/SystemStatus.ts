interface SystemStatus {
  appData: string;
  appName: string;
  authentication: string;
  branch: string;
  buildTime: string;
  databaseVersion: string;
  databaseType: string;
  instanceName: string;
  isAdmin: boolean;
  isDebug: boolean;
  isDocker: boolean;
  isLinux: boolean;
  isNetCore: boolean;
  isOsx: boolean;
  isProduction: boolean;
  isUserInteractive: boolean;
  isWindows: boolean;
  /** Server host OS for downloadable tools (windows | darwin | linux). */
  hostBinaryPlatformOs?: string;
  /** Server host CPU family for tool binaries (x64 | arm64 | arm). */
  hostBinaryPlatformArch?: string;
  /** Linux only: musl | glibc | unknown (third-party FFmpeg builds are often glibc). */
  hostBinaryPlatformLibc?: string | null;
  migrationVersion: number;
  mode: string;
  osName: string;
  osVersion: string;
  packageAuthor: string;
  packageUpdateMechanism: string;
  packageUpdateMechanismMessage: string;
  packageVersion: string;
  runtimeName: string;
  runtimeVersion: string;
  sqliteVersion: string;
  startTime: string;
  startupPath: string;
  urlBase: string;
  version: string;
}

export default SystemStatus;
