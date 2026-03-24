import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Card from 'Components/Card';
import Label from 'Components/Label';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import TagList from 'Components/TagList';
import { kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import EditNotificationModalConnector from './EditNotificationModalConnector';
import styles from './Notification.css';

class Notification extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      isEditNotificationModalOpen: false,
      isDeleteNotificationModalOpen: false
    };
  }

  //
  // Listeners

  onEditNotificationPress = () => {
    this.setState({ isEditNotificationModalOpen: true });
  };

  onEditNotificationModalClose = () => {
    this.setState({ isEditNotificationModalOpen: false });
  };

  onDeleteNotificationPress = () => {
    this.setState({
      isEditNotificationModalOpen: false,
      isDeleteNotificationModalOpen: true
    });
  };

  onDeleteNotificationModalClose = () => {
    this.setState({ isDeleteNotificationModalOpen: false });
  };

  onConfirmDeleteNotification = () => {
    this.props.onConfirmDeleteNotification(this.props.id);
  };

  //
  // Render

  render() {
    const {
      id,
      name,
      onGrab,
      onDownload,
      onUpgrade,
      onImportComplete,
      onRename,
      onChannelAdd,
      onChannelDelete,
      onVideoFileDelete,
      onVideoFileDeleteForUpgrade,
      onHealthIssue,
      onHealthRestored,
      onApplicationUpdate,
      onManualInteractionRequired,
      supportsOnGrab,
      supportsOnDownload,
      supportsOnUpgrade,
      supportsOnImportComplete,
      supportsOnRename,
      supportsOnChannelAdd,
      supportsOnChannelDelete,
      supportsOnVideoFileDelete,
      supportsOnVideoFileDeleteForUpgrade,
      supportsOnHealthIssue,
      supportsOnHealthRestored,
      supportsOnApplicationUpdate,
      supportsOnManualInteractionRequired,
      tags,
      tagList
    } = this.props;

    return (
      <Card
        className={styles.notification}
        overlayContent={true}
        onPress={this.onEditNotificationPress}
      >
        <div className={styles.name}>
          {name}
        </div>

        {
          supportsOnGrab && onGrab ?
            <Label kind={kinds.SUCCESS}>
              {translate('OnGrab')}
            </Label> :
            null
        }

        {
          supportsOnDownload && onDownload ?
            <Label kind={kinds.SUCCESS}>
              {translate('OnFileImport')}
            </Label> :
            null
        }

        {
          supportsOnUpgrade && onDownload && onUpgrade ?
            <Label kind={kinds.SUCCESS}>
              {translate('OnFileUpgrade')}
            </Label> :
            null
        }

        {
          supportsOnImportComplete && onImportComplete ?
            <Label kind={kinds.SUCCESS}>
              {translate('OnImportComplete')}
            </Label> :
            null
        }

        {
          supportsOnRename && onRename ?
            <Label kind={kinds.SUCCESS}>
              {translate('OnRename')}
            </Label> :
            null
        }

        {
          supportsOnHealthIssue && onHealthIssue ?
            <Label kind={kinds.SUCCESS}>
              {translate('OnHealthIssue')}
            </Label> :
            null
        }

        {
          supportsOnHealthRestored && onHealthRestored ?
            <Label kind={kinds.SUCCESS}>
              {translate('OnHealthRestored')}
            </Label> :
            null
        }

        {
          supportsOnApplicationUpdate && onApplicationUpdate ?
            <Label kind={kinds.SUCCESS}>
              {translate('OnApplicationUpdate')}
            </Label> :
            null
        }

        {
          supportsOnChannelAdd && onChannelAdd ?
            <Label kind={kinds.SUCCESS}>
              {translate('OnChannelAdd')}
            </Label> :
            null
        }

        {
          supportsOnChannelDelete && onChannelDelete ?
            <Label kind={kinds.SUCCESS}>
              {translate('OnChannelDelete')}
            </Label> :
            null
        }

        {
          supportsOnVideoFileDelete && onVideoFileDelete ?
            <Label kind={kinds.SUCCESS}>
              {translate('OnVideoFileDelete')}
            </Label> :
            null
        }

        {
          supportsOnVideoFileDeleteForUpgrade && onVideoFileDelete && onVideoFileDeleteForUpgrade ?
            <Label kind={kinds.SUCCESS}>
              {translate('OnVideoFileDeleteForUpgrade')}
            </Label> :
            null
        }

        {
          supportsOnManualInteractionRequired && onManualInteractionRequired ?
            <Label kind={kinds.SUCCESS}>
              {translate('OnManualInteractionRequired')}
            </Label> :
            null
        }

        {
          !onGrab && !onDownload && !onRename && !onImportComplete && !onHealthIssue && !onHealthRestored && !onApplicationUpdate && !onChannelAdd && !onChannelDelete && !onVideoFileDelete && !onManualInteractionRequired ?
            <Label
              kind={kinds.DISABLED}
              outline={true}
            >
              {translate('Disabled')}
            </Label> :
            null
        }

        <TagList
          tags={tags}
          tagList={tagList}
        />

        <EditNotificationModalConnector
          id={id}
          isOpen={this.state.isEditNotificationModalOpen}
          onModalClose={this.onEditNotificationModalClose}
          onDeleteNotificationPress={this.onDeleteNotificationPress}
        />

        <ConfirmModal
          isOpen={this.state.isDeleteNotificationModalOpen}
          kind={kinds.DANGER}
          title={translate('DeleteNotification')}
          message={translate('DeleteNotificationMessageText', { name })}
          confirmLabel={translate('Delete')}
          onConfirm={this.onConfirmDeleteNotification}
          onCancel={this.onDeleteNotificationModalClose}
        />
      </Card>
    );
  }
}

Notification.propTypes = {
  id: PropTypes.number.isRequired,
  name: PropTypes.string.isRequired,
  onGrab: PropTypes.bool.isRequired,
  onDownload: PropTypes.bool.isRequired,
  onUpgrade: PropTypes.bool.isRequired,
  onImportComplete: PropTypes.bool.isRequired,
  onRename: PropTypes.bool.isRequired,
  onChannelAdd: PropTypes.bool.isRequired,
  onChannelDelete: PropTypes.bool.isRequired,
  onVideoFileDelete: PropTypes.bool.isRequired,
  onVideoFileDeleteForUpgrade: PropTypes.bool.isRequired,
  onHealthIssue: PropTypes.bool.isRequired,
  onHealthRestored: PropTypes.bool.isRequired,
  onApplicationUpdate: PropTypes.bool.isRequired,
  onManualInteractionRequired: PropTypes.bool.isRequired,
  supportsOnGrab: PropTypes.bool.isRequired,
  supportsOnDownload: PropTypes.bool.isRequired,
  supportsOnImportComplete: PropTypes.bool.isRequired,
  supportsOnChannelAdd: PropTypes.bool.isRequired,
  supportsOnChannelDelete: PropTypes.bool.isRequired,
  supportsOnVideoFileDelete: PropTypes.bool.isRequired,
  supportsOnVideoFileDeleteForUpgrade: PropTypes.bool.isRequired,
  supportsOnUpgrade: PropTypes.bool.isRequired,
  supportsOnRename: PropTypes.bool.isRequired,
  supportsOnHealthIssue: PropTypes.bool.isRequired,
  supportsOnHealthRestored: PropTypes.bool.isRequired,
  supportsOnApplicationUpdate: PropTypes.bool.isRequired,
  supportsOnManualInteractionRequired: PropTypes.bool.isRequired,
  tags: PropTypes.arrayOf(PropTypes.number).isRequired,
  tagList: PropTypes.arrayOf(PropTypes.object).isRequired,
  onConfirmDeleteNotification: PropTypes.func.isRequired
};

export default Notification;
