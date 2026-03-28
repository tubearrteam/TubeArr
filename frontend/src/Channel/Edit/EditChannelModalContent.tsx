import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import ChannelMonitoringOptionsPopoverContent from 'AddChannel/ChannelMonitoringOptionsPopoverContent';
import ChannelTypePopoverContent from 'AddChannel/ChannelTypePopoverContent';
import AppState from 'App/State/AppState';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import SpinnerErrorButton from 'Components/Link/SpinnerErrorButton';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import Popover from 'Components/Tooltip/Popover';
import {
  icons,
  inputTypes,
  kinds,
  tooltipPositions,
} from 'Helpers/Props';
import type Channel from 'Channel/Channel';
import useChannel from 'Channel/useChannel';
import { saveChannel, setChannelValue } from 'Store/Actions/channelActions';
import selectSettings from 'Store/Selectors/selectSettings';
import { InputChanged } from 'typings/inputs';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import translate from 'Utilities/String/translate';
import styles from './EditChannelModalContent.css';

export interface EditChannelModalContentProps {
  channelId: number;
  onModalClose: () => void;
  onDeleteChannelPress: () => void;
}
function EditChannelModalContent({
  channelId,
  onModalClose,
  onDeleteChannelPress,
}: EditChannelModalContentProps) {
  const dispatch = useDispatch();
  const channel = useChannel(channelId);
  const [diskFolderName, setDiskFolderName] = useState('');
  const abortFolderPreviewRef = useRef<(() => void) | null>(null);

  const isSmallScreen = useSelector(
    (state: AppState) => state.app.dimensions.isSmallScreen
  );
  const isWindows = useSelector(
    (state: AppState) => state.system.status.item?.isWindows ?? false
  );

  const channelData = (channel ?? {}) as Partial<Channel> & { thumbnailUrl?: string };

  const {
    title = '',
    overview,
    thumbnailUrl,
    images = [],
    youtubeChannelId = '',
    titleSlug = '',
    monitored = false,
    playlistFolder = true,
    filterOutShorts = false,
    filterOutLivestreams = false,
    qualityProfileId = 0,
    channelType: channelType = 'standard',
    tags = [],
    rootFolderPath = '',
    roundRobinLatestVideoCount: rrCount,
    monitorNewItems: channelMonitorNewItems = 'all',
    monitorPreset: channelMonitorPreset,
  } = channelData;

  const description = overview ?? '';
  const posterUrl =
    thumbnailUrl ??
    images.find((image) => image.coverType === 'poster')?.url ??
    images.find((image) => image.coverType === 'fanart')?.url;

  useEffect(() => {
    const yt = (youtubeChannelId || '').trim();

    if (abortFolderPreviewRef.current) {
      abortFolderPreviewRef.current();
      abortFolderPreviewRef.current = null;
    }

    if (!yt) {
      setDiskFolderName('');
      return undefined;
    }

    const { request, abortRequest } = createAjaxRequest({
      url: '/channels/folder-preview',
      data: {
        youtubeChannelId: yt,
        title: title || '',
        titleSlug: titleSlug || '',
      },
    });

    abortFolderPreviewRef.current = abortRequest;

    request
      .done((data) => {
        const folder = data && (data.folder != null ? data.folder : data.Folder);
        setDiskFolderName(typeof folder === 'string' ? folder.trim() : '');
      })
      .fail(() => {
        setDiskFolderName((titleSlug || '').trim());
      });

    return () => {
      if (abortFolderPreviewRef.current) {
        abortFolderPreviewRef.current();
        abortFolderPreviewRef.current = null;
      }
    };
  }, [youtubeChannelId, title, titleSlug]);

  const { isSaving, saveError, pendingChanges: sectionPendingChanges } = useSelector(
    (state: AppState) => state.channels
  );
  const keyed = sectionPendingChanges != null && channelId != null && typeof (sectionPendingChanges as Record<number, unknown>)[channelId] === 'object';
  const keyedPending = keyed ? (sectionPendingChanges as Record<number, Partial<Channel>>)[channelId] : undefined;
  const isKeyedStructure = sectionPendingChanges != null && typeof sectionPendingChanges === 'object' && !Array.isArray(sectionPendingChanges) &&
    Object.keys(sectionPendingChanges).length > 0 && Object.keys(sectionPendingChanges).every((k) => /^\d+$/.test(k));
  const pendingChanges: Partial<Channel> = keyedPending ?? (isKeyedStructure ? {} : ((sectionPendingChanges ?? {}) as Partial<Channel>));

  const { monitorSelectKey, roundRobinFieldValue } = useMemo(() => {
    const hasPendingMonitored = Object.prototype.hasOwnProperty.call(
      pendingChanges,
      'monitored'
    );
    const effectiveMonitored = hasPendingMonitored
      ? Boolean((pendingChanges as { monitored?: boolean }).monitored)
      : monitored;

    const hasPendingRr = Object.prototype.hasOwnProperty.call(
      pendingChanges,
      'roundRobinLatestVideoCount'
    );
    const pendingRrVal = hasPendingRr
      ? (pendingChanges as { roundRobinLatestVideoCount?: number | null }).roundRobinLatestVideoCount
      : undefined;
    const resolvedRr =
      pendingRrVal !== undefined ? pendingRrVal : rrCount;

    const hasPendingPreset = Object.prototype.hasOwnProperty.call(
      pendingChanges,
      'monitorPreset'
    );
    const effectivePreset = hasPendingPreset
      ? (pendingChanges as { monitorPreset?: string | null }).monitorPreset
      : channelMonitorPreset;

    const hasPendingMni = Object.prototype.hasOwnProperty.call(
      pendingChanges,
      'monitorNewItems'
    );
    const rawMni = hasPendingMni
      ? (pendingChanges as { monitorNewItems?: unknown }).monitorNewItems
      : channelMonitorNewItems;
    const mniIsNone =
      rawMni === 'none' ||
      rawMni === 0 ||
      rawMni === '0';

    const monitorKey = !effectiveMonitored
      ? 'none'
      : resolvedRr != null && resolvedRr > 0
        ? 'roundRobin'
        : effectivePreset === 'specificVideos'
          ? 'specificVideos'
          : effectivePreset === 'specificPlaylists'
            ? 'specificPlaylists'
            : effectiveMonitored && mniIsNone && !effectivePreset
              ? 'specificVideos'
              : 'all';

    const roundRobinVal =
      resolvedRr != null && resolvedRr > 0 ? resolvedRr : '';

    return {
      monitorSelectKey: monitorKey,
      roundRobinFieldValue: roundRobinVal,
    };
  }, [monitored, rrCount, pendingChanges, channelMonitorNewItems, channelMonitorPreset]);

  const { settings, ...otherSettings } = useMemo(() => {
    return selectSettings(
      {
        rootFolderPath,
        monitor: monitorSelectKey,
        playlistFolder,
        filterOutShorts,
        filterOutLivestreams,
        qualityProfileId,
        channelType: channelType,
        tags,
        roundRobinLatestVideoCount: roundRobinFieldValue,
      },
      pendingChanges,
      saveError
    );
  }, [
    monitorSelectKey,
    rootFolderPath,
    playlistFolder,
    filterOutShorts,
    filterOutLivestreams,
    qualityProfileId,
    channelType,
    tags,
    roundRobinFieldValue,
    pendingChanges,
    saveError,
  ]);

  const handleInputChange = useCallback(
    ({ name, value }: InputChanged) => {
      if (name === 'qualityProfileId') {
        // @ts-expect-error actions aren't typed
        dispatch(setChannelValue({ name, value: parseInt(value as string, 10), id: channelId }));
        return;
      }

      if (name === 'monitor') {
        const mode = String(value ?? '');

        if (mode === 'none') {
          // @ts-expect-error actions aren't typed
          dispatch(setChannelValue({ name: 'monitored', value: false, id: channelId }));
          // @ts-expect-error actions aren't typed
          dispatch(setChannelValue({ name: 'roundRobinLatestVideoCount', value: null, id: channelId }));
          // @ts-expect-error actions aren't typed
          dispatch(setChannelValue({ name: 'monitorPreset', value: null, id: channelId }));
          return;
        }

        if (mode === 'roundRobin') {
          const hasPendingRr = Object.prototype.hasOwnProperty.call(
            pendingChanges,
            'roundRobinLatestVideoCount'
          );
          const pendingRr = hasPendingRr
            ? (pendingChanges as { roundRobinLatestVideoCount?: number | null }).roundRobinLatestVideoCount
            : undefined;
          const baseRr =
            pendingRr !== undefined && pendingRr !== null && pendingRr > 0
              ? pendingRr
              : rrCount != null && rrCount > 0
                ? rrCount
                : 5;

          // @ts-expect-error actions aren't typed
          dispatch(setChannelValue({ name: 'monitored', value: true, id: channelId }));
          // @ts-expect-error actions aren't typed
          dispatch(setChannelValue({ name: 'monitorNewItems', value: 'all', id: channelId }));
          // @ts-expect-error actions aren't typed
          dispatch(setChannelValue({ name: 'roundRobinLatestVideoCount', value: baseRr, id: channelId }));
          // @ts-expect-error actions aren't typed
          dispatch(setChannelValue({ name: 'monitorPreset', value: null, id: channelId }));
          return;
        }

        if (mode === 'specificVideos') {
          // @ts-expect-error actions aren't typed
          dispatch(setChannelValue({ name: 'monitored', value: true, id: channelId }));
          // @ts-expect-error actions aren't typed
          dispatch(setChannelValue({ name: 'roundRobinLatestVideoCount', value: null, id: channelId }));
          // @ts-expect-error actions aren't typed
          dispatch(setChannelValue({ name: 'monitorNewItems', value: 'none', id: channelId }));
          // @ts-expect-error actions aren't typed
          dispatch(setChannelValue({ name: 'monitorPreset', value: 'specificVideos', id: channelId }));
          return;
        }

        if (mode === 'specificPlaylists') {
          // @ts-expect-error actions aren't typed
          dispatch(setChannelValue({ name: 'monitored', value: true, id: channelId }));
          // @ts-expect-error actions aren't typed
          dispatch(setChannelValue({ name: 'roundRobinLatestVideoCount', value: null, id: channelId }));
          // @ts-expect-error actions aren't typed
          dispatch(setChannelValue({ name: 'monitorNewItems', value: 'none', id: channelId }));
          // @ts-expect-error actions aren't typed
          dispatch(setChannelValue({ name: 'monitorPreset', value: 'specificPlaylists', id: channelId }));
          return;
        }

        // @ts-expect-error actions aren't typed
        dispatch(setChannelValue({ name: 'monitored', value: true, id: channelId }));
        // @ts-expect-error actions aren't typed
        dispatch(setChannelValue({ name: 'roundRobinLatestVideoCount', value: null, id: channelId }));
        // @ts-expect-error actions aren't typed
        dispatch(setChannelValue({ name: 'monitorNewItems', value: 'all', id: channelId }));
        // @ts-expect-error actions aren't typed
        dispatch(setChannelValue({ name: 'monitorPreset', value: null, id: channelId }));
        return;
      }

      if (name === 'roundRobinLatestVideoCount') {
        const raw = String(value ?? '').trim();
        const n = parseInt(raw, 10);
        const capped =
          raw === '' || !Number.isFinite(n) || n <= 0 ? null : n;
        // @ts-expect-error actions aren't typed
        dispatch(setChannelValue({ name, value: capped, id: channelId }));
        return;
      }

      // @ts-expect-error actions aren't typed
      dispatch(setChannelValue({ name, value, id: channelId }));
    },
    [dispatch, channelId, pendingChanges, rrCount]
  );

  const handleSavePress = useCallback(() => {
    dispatch(
      saveChannel({
        id: channelId,
        moveFiles: false,
      })
    );
  }, [channelId, dispatch]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>{translate('EditChannelModalHeader', { title })}</ModalHeader>

      <ModalBody>
        <div className={styles.container}>
          {
            isSmallScreen ?
              null :
              <div className={styles.poster}>
                {
                  posterUrl ?
                    <img
                      className={styles.poster}
                      alt={title}
                      src={posterUrl}
                    /> :
                    null
                }
              </div>
          }

          <div className={styles.info}>
            {
              description ?
                <div className={styles.overview}>
                  {description}
                </div> :
                null
            }

            <Form {...otherSettings}>
              <FormGroup>
                <FormLabel>{translate('RootFolder')}</FormLabel>

                <FormInputGroup
                  type={inputTypes.ROOT_FOLDER_SELECT}
                  name="rootFolderPath"
                  valueOptions={{
                    channelFolder:
                      (diskFolderName && diskFolderName.trim()) ||
                      (titleSlug && titleSlug.trim()) ||
                      undefined,
                    isWindows
                  }}
                  selectedValueOptions={{
                    channelFolder:
                      (diskFolderName && diskFolderName.trim()) ||
                      (titleSlug && titleSlug.trim()) ||
                      undefined,
                    isWindows
                  }}
                  onChange={handleInputChange}
                  {...settings.rootFolderPath}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('Monitor')}

                  <Popover
                    anchor={
                      <Icon
                        className={styles.labelIcon}
                        name={icons.INFO}
                      />
                    }
                    title={translate('MonitoringOptions')}
                    body={<ChannelMonitoringOptionsPopoverContent />}
                    position={tooltipPositions.RIGHT}
                  />
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.MONITOR_VIDEOS_SELECT}
                  name="monitor"
                  onChange={handleInputChange}
                  {...settings.monitor}
                />
              </FormGroup>

              {
                settings.monitor.value === 'roundRobin' ?
                  <FormGroup>
                    <FormLabel>
                      {translate('RoundRobinMonitoringLatestCount')}
                      <Popover
                        anchor={
                          <Icon
                            className={styles.labelIcon}
                            name={icons.INFO}
                          />
                        }
                        title={translate('RoundRobinMonitoring')}
                        body={translate('RoundRobinMonitoringHelpText')}
                        position={tooltipPositions.RIGHT}
                      />
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.NUMBER}
                      name="roundRobinLatestVideoCount"
                      min={1}
                      onChange={handleInputChange}
                      {...settings.roundRobinLatestVideoCount}
                      helpText={translate('RoundRobinMonitoringLatestCountHelp')}
                    />
                  </FormGroup> :
                  null
              }

              <FormGroup>
                <FormLabel>{translate('QualityProfile')}</FormLabel>

                <FormInputGroup
                  type={inputTypes.QUALITY_PROFILE_SELECT}
                  name="qualityProfileId"
                  onChange={handleInputChange}
                  {...settings.qualityProfileId}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('ChannelType')}

                  <Popover
                    anchor={
                      <Icon
                        className={styles.labelIcon}
                        name={icons.INFO}
                      />
                    }
                    title={translate('ChannelTypes')}
                    body={<ChannelTypePopoverContent />}
                    position={tooltipPositions.RIGHT}
                  />
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.CHANNEL_TYPE_SELECT}
                  name="channelType"
                  onChange={handleInputChange}
                  {...settings.channelType}
                  helpText={translate('ChannelTypesHelpText')}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>{translate('PlaylistFolder')}</FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="playlistFolder"
                  onChange={handleInputChange}
                  {...settings.playlistFolder}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>{translate('ChannelFilterOutShorts')}</FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="filterOutShorts"
                  onChange={handleInputChange}
                  {...settings.filterOutShorts}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>{translate('ChannelFilterOutLivestreams')}</FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="filterOutLivestreams"
                  onChange={handleInputChange}
                  {...settings.filterOutLivestreams}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>{translate('Tags')}</FormLabel>

                <FormInputGroup
                  type={inputTypes.TAG}
                  name="tags"
                  onChange={handleInputChange}
                  {...settings.tags}
                />
              </FormGroup>
            </Form>
          </div>
        </div>
      </ModalBody>

      <ModalFooter className={styles.modalFooter}>
        <Button
          kind={kinds.DANGER}
          onPress={onDeleteChannelPress}
        >
          {translate('Delete')}
        </Button>

        <div>
          <Button onPress={onModalClose}>{translate('Cancel')}</Button>

          <SpinnerErrorButton
            className={styles.addButton}
            error={saveError}
            isSpinning={isSaving}
            onPress={handleSavePress}
          >
            {translate('Save')}
          </SpinnerErrorButton>
        </div>
      </ModalFooter>
    </ModalContent>
  );
}

export default EditChannelModalContent;
