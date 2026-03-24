import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { createSelector } from 'reselect';
import { useSelect } from 'App/SelectContext';
import AppState from 'App/State/AppState';
import { RENAME_CHANNEL } from 'Commands/commandNames';
import SpinnerButton from 'Components/Link/SpinnerButton';
import PageContentFooter from 'Components/Page/PageContentFooter';
import usePrevious from 'Helpers/Hooks/usePrevious';
import { kinds } from 'Helpers/Props';
import { fetchRootFolders } from 'Store/Actions/rootFolderActions';
import {
  saveChannelEditor,
  updateChannelMonitor,
} from 'Store/Actions/channelActions';
import createCommandExecutingSelector from 'Store/Selectors/createCommandExecutingSelector';
import translate from 'Utilities/String/translate';
import getSelectedIds from 'Utilities/Table/getSelectedIds';
import DeleteChannelModal from './Delete/DeleteChannelModal';
import EditChannelModal from './Edit/EditChannelModal';
import OrganizeChannelModal from './Organize/OrganizeChannelModal';
import ChangeMonitoringModal from './PlaylistPass/ChangeMonitoringModal';
import TagsModal from './Tags/TagsModal';
import styles from './ChannelIndexSelectFooter.css';

interface SavePayload {
  monitored?: boolean;
  qualityProfileId?: number;
  channelType?: string;
  playlistFolder?: boolean;
  rootFolderPath?: string;
  moveFiles?: boolean;
}

const channelEditorSelector = createSelector(
  (state: AppState) => state.channels,
  (channelsState) => {
    const { isSaving, isDeleting, deleteError } = channelsState;

    return {
      isSaving,
      isDeleting,
      deleteError,
    };
  }
);

function ChannelIndexSelectFooter() {
  const { isSaving, isDeleting, deleteError } =
    useSelector(channelEditorSelector);

  const isOrganizingChannels = useSelector(
    createCommandExecutingSelector(RENAME_CHANNEL)
  );

  const dispatch = useDispatch();

  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [isOrganizeModalOpen, setIsOrganizeModalOpen] = useState(false);
  const [isTagsModalOpen, setIsTagsModalOpen] = useState(false);
  const [isMonitoringModalOpen, setIsMonitoringModalOpen] = useState(false);
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const [isSavingChannels, setIsSavingChannels] = useState(false);
  const [isSavingTags, setIsSavingTags] = useState(false);
  const [isSavingMonitoring, setIsSavingMonitoring] = useState(false);
  const previousIsDeleting = usePrevious(isDeleting);

  const [selectState, selectDispatch] = useSelect();
  const { selectedState } = selectState;

  const channelIds = useMemo(() => {
    return getSelectedIds(selectedState);
  }, [selectedState]);

  const selectedCount = channelIds.length;

  const onEditPress = useCallback(() => {
    setIsEditModalOpen(true);
  }, [setIsEditModalOpen]);

  const onEditModalClose = useCallback(() => {
    setIsEditModalOpen(false);
  }, [setIsEditModalOpen]);

  const onSavePress = useCallback(
    (payload: SavePayload) => {
      setIsSavingChannels(true);
      setIsEditModalOpen(false);

      dispatch(
        saveChannelEditor({
          ...payload,
          channelIds: channelIds,
        })
      );
    },
    [channelIds, dispatch]
  );

  const onOrganizePress = useCallback(() => {
    setIsOrganizeModalOpen(true);
  }, [setIsOrganizeModalOpen]);

  const onOrganizeModalClose = useCallback(() => {
    setIsOrganizeModalOpen(false);
  }, [setIsOrganizeModalOpen]);

  const onTagsPress = useCallback(() => {
    setIsTagsModalOpen(true);
  }, [setIsTagsModalOpen]);

  const onTagsModalClose = useCallback(() => {
    setIsTagsModalOpen(false);
  }, [setIsTagsModalOpen]);

  const onApplyTagsPress = useCallback(
    (tags: number[], applyTags: string) => {
      setIsSavingTags(true);
      setIsTagsModalOpen(false);

      dispatch(
        saveChannelEditor({
          channelIds: channelIds,
          tags,
          applyTags,
        })
      );
    },
    [channelIds, dispatch]
  );

  const onMonitoringPress = useCallback(() => {
    setIsMonitoringModalOpen(true);
  }, [setIsMonitoringModalOpen]);

  const onMonitoringClose = useCallback(() => {
    setIsMonitoringModalOpen(false);
  }, [setIsMonitoringModalOpen]);

  const onMonitoringSavePress = useCallback(
    (monitor: string, roundRobinLatestVideoCount?: number) => {
      if (!monitor || monitor === 'noChange') {
        return;
      }

      if (
        monitor === 'roundRobin' &&
        (!roundRobinLatestVideoCount || roundRobinLatestVideoCount <= 0)
      ) {
        return;
      }

      setIsSavingMonitoring(true);
      setIsMonitoringModalOpen(false);

      dispatch(
        updateChannelMonitor({
          channelIds: channelIds,
          monitor,
          ...(monitor === 'roundRobin'
            ? { roundRobinLatestVideoCount }
            : {}),
        })
      );
    },
    [channelIds, dispatch]
  );

  const onDeletePress = useCallback(() => {
    setIsDeleteModalOpen(true);
  }, [setIsDeleteModalOpen]);

  const onDeleteModalClose = useCallback(() => {
    setIsDeleteModalOpen(false);
  }, []);

  useEffect(() => {
    if (!isSaving) {
      setIsSavingChannels(false);
      setIsSavingTags(false);
      setIsSavingMonitoring(false);
    }
  }, [isSaving]);

  useEffect(() => {
    if (previousIsDeleting && !isDeleting && !deleteError) {
      selectDispatch({ type: 'unselectAll' });
    }
  }, [previousIsDeleting, isDeleting, deleteError, selectDispatch]);

  useEffect(() => {
    dispatch(fetchRootFolders());
  }, [dispatch]);

  const anySelected = selectedCount > 0;

  return (
    <PageContentFooter className={styles.footer}>
      <div className={styles.buttons}>
        <div className={styles.actionButtons}>
          <SpinnerButton
            isSpinning={isSaving && isSavingChannels}
            isDisabled={!anySelected || isOrganizingChannels}
            onPress={onEditPress}
          >
            {translate('Edit')}
          </SpinnerButton>

          <SpinnerButton
            kind={kinds.WARNING}
            isSpinning={isOrganizingChannels}
            isDisabled={!anySelected || isOrganizingChannels}
            onPress={onOrganizePress}
          >
            {translate('RenameFiles')}
          </SpinnerButton>

          <SpinnerButton
            isSpinning={isSaving && isSavingTags}
            isDisabled={!anySelected || isOrganizingChannels}
            onPress={onTagsPress}
          >
            {translate('SetTags')}
          </SpinnerButton>

          <SpinnerButton
            isSpinning={isSaving && isSavingMonitoring}
            isDisabled={!anySelected || isOrganizingChannels}
            onPress={onMonitoringPress}
          >
            {translate('UpdateMonitoring')}
          </SpinnerButton>
        </div>

        <div className={styles.deleteButtons}>
          <SpinnerButton
            kind={kinds.DANGER}
            isSpinning={isDeleting}
            isDisabled={!anySelected || isDeleting}
            onPress={onDeletePress}
          >
            {translate('Delete')}
          </SpinnerButton>
        </div>
      </div>

      <div className={styles.selected}>
        {translate('CountChannelSelected', { count: selectedCount })}
      </div>

      <EditChannelModal
        isOpen={isEditModalOpen}
        channelIds={channelIds}
        onSavePress={onSavePress}
        onModalClose={onEditModalClose}
      />

      <TagsModal
        isOpen={isTagsModalOpen}
        channelIds={channelIds}
        onApplyTagsPress={onApplyTagsPress}
        onModalClose={onTagsModalClose}
      />

      <ChangeMonitoringModal
        isOpen={isMonitoringModalOpen}
        channelIds={channelIds}
        onSavePress={onMonitoringSavePress}
        onModalClose={onMonitoringClose}
      />

      <OrganizeChannelModal
        isOpen={isOrganizeModalOpen}
        channelIds={channelIds}
        onModalClose={onOrganizeModalClose}
      />

      <DeleteChannelModal
        isOpen={isDeleteModalOpen}
        channelIds={channelIds}
        onModalClose={onDeleteModalClose}
      />
    </PageContentFooter>
  );
}

export default ChannelIndexSelectFooter;
