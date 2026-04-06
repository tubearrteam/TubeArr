import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { deleteChannel } from 'Store/Actions/channelActions';
import createChannelSelector from 'Store/Selectors/createChannelSelector';
import DeleteChannelModalContent from './DeleteChannelModalContent';

function createMapStateToProps() {
  const selectChannelByChannelId = createChannelSelector();

  return createSelector(
    (state, { channelId }) => selectChannelByChannelId(state, { channelId: channelId }),
    (channel) => channel
  );
}

function createMapDispatchToProps(dispatch, ownProps) {
  return {
    onDeletePress(deleteFiles) {
      const promise = dispatch(
        deleteChannel({
          id: ownProps.channelId,
          deleteFiles,
        })
      );

      const onSuccess = () => {
        ownProps.onModalClose(true);
        if (ownProps.onDeleteComplete) {
          ownProps.onDeleteComplete();
        }
      };

      if (promise && typeof promise.then === 'function') {
        promise.then(onSuccess, () => {});
      } else if (promise && typeof promise.done === 'function') {
        promise.done(onSuccess).fail(() => {});
      } else {
        onSuccess();
      }
    }
  };
}

export default connect(createMapStateToProps, createMapDispatchToProps)(DeleteChannelModalContent);
