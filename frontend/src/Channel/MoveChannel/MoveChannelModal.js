import PropTypes from 'prop-types';
import React from 'react';
import Button from 'Components/Link/Button';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { kinds, sizes } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import styles from './MoveChannelModal.css';

function MoveChannelModal(props) {
  const {
    originalPath,
    destinationPath,
    destinationRootFolder,
    isOpen,
    onModalClose,
    onSavePress,
    onMoveChannelPress
  } = props;

  if (
    isOpen &&
    !originalPath &&
    !destinationPath &&
    !destinationRootFolder
  ) {
    console.error('originalPath and destinationPath OR destinationRootFolder must be provided');
  }

  return (
    <Modal
      isOpen={isOpen}
      size={sizes.MEDIUM}
      closeOnBackgroundClick={false}
      onModalClose={onModalClose}
    >
      <ModalContent
        showCloseButton={true}
        onModalClose={onModalClose}
      >
        <ModalHeader>
          {translate('MoveFiles')}
        </ModalHeader>

        <ModalBody>
          {
            destinationRootFolder ?
              translate('MoveChannelFoldersToRootFolder', { destinationRootFolder }) :
              translate('MoveChannelFoldersToNewPath', { originalPath, destinationPath })
          }
        </ModalBody>

        <ModalFooter>
          <Button
            className={styles.doNotMoveButton}
            onPress={onSavePress}
          >
            {translate('MoveChannelFoldersDontMoveFiles')}
          </Button>

          <Button
            kind={kinds.DANGER}
            onPress={onMoveChannelPress}
          >
            {translate('MoveChannelFoldersMoveFiles')}
          </Button>
        </ModalFooter>
      </ModalContent>
    </Modal>
  );
}

MoveChannelModal.propTypes = {
  originalPath: PropTypes.string,
  destinationPath: PropTypes.string,
  destinationRootFolder: PropTypes.string,
  isOpen: PropTypes.bool.isRequired,
  onModalClose: PropTypes.func.isRequired,
  onSavePress: PropTypes.func.isRequired,
  onMoveChannelPress: PropTypes.func.isRequired
};

export default MoveChannelModal;
