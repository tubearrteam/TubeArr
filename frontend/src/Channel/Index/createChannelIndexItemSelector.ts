import { maxBy } from 'lodash';
import { createSelector } from 'reselect';
import Command from 'Commands/Command';
import { REFRESH_CHANNEL, DOWNLOAD_MONITORED } from 'Commands/commandNames';
import Channel from 'Channel/Channel';
import createExecutingCommandsSelector from 'Store/Selectors/createExecutingCommandsSelector';
import createChannelQualityProfileSelector from 'Store/Selectors/createChannelQualityProfileSelector';
import { createChannelSelectorForHook } from 'Store/Selectors/createChannelSelector';

function createChannelIndexItemSelector(channelId: number) {
  return createSelector(
    createChannelSelectorForHook(channelId),
    createChannelQualityProfileSelector(channelId),
    createExecutingCommandsSelector(),
    (channel: Channel, qualityProfile, executingCommands: Command[]) => {
      const isRefreshingChannel = executingCommands.some((command) => {
        return (
          command.name === REFRESH_CHANNEL && command.body.channelId === channelId
        );
      });

      const isSearchingChannel = executingCommands.some((command) => {
        return (
          command.name === DOWNLOAD_MONITORED && command.body.channelId === channelId
        );
      });

      const latestPlaylist = maxBy(
        channel.playlists,
        (playlist) => playlist.playlistNumber
      );

      return {
        channel,
        qualityProfile,
        latestPlaylist,
        isRefreshingChannel,
        isSearchingChannel,
      };
    }
  );
}

export default createChannelIndexItemSelector;
