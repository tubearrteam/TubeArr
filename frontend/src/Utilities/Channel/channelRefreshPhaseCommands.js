import { findCommand, isCommandExecuting } from 'Utilities/Command';

export const CHANNEL_REFRESH_PHASE_COMMAND_NAMES = [
  'RefreshChannelUploadsPopulation',
  'RefreshChannelHydration',
  'RefreshChannelLivestreamIdentification',
  'RefreshChannelShortsParsing',
  'RefreshChannelPlaylistDiscovery',
  'RefreshChannelPlaylistPopulation'
];

export function isChannelRefreshPhaseExecuting(commands, channelId) {
  return CHANNEL_REFRESH_PHASE_COMMAND_NAMES.some((name) =>
    isCommandExecuting(findCommand(commands, { name, channelId }))
  );
}

export function isChannelRefreshPhaseNameExecuting(commands, channelId, phaseCommandName) {
  return isCommandExecuting(findCommand(commands, { name: phaseCommandName, channelId }));
}
