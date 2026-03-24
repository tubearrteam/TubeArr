import React, { useCallback } from 'react';
import { useDispatch } from 'react-redux';
import Modal from 'Components/Modal/Modal';
import { clearPendingChanges } from 'Store/Actions/baseActions';
import { EditChannelModalContentProps } from './EditChannelModalContent';
import EditChannelModalContentConnector from './EditChannelModalContentConnector';

interface EditChannelModalProps extends EditChannelModalContentProps {
  isOpen: boolean;
}

function EditChannelModal({
  isOpen,
  onModalClose,
  ...otherProps
}: EditChannelModalProps) {
  const dispatch = useDispatch();

  const handleModalClose = useCallback(() => {
    dispatch(clearPendingChanges({ section: 'channels' }));
    onModalClose();
  }, [dispatch, onModalClose]);

  return (
    <Modal isOpen={isOpen} onModalClose={handleModalClose}>
      <EditChannelModalContentConnector
        {...otherProps}
        onModalClose={handleModalClose}
      />
    </Modal>
  );
}

export default EditChannelModal;
