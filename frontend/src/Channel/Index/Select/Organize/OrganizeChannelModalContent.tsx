import { orderBy } from 'lodash';
import React, { useCallback, useMemo } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { RENAME_CHANNEL } from 'Commands/commandNames';
import Alert from 'Components/Alert';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { icons, kinds } from 'Helpers/Props';
import Channel from 'Channel/Channel';
import { executeCommand } from 'Store/Actions/commandActions';
import createAllChannelSelector from 'Store/Selectors/createAllChannelSelector';
import translate from 'Utilities/String/translate';
import styles from './OrganizeChannelModalContent.css';

interface OrganizeChannelModalContentProps {
  channelIds: number[];
  onModalClose: () => void;
}

function OrganizeChannelModalContent(props: OrganizeChannelModalContentProps) {
  const { channelIds, onModalClose } = props;

  const allChannels: Channel[] = useSelector(createAllChannelSelector());
  const dispatch = useDispatch();

  const channelTitles = useMemo(() => {
    const channels = channelIds.reduce((acc: Channel[], id) => {
      const channel = allChannels.find((s) => s.id === id);

      if (channel) {
        acc.push(channel);
      }

      return acc;
    }, []);

    const sorted = orderBy(channels, ['sortTitle']);

    return sorted.map((channel) => channel.title);
  }, [channelIds, allChannels]);

  const onOrganizePress = useCallback(() => {
    dispatch(
      executeCommand({
        name: RENAME_CHANNEL,
        channelIds: channelIds,
      })
    );

    onModalClose();
  }, [channelIds, onModalClose, dispatch]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {translate('OrganizeSelectedChannelModalHeader')}
      </ModalHeader>

      <ModalBody>
        <Alert>
          {translate('OrganizeSelectedChannelModalAlert')}
          <Icon className={styles.renameIcon} name={icons.ORGANIZE} />
        </Alert>

        <div className={styles.message}>
          {translate('OrganizeSelectedChannelModalConfirmation', {
            count: channelTitles.length,
          })}
        </div>

        <ul>
          {channelTitles.map((title) => {
            return <li key={title}>{title}</li>;
          })}
        </ul>
      </ModalBody>

      <ModalFooter>
        <Button onPress={onModalClose}>{translate('Cancel')}</Button>

        <Button kind={kinds.DANGER} onPress={onOrganizePress}>
          {translate('Organize')}
        </Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default OrganizeChannelModalContent;
