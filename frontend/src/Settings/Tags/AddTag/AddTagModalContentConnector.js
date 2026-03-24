import PropTypes from 'prop-types';
import React from 'react';
import { connect } from 'react-redux';
import { addTag } from 'Store/Actions/tagActions';
import AddTagModalContent from './AddTagModalContent';

const mapDispatchToProps = {
  dispatchAddTag: addTag
};

function AddTagModalContentConnector({ dispatchAddTag, onModalClose, ...otherProps }) {
  const onSave = (payload) => {
    dispatchAddTag({
      tag: payload,
      onTagCreated: () => {
        onModalClose();
      }
    });
  };

  return (
    <AddTagModalContent
      {...otherProps}
      onSave={onSave}
      onModalClose={onModalClose}
    />
  );
}

AddTagModalContentConnector.propTypes = {
  dispatchAddTag: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired
};

export default connect(null, mapDispatchToProps)(AddTagModalContentConnector);
