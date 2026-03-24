import React from 'react';
import Modal from 'Components/Modal/Modal';
import DeleteChannelModalContent from './DeleteChannelModalContent';

interface DeleteChannelModalProps {
  isOpen: boolean;
  channelIds: number[];
  onModalClose(): void;
}

function DeleteChannelModal(props: DeleteChannelModalProps) {
  const { isOpen, channelIds, onModalClose } = props;

  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <DeleteChannelModalContent
        channelIds={channelIds}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default DeleteChannelModal;
