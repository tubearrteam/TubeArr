import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { fetchHistory, markAsFailed } from 'Store/Actions/historyActions';
import createVideoSelector from 'Store/Selectors/createVideoSelector';
import createChannelSelector from 'Store/Selectors/createChannelSelector';
import ChannelHistoryRow from './ChannelHistoryRow';

function createMapStateToProps() {
  return createSelector(
    createChannelSelector(),
    createVideoSelector(),
    (channel, video) => {
      return {
        channel,
        video
      };
    }
  );
}

const mapDispatchToProps = {
  fetchHistory,
  markAsFailed
};

export default connect(createMapStateToProps, mapDispatchToProps)(ChannelHistoryRow);
