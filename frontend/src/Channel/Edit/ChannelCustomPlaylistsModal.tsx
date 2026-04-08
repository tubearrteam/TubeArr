import React, { useCallback, useRef, useState } from 'react';
import { useDispatch } from 'react-redux';
import Alert from 'Components/Alert';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import Button from 'Components/Link/Button';
import SpinnerErrorButton from 'Components/Link/SpinnerErrorButton';
import { kinds } from 'Helpers/Props';
import ChannelEditCustomPlaylists, {
  toSaveCustomPlaylistsPayload,
  validateCustomPlaylistsBeforeSave,
  type CustomPlaylistDraft,
} from 'Channel/Edit/ChannelEditCustomPlaylists';
import useChannel from 'Channel/useChannel';
import { updateItem } from 'Store/Actions/baseActions';
import { section as channelsSection } from 'Store/Actions/channelActions';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import translate from 'Utilities/String/translate';
import styles from './EditChannelModalContent.css';

export interface ChannelCustomPlaylistsModalProps {
  isOpen: boolean;
  onModalClose: () => void;
  channelId: number;
}

export default function ChannelCustomPlaylistsModal({
  isOpen,
  onModalClose,
  channelId,
}: ChannelCustomPlaylistsModalProps) {
  const dispatch = useDispatch();
  const channel = useChannel(channelId);
  const draftsRef = useRef<CustomPlaylistDraft[]>([]);
  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState<unknown>(null);
  const [validationMessage, setValidationMessage] = useState<string | null>(null);

  const onDraftChange = useCallback((drafts: CustomPlaylistDraft[]) => {
    draftsRef.current = drafts;
  }, []);

  const handleSave = useCallback(() => {
    setSaveError(null);
    setValidationMessage(null);
    const validation = validateCustomPlaylistsBeforeSave(draftsRef.current);
    if (validation) {
      setValidationMessage(validation);
      return;
    }

    setIsSaving(true);
    const payload = toSaveCustomPlaylistsPayload(draftsRef.current);
    const { request } = createAjaxRequest({
      url: `/channels/${channelId}`,
      method: 'PUT',
      contentType: 'application/json',
      dataType: 'json',
      data: JSON.stringify({ customPlaylists: payload }),
    });

    request
      .done((data: Record<string, unknown>) => {
        setIsSaving(false);
        const id = data?.id ?? data?.Id;
        if (id != null) {
          dispatch(
            updateItem({
              section: channelsSection,
              ...data,
              id,
            })
          );
        }
        onModalClose();
      })
      .fail((xhr: { aborted?: boolean }) => {
        setIsSaving(false);
        setSaveError(xhr.aborted ? null : xhr);
      });
  }, [channelId, dispatch, onModalClose]);

  const title = (channel?.title ?? '').trim() || translate('Channel');

  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>{translate('CustomPlaylistsModalTitle', { title })}</ModalHeader>

        <ModalBody>
          {validationMessage ? (
            <Alert kind={kinds.DANGER}>{validationMessage}</Alert>
          ) : null}
          <div className={styles.customPlaylistsModalScroll}>
            <ChannelEditCustomPlaylists
              embeddedInModal
              source={channel?.customPlaylists}
              translate={translate}
              onDraftChange={onDraftChange}
            />
          </div>
        </ModalBody>

        <ModalFooter className={styles.modalFooter}>
          <Button onPress={onModalClose}>{translate('Cancel')}</Button>
          <SpinnerErrorButton
            className={styles.addButton}
            error={saveError}
            isSpinning={isSaving}
            onPress={handleSave}
          >
            {translate('Save')}
          </SpinnerErrorButton>
        </ModalFooter>
      </ModalContent>
    </Modal>
  );
}
