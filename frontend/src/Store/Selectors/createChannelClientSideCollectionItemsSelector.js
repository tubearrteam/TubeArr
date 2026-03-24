import { createSelector, createSelectorCreator, defaultMemoize } from 'reselect';
import hasDifferentItemsOrOrder from 'Utilities/Object/hasDifferentItemsOrOrder';
import createClientSideCollectionSelector from './createClientSideCollectionSelector';

function createUnoptimizedSelector(uiSection) {
  return createSelector(
    createClientSideCollectionSelector('channels', uiSection),
    (channels) => {
      const items = channels.items.map((s) => {
        const {
          id,
          sortTitle
        } = s;

        return {
          id,
          sortTitle
        };
      });

      return {
        ...channels,
        items
      };
    }
  );
}

function channelListEqual(a, b) {
  return hasDifferentItemsOrOrder(a, b);
}

const createChannelEqualSelector = createSelectorCreator(
  defaultMemoize,
  channelListEqual
);

function createChannelClientSideCollectionItemsSelector(uiSection) {
  return createChannelEqualSelector(
    createUnoptimizedSelector(uiSection),
    (channels) => channels
  );
}

export default createChannelClientSideCollectionItemsSelector;
