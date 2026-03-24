import React from 'react';
import Modal from 'Components/Modal/Modal';
import ChangeMonitoringModalContent from './ChangeMonitoringModalContent';

interface ChangeMonitoringModalProps {
  isOpen: boolean;
  channelIds: number[];
  onSavePress(monitor: string, roundRobinLatestVideoCount?: number): void;
  onModalClose(): void;
}

function ChangeMonitoringModal(props: ChangeMonitoringModalProps) {
  const { isOpen, channelIds, onSavePress, onModalClose } = props;

  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <ChangeMonitoringModalContent
        channelIds={channelIds}
        onSavePress={onSavePress}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default ChangeMonitoringModal;
