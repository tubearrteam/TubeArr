import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';
import Channel from 'Channel/Channel';
import ImportList from 'typings/ImportList';
import createAllChannelsSelector from './createAllChannelSelector';

function createProfileInUseSelector(profileProp: string) {
  return createSelector(
    (_: AppState, { id }: { id: number }) => id,
    createAllChannelsSelector(),
    (state: AppState) => state.settings.importLists.items,
    (id, channels, lists) => {
      if (!id) {
        return false;
      }

      return (
        channels.some((c) => c[profileProp as keyof Channel] === id) ||
        lists.some((list) => list[profileProp as keyof ImportList] === id)
      );
    }
  );
}

export default createProfileInUseSelector;
