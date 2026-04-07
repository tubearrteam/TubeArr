import PropTypes from 'prop-types';
import React, { Component } from 'react';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import InlineMarkdown from 'Components/Markdown/InlineMarkdown';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { icons, inputTypes, kinds } from 'Helpers/Props';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import styles from './DeleteChannelModalContent.css';

class DeleteChannelModalContent extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      deleteFiles: false
    };
  }

  //
  // Listeners

  onDeleteFilesChange = ({ value }) => {
    this.setState({ deleteFiles: value });
  };

  onDeleteChannelConfirmed = () => {
    const deleteFiles = this.state.deleteFiles;

    this.setState({ deleteFiles: false });
    this.props.onDeletePress(deleteFiles);
  };

  //
  // Render

  render() {
    const {
      title,
      path,
      statistics = {},
      onModalClose
    } = this.props;

    const {
      videoFileCount = 0,
      sizeOnDisk = 0
    } = statistics;

    const deleteFiles = this.state.deleteFiles;

    return (
      <ModalContent
        onModalClose={onModalClose}
      >
        <ModalHeader>
          {translate('DeleteChannelModalHeader', { title })}
        </ModalHeader>

        <ModalBody>
          <div className={styles.pathContainer}>
            <Icon
              className={styles.pathIcon}
              name={icons.FOLDER}
            />

            {path}
          </div>

          <FormGroup>
            <FormLabel>{videoFileCount === 0 ? translate('DeleteChannelFolder') : translate('DeleteVideosFiles', { videoFileCount })}</FormLabel>

            <FormInputGroup
              type={inputTypes.CHECK}
              name="deleteFiles"
              value={deleteFiles}
              helpText={videoFileCount === 0 ? translate('DeleteChannelFolderHelpText') : translate('DeleteVideosFilesHelpText')}
              kind={kinds.DANGER}
              onChange={this.onDeleteFilesChange}
            />
          </FormGroup>

          {
            deleteFiles ?
              <div className={styles.deleteFilesMessage}>
                <div><InlineMarkdown data={translate('DeleteChannelFolderConfirmation', { path })} blockClassName={styles.folderPath} /></div>

                {
                  videoFileCount ?
                    <div className={styles.deleteCount}>
                      {translate('DeleteChannelFolderVideoCount', { videoFileCount, size: formatBytes(sizeOnDisk) })}
                    </div> :
                    null
                }
              </div> :
              null
          }
        </ModalBody>

        <ModalFooter>
          <Button onPress={onModalClose}>
            {translate('Close')}
          </Button>

          <Button
            kind={kinds.DANGER}
            onPress={this.onDeleteChannelConfirmed}
          >
            {translate('Delete')}
          </Button>
        </ModalFooter>
      </ModalContent>
    );
  }
}

DeleteChannelModalContent.propTypes = {
  title: PropTypes.string.isRequired,
  path: PropTypes.string.isRequired,
  statistics: PropTypes.object.isRequired,
  onDeletePress: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired
};

DeleteChannelModalContent.defaultProps = {
  statistics: {
    videoFileCount: 0
  }
};

export default DeleteChannelModalContent;
