import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import createChannelSelector from 'Store/Selectors/createChannelSelector';
import createTagsSelector from 'Store/Selectors/createTagsSelector';
import sortByProp from 'Utilities/Array/sortByProp';
import ChannelTags from './ChannelTags';

function createMapStateToProps() {
  const channelSelector = createChannelSelector();

  return createSelector(
    (state, { channelId }) => channelSelector(state, { channelId: channelId }),
    createTagsSelector(),
    (channel, tagList) => {
      const tags = channel.tags
        .map((tagId) => tagList.find((tag) => tag.id === tagId))
        .filter((tag) => !!tag)
        .sort(sortByProp('label'))
        .map((tag) => tag.label);

      return {
        tags
      };
    }
  );
}

export default connect(createMapStateToProps)(ChannelTags);
