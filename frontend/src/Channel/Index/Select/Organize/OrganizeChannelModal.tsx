import React from 'react';
import Modal from 'Components/Modal/Modal';
import OrganizeChannelModalContent from './OrganizeChannelModalContent';

interface OrganizeChannelModalProps {
  isOpen: boolean;
  channelIds: number[];
  onModalClose: () => void;
}

function OrganizeChannelModal(props: OrganizeChannelModalProps) {
  const { isOpen, onModalClose, ...otherProps } = props;

  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <OrganizeChannelModalContent
        {...otherProps}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default OrganizeChannelModal;
