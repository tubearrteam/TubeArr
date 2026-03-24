import React from 'react';
import Modal from 'Components/Modal/Modal';
import EditChannelModalContent from './EditChannelModalContent';

interface EditChannelModalProps {
  isOpen: boolean;
  channelIds: number[];
  onSavePress(payload: object): void;
  onModalClose(): void;
}

function EditChannelModal(props: EditChannelModalProps) {
  const { isOpen, channelIds, onSavePress, onModalClose } = props;

  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <EditChannelModalContent
        channelIds={channelIds}
        onSavePress={onSavePress}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default EditChannelModal;
