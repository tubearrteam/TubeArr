import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import createExistingChannelSelector from 'Store/Selectors/createExistingChannelSelector';
import ImportChannelSearchResult from './ImportChannelSearchResult';

function createMapStateToProps() {
  return createSelector(
    createExistingChannelSelector(),
    (isExistingChannel) => {
      return {
        isExistingChannel
      };
    }
  );
}

export default connect(createMapStateToProps)(ImportChannelSearchResult);
