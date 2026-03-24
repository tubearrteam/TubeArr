import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import createDimensionsSelector from 'Store/Selectors/createDimensionsSelector';
import createExistingChannelSelector from 'Store/Selectors/createExistingChannelSelector';
import AddNewChannelSearchResult from './AddNewChannelSearchResult';

function createMapStateToProps() {
  return createSelector(
    createExistingChannelSelector(),
    createDimensionsSelector(),
    (isExistingChannel, dimensions) => {
      return {
        isExistingChannel,
        isSmallScreen: dimensions.isSmallScreen
      };
    }
  );
}

export default connect(createMapStateToProps)(AddNewChannelSearchResult);
