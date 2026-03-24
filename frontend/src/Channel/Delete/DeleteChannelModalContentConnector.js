import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { deleteChannel, setDeleteOption } from 'Store/Actions/channelActions';
import createChannelSelector from 'Store/Selectors/createChannelSelector';
import DeleteChannelModalContent from './DeleteChannelModalContent';

function createMapStateToProps() {
  const selectChannelByChannelId = createChannelSelector();

  return createSelector(
    (state) => state.channels.deleteOptions,
    (state, { channelId }) => selectChannelByChannelId(state, { channelId: channelId }),
    (deleteOptions, channel) => {
      return {
        ...channel,
        deleteOptions
      };
    }
  );
}

function createMapDispatchToProps(dispatch, ownProps) {
  return {
    onDeleteOptionChange(option) {
      dispatch(
        setDeleteOption({
          [option.name]: option.value
        })
      );
    },

    onDeletePress(deleteFiles, addImportListExclusion) {
      const promise = dispatch(
        deleteChannel({
          id: ownProps.channelId,
          deleteFiles,
          addImportListExclusion
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
