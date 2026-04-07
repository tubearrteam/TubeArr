import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';
import Channel from 'Channel/Channel';
import createAllChannelsSelector from './createAllChannelSelector';

function createProfileInUseSelector(profileProp: string) {
  return createSelector(
    (_: AppState, { id }: { id: number }) => id,
    createAllChannelsSelector(),
    (id, channels) => {
      if (!id) {
        return false;
      }

      return channels.some((c) => c[profileProp as keyof Channel] === id);
    }
  );
}

export default createProfileInUseSelector;
