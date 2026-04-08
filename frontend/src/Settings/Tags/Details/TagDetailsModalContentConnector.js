import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import createAllChannelsSelector from 'Store/Selectors/createAllChannelSelector';
import TagDetailsModalContent from './TagDetailsModalContent';

function findMatchingItems(ids, items) {
  return items.filter((s) => {
    return ids.includes(s.id);
  });
}

function createUnorderedMatchingChannelsSelector() {
  return createSelector(
    (state, { channelIds }) => channelIds,
    createAllChannelsSelector(),
    findMatchingItems
  );
}

function createMatchingChannelsSelector() {
  return createSelector(
    createUnorderedMatchingChannelsSelector(),
    (channels) => {
      return channels.sort((channelA, channelB) => {
        const sortTitleA = channelA.sortTitle;
        const sortTitleB = channelB.sortTitle;

        if (sortTitleA > sortTitleB) {
          return 1;
        } else if (sortTitleA < sortTitleB) {
          return -1;
        }

        return 0;
      });
    }
  );
}

function createMatchingNotificationsSelector() {
  return createSelector(
    (state, { notificationIds }) => notificationIds,
    (state) => state.settings.notifications.items,
    findMatchingItems
  );
}

function createMatchingAutoTagsSelector() {
  return createSelector(
    (state, { autoTagIds }) => autoTagIds,
    (state) => state.settings.autoTaggings.items,
    findMatchingItems
  );
}

function createMapStateToProps() {
  return createSelector(
    createMatchingChannelsSelector(),
    createMatchingNotificationsSelector(),
    createMatchingAutoTagsSelector(),
    (channels, notifications, autoTags) => {
      return {
        channels,
        notifications,
        autoTags
      };
    }
  );
}

export default connect(createMapStateToProps)(TagDetailsModalContent);
