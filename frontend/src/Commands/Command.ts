import ModelBase from 'App/ModelBase';

export type CommandStatus =
  | 'queued'
  | 'started'
  | 'completed'
  | 'failed'
  | 'aborted'
  | 'cancelled'
  | 'orphaned';

export type CommandResult = 'unknown' | 'successful' | 'unsuccessful';

export interface MetadataProgressStage {
  key: string;
  label: string;
  completed: number;
  total: number;
  percent: number;
  detail?: string;
  errors?: string[];
}

export interface MetadataProgress {
  stages: MetadataProgressStage[];
  errors?: string[];
}

export interface CommandBody {
  sendUpdatesToClient: boolean;
  updateScheduledTask: boolean;
  completionMessage: string;
  requiresDiskAccess: boolean;
  isExclusive: boolean;
  isLongRunning: boolean;
  name: string;
  lastExecutionTime: string;
  lastStartTime: string;
  trigger: string;
  suppressMessages: boolean;
  channelId?: number;
  channelIds?: number[];
  playlistNumber?: number;
  videoIds?: number[];
  metadataProgress?: MetadataProgress;
  /** Refresh channel phase: uploadsPopulation | hydration | shortsParsing */
  metadataStep?: string;
  originalCommandName?: string;
  /** Live detail while a phase is running (e.g. DB save progress) */
  phaseDetail?: string;
  [key: string]:
    | string
    | number
    | boolean
    | number[]
    | string[]
    | MetadataProgress
    | MetadataProgressStage[]
    | undefined;
}

interface Command extends ModelBase {
  name: string;
  commandName: string;
  message: string;
  body: CommandBody;
  priority: string;
  status: CommandStatus;
  result: CommandResult;
  queued: string;
  started: string;
  ended?: string;
  duration?: string;
  trigger: string;
  stateChangeTime: string;
  sendUpdatesToClient: boolean;
  updateScheduledTask: boolean;
  lastExecutionTime: string;
}

export default Command;
