import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';

function createAllChannelsSelector() {
  return createSelector(
    (state: AppState) => state.channels,
    (channels) => {
      return Array.isArray(channels.items) ? channels.items : [];
    }
  );
}

export default createAllChannelsSelector;
