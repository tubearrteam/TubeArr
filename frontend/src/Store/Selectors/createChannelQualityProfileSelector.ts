import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';
import Channel from 'Channel/Channel';
import QualityProfile from 'typings/QualityProfile';
import { createChannelSelectorForHook } from './createChannelSelector';

function createChannelQualityProfileSelector(channelId: number) {
  return createSelector(
    (state: AppState) => state.settings.qualityProfiles.items,
    createChannelSelectorForHook(channelId),
    (qualityProfiles: QualityProfile[], channel = {} as Channel) => {
      return qualityProfiles.find(
        (profile) => profile.id === channel.qualityProfileId
      );
    }
  );
}

export default createChannelQualityProfileSelector;
