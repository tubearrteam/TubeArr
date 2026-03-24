import { createSelector } from 'reselect';
import createAllChannelsSelector from './createAllChannelSelector';

function createChannelCountSelector() {
  return createSelector(createAllChannelsSelector(), (channels) => {
    return channels.length;
  });
}

export default createChannelCountSelector;
