import { orderBy } from 'lodash';
import React, { useCallback, useMemo, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Button from 'Components/Link/Button';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { inputTypes, kinds } from 'Helpers/Props';
import Channel from 'Channel/Channel';
import { bulkDeleteChannels, setDeleteOption } from 'Store/Actions/channelActions';
import createAllChannelSelector from 'Store/Selectors/createAllChannelSelector';
import { CheckInputChanged } from 'typings/inputs';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import styles from './DeleteChannelModalContent.css';

interface DeleteChannelModalContentProps {
  channelIds: number[];
  onModalClose(): void;
}

const selectDeleteOptions = createSelector(
  (state: AppState) => state.channels.deleteOptions,
  (deleteOptions) => deleteOptions
);

function DeleteChannelModalContent(props: DeleteChannelModalContentProps) {
  const { channelIds, onModalClose } = props;

  const { addImportListExclusion } = useSelector(selectDeleteOptions);
  const allChannels: Channel[] = useSelector(createAllChannelSelector());
  const dispatch = useDispatch();

  const [deleteFiles, setDeleteFiles] = useState(false);

  const channels = useMemo((): Channel[] => {
    const channelsList = channelIds.map((id) => {
      return allChannels.find((channel) => channel.id === id);
    }) as Channel[];

    return orderBy(channelsList, ['sortTitle']);
  }, [channelIds, allChannels]);

  const onDeleteFilesChange = useCallback(
    ({ value }: CheckInputChanged) => {
      setDeleteFiles(value);
    },
    [setDeleteFiles]
  );

  const onDeleteOptionChange = useCallback(
    ({ name, value }: { name: string; value: boolean }) => {
      dispatch(
        setDeleteOption({
          [name]: value,
        })
      );
    },
    [dispatch]
  );

  const onDeleteChannelsConfirmed = useCallback(() => {
    setDeleteFiles(false);

    dispatch(
      bulkDeleteChannels({
        channelIds: channelIds,
        deleteFiles,
        addImportListExclusion,
      })
    );

    onModalClose();
  }, [
    channelIds,
    deleteFiles,
    addImportListExclusion,
    setDeleteFiles,
    dispatch,
    onModalClose,
  ]);

  const { totalVideoFileCount, totalSizeOnDisk } = useMemo(() => {
    return channels.reduce(
      (acc, { statistics = {} }) => {
        const { videoFileCount = 0, sizeOnDisk = 0 } = statistics;

        acc.totalVideoFileCount += videoFileCount;
        acc.totalSizeOnDisk += sizeOnDisk;

        return acc;
      },
      {
        totalVideoFileCount: 0,
        totalSizeOnDisk: 0,
      }
    );
  }, [channels]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>{translate('DeleteSelectedChannel')}</ModalHeader>

      <ModalBody>
        <div>
          <FormGroup>
            <FormLabel>{translate('AddListExclusion')}</FormLabel>

            <FormInputGroup
              type={inputTypes.CHECK}
              name="addImportListExclusion"
              value={addImportListExclusion}
              helpText={translate('AddListExclusionChannelHelpText')}
              onChange={onDeleteOptionChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>
              {channels.length > 1
                ? translate('DeleteChannelFolders')
                : translate('DeleteChannelFolder')}
            </FormLabel>

            <FormInputGroup
              type={inputTypes.CHECK}
              name="deleteFiles"
              value={deleteFiles}
              helpText={
                channels.length > 1
                  ? translate('DeleteChannelFoldersHelpText')
                  : translate('DeleteChannelFolderHelpText')
              }
              kind={kinds.DANGER}
              onChange={onDeleteFilesChange}
            />
          </FormGroup>
        </div>

        <div className={styles.message}>
          {deleteFiles
            ? translate('DeleteChannelFolderCountWithFilesConfirmation', {
                count: channels.length,
              })
            : translate('DeleteChannelFolderCountConfirmation', {
                count: channels.length,
              })}
        </div>

        <ul>
          {channels.map(({ title, path, statistics = {} }) => {
            const { videoFileCount = 0, sizeOnDisk = 0 } = statistics;

            return (
              <li key={title}>
                <span>{title}</span>

                {deleteFiles && (
                  <span>
                    <span className={styles.pathContainer}>
                      -<span className={styles.path}>{path}</span>
                    </span>

                    {!!videoFileCount && (
                      <span className={styles.statistics}>
                        (
                        {translate('DeleteChannelFolderVideoCount', {
                          videoFileCount,
                          size: formatBytes(sizeOnDisk),
                        })}
                        )
                      </span>
                    )}
                  </span>
                )}
              </li>
            );
          })}
        </ul>

        {deleteFiles && !!totalVideoFileCount ? (
          <div className={styles.deleteFilesMessage}>
            {translate('DeleteChannelFolderVideoCount', {
              videoFileCount: totalVideoFileCount,
              size: formatBytes(totalSizeOnDisk),
            })}
          </div>
        ) : null}
      </ModalBody>

      <ModalFooter>
        <Button onPress={onModalClose}>{translate('Cancel')}</Button>

        <Button kind={kinds.DANGER} onPress={onDeleteChannelsConfirmed}>
          {translate('Delete')}
        </Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default DeleteChannelModalContent;
