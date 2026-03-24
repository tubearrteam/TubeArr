import React from 'react';
import Modal from 'Components/Modal/Modal';
import ChannelIndexOverviewOptionsModalContent from './ChannelIndexOverviewOptionsModalContent';

interface ChannelIndexOverviewOptionsModalProps {
  isOpen: boolean;
  onModalClose(...args: unknown[]): void;
}

function ChannelIndexOverviewOptionsModal({
  isOpen,
  onModalClose,
  ...otherProps
}: ChannelIndexOverviewOptionsModalProps) {
  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <ChannelIndexOverviewOptionsModalContent
        {...otherProps}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default ChannelIndexOverviewOptionsModal;
