import PropTypes from 'prop-types';
import React from 'react';
import Modal from 'Components/Modal/Modal';
import { sizes } from 'Helpers/Props';
import DeleteChannelModalContentConnector from './DeleteChannelModalContentConnector';

function DeleteChannelModal(props) {
  const {
    isOpen,
    onModalClose,
    onDeleteComplete,
    ...otherProps
  } = props;

  return (
    <Modal
      isOpen={isOpen}
      size={sizes.MEDIUM}
      onModalClose={onModalClose}
    >
      <DeleteChannelModalContentConnector
        {...otherProps}
        onModalClose={onModalClose}
        onDeleteComplete={onDeleteComplete}
      />
    </Modal>
  );
}

DeleteChannelModal.propTypes = {
  ...(DeleteChannelModalContentConnector.propTypes || {}),
  isOpen: PropTypes.bool.isRequired,
  onModalClose: PropTypes.func.isRequired,
  onDeleteComplete: PropTypes.func
};

export default DeleteChannelModal;
