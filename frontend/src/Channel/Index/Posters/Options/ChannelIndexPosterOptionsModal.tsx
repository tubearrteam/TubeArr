import React from 'react';
import Modal from 'Components/Modal/Modal';
import ChannelIndexPosterOptionsModalContent from './ChannelIndexPosterOptionsModalContent';

interface ChannelIndexPosterOptionsModalProps {
  isOpen: boolean;
  onModalClose(...args: unknown[]): unknown;
}

function ChannelIndexPosterOptionsModal({
  isOpen,
  onModalClose,
}: ChannelIndexPosterOptionsModalProps) {
  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <ChannelIndexPosterOptionsModalContent onModalClose={onModalClose} />
    </Modal>
  );
}

export default ChannelIndexPosterOptionsModal;
