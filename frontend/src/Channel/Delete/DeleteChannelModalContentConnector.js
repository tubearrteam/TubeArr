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

function createMapDispatchToProps(dispatch, props) {
  return {
    onDeleteOptionChange(option) {
      dispatch(
        setDeleteOption({
          [option.name]: option.value
        })
      );
    },

    onDeletePress(deleteFiles, addImportListExclusion) {
      dispatch(
        deleteChannel({
          id: props.channelId,
          deleteFiles,
          addImportListExclusion
        })
      );

      props.onModalClose(true);
    }
  };
}

export default connect(createMapStateToProps, createMapDispatchToProps)(DeleteChannelModalContent);
