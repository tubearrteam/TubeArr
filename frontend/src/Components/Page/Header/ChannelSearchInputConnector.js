import { push } from 'redux-first-history';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import createAllChannelsSelector from 'Store/Selectors/createAllChannelSelector';
import createDeepEqualSelector from 'Store/Selectors/createDeepEqualSelector';
import createTagsSelector from 'Store/Selectors/createTagsSelector';
import ChannelSearchInput from './ChannelSearchInput';

function createCleanChannelsSelector() {
  return createSelector(
    createAllChannelsSelector(),
    createTagsSelector(),
    (allChannels, allTags) => {
      return allChannels.map((channel) => {
        const {
          title,
          titleSlug,
          sortTitle,
          images,
          alternateTitles = [],
          youtubeChannelId,
          tags: channelTags
        } = channel;

        const tagsList = Array.isArray(channelTags) ? channelTags : [];
        return {
          title,
          titleSlug,
          sortTitle,
          images,
          alternateTitles,
          youtubeChannelId,
          firstCharacter: String(title ?? sortTitle ?? '').charAt(0).toLowerCase(),
          tags: tagsList.reduce((acc, id) => {
            const matchingTag = allTags.find((tag) => tag.id === id);

            if (matchingTag) {
              acc.push(matchingTag);
            }

            return acc;
          }, [])
        };
      });
    }
  );
}

function createMapStateToProps() {
  return createDeepEqualSelector(
    createCleanChannelsSelector(),
    (channels) => {
      return {
        channels
      };
    }
  );
}

function createMapDispatchToProps(dispatch, props) {
  return {
    onGoToChannel(titleSlug) {
      dispatch(push(`${window.TubeArr.urlBase}/channels/${titleSlug}`));
    },

    onGoToAddNewChannel(query) {
      dispatch(push(`${window.TubeArr.urlBase}/add/new?term=${encodeURIComponent(query)}`));
    }
  };
}

export default connect(createMapStateToProps, createMapDispatchToProps)(ChannelSearchInput);
