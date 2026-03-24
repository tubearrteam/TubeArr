import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputButton from 'Components/Form/FormInputButton';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import useModalOpenState from 'Helpers/Hooks/useModalOpenState';
import { inputTypes, kinds, sizes } from 'Helpers/Props';
import { clearPendingChanges } from 'Store/Actions/baseActions';
import {
  fetchNamingExamples,
  fetchNamingSettings,
  setNamingSettingsValue,
} from 'Store/Actions/settingsActions';
import createSettingsSectionSelector from 'Store/Selectors/createSettingsSectionSelector';
import NamingConfig from 'typings/Settings/NamingConfig';
import translate from 'Utilities/String/translate';
import NamingModal from './NamingModal';
import styles from './Naming.css';

const SECTION = 'naming';

function createNamingSelector() {
  return createSelector(
    (state: AppState) => state.settings.advancedSettings,
    (state: AppState) => state.settings.namingExamples,
    createSettingsSectionSelector(SECTION),
    (advancedSettings, namingExamples, sectionSettings) => {
      return {
        advancedSettings,
        examples: namingExamples.item,
        examplesPopulated: namingExamples.isPopulated,
        ...sectionSettings,
      };
    }
  );
}

interface NamingModalOptions {
  name: keyof Pick<
    NamingConfig,
    | 'standardVideoFormat'
    | 'dailyVideoFormat'
    | 'episodicVideoFormat'
    | 'streamingVideoFormat'
    | 'channelFolderFormat'
    | 'playlistFolderFormat'
    | 'specialsFolderFormat'
  >;
  playlist?: boolean;
  video?: boolean;
  daily?: boolean;
  anime?: boolean;
  additional?: boolean;
}

function Naming() {
  const {
    advancedSettings,
    isFetching,
    error,
    settings,
    hasSettings,
    examples,
    examplesPopulated,
  } = useSelector(createNamingSelector());

  const dispatch = useDispatch();

  const [isNamingModalOpen, setNamingModalOpen, setNamingModalClosed] =
    useModalOpenState(false);
  const [namingModalOptions, setNamingModalOptions] =
    useState<NamingModalOptions | null>(null);
  const namingExampleTimeout = useRef<ReturnType<typeof setTimeout>>();

  useEffect(() => {
    dispatch(fetchNamingSettings());
    dispatch(fetchNamingExamples());

    return () => {
      dispatch(clearPendingChanges({ section: SECTION }));
    };
  }, [dispatch]);

  const handleInputChange = useCallback(
    ({ name, value }: { name: string; value: string }) => {
      // @ts-expect-error 'setNamingSettingsValue' isn't typed yet
      dispatch(setNamingSettingsValue({ name, value }));

      if (namingExampleTimeout.current) {
        clearTimeout(namingExampleTimeout.current);
      }

      namingExampleTimeout.current = setTimeout(() => {
        dispatch(fetchNamingExamples());
      }, 1000);
    },
    [dispatch]
  );

  const onStandardNamingModalOpenClick = useCallback(() => {
    setNamingModalOpen();

    setNamingModalOptions({
      name: 'standardVideoFormat',
      playlist: true,
      video: true,
      additional: true,
    });
  }, [setNamingModalOpen, setNamingModalOptions]);

  const onDailyNamingModalOpenClick = useCallback(() => {
    setNamingModalOpen();

    setNamingModalOptions({
      name: 'dailyVideoFormat',
      playlist: true,
      video: true,
      daily: true,
      additional: true,
    });
  }, [setNamingModalOpen, setNamingModalOptions]);

  const onEpisodicNamingModalOpenClick = useCallback(() => {
    setNamingModalOpen();

    setNamingModalOptions({
      name: 'episodicVideoFormat',
      playlist: true,
      video: true,
      anime: true,
      additional: true,
    });
  }, [setNamingModalOpen, setNamingModalOptions]);

  const onChannelFolderNamingModalOpenClick = useCallback(() => {
    setNamingModalOpen();

    setNamingModalOptions({
      name: 'channelFolderFormat',
    });
  }, [setNamingModalOpen, setNamingModalOptions]);

  const onPlaylistFolderNamingModalOpenClick = useCallback(() => {
    setNamingModalOpen();

    setNamingModalOptions({
      name: 'playlistFolderFormat',
      playlist: true,
    });
  }, [setNamingModalOpen, setNamingModalOptions]);

  const onSpecialsFolderNamingModalOpenClick = useCallback(() => {
    setNamingModalOpen();

    setNamingModalOptions({
      name: 'specialsFolderFormat',
      playlist: true,
    });
  }, [setNamingModalOpen, setNamingModalOptions]);

  const renameVideos = hasSettings && settings.renameVideos.value;
  const replaceIllegalCharacters =
    hasSettings && settings.replaceIllegalCharacters.value;

  const multiVideoStyleOptions = [
    { key: 0, value: translate('Extend'), hint: 'S01E01-02-03' },
    { key: 1, value: translate('Duplicate'), hint: 'S01E01.S01E02' },
    { key: 2, value: translate('Repeat'), hint: 'S01E01E02E03' },
    { key: 4, value: translate('Range'), hint: 'S01E01-03' },
    { key: 5, value: translate('PrefixedRange'), hint: 'S01E01-E03' },
  ];

  const colonReplacementOptions = [
    { key: 0, value: translate('Delete') },
    { key: 1, value: translate('ReplaceWithDash') },
    { key: 2, value: translate('ReplaceWithSpaceDash') },
    { key: 3, value: translate('ReplaceWithSpaceDashSpace') },
    {
      key: 4,
      value: translate('SmartReplace'),
      hint: translate('SmartReplaceHint'),
    },
    {
      key: 5,
      value: translate('Custom'),
      hint: translate('CustomColonReplacementFormatHint'),
    },
  ];

  const standardVideoFormatHelpTexts = [];
  const standardVideoFormatErrors = [];
  const dailyVideoFormatHelpTexts = [];
  const dailyVideoFormatErrors = [];
  const episodicVideoFormatHelpTexts = [];
  const episodicVideoFormatErrors = [];
  const channelFolderFormatHelpTexts = [];
  const channelFolderFormatErrors = [];
  const playlistFolderFormatHelpTexts = [];
  const playlistFolderFormatErrors = [];
  const specialsFolderFormatHelpTexts = [];
  const specialsFolderFormatErrors = [];

  if (examplesPopulated) {
    if (examples.singleVideoExample) {
      standardVideoFormatHelpTexts.push(
        `${translate('SingleVideo')}: ${examples.singleVideoExample}`
      );
    } else {
      standardVideoFormatErrors.push({
        message: translate('SingleVideoInvalidFormat'),
      });
    }

    if (examples.multiVideoExample) {
      standardVideoFormatHelpTexts.push(
        `${translate('MultiVideo')}: ${examples.multiVideoExample}`
      );
    } else {
      standardVideoFormatErrors.push({
        message: translate('MultiVideoInvalidFormat'),
      });
    }

    if (examples.dailyVideoExample) {
      dailyVideoFormatHelpTexts.push(
        `${translate('Example')}: ${examples.dailyVideoExample}`
      );
    } else {
      dailyVideoFormatErrors.push({ message: translate('InvalidFormat') });
    }

    if (examples.episodicVideoExample) {
      episodicVideoFormatHelpTexts.push(
        `${translate('SingleVideo')}: ${examples.episodicVideoExample}`
      );
    } else {
      episodicVideoFormatErrors.push({
        message: translate('SingleVideoInvalidFormat'),
      });
    }

    if (examples.episodicMultiVideoExample) {
      episodicVideoFormatHelpTexts.push(
        `${translate('MultiVideo')}: ${examples.episodicMultiVideoExample}`
      );
    } else {
      episodicVideoFormatErrors.push({
        message: translate('MultiVideoInvalidFormat'),
      });
    }

    if (examples.channelFolderExample) {
      channelFolderFormatHelpTexts.push(
        `${translate('Example')}: ${examples.channelFolderExample}`
      );
    } else {
      channelFolderFormatErrors.push({ message: translate('InvalidFormat') });
    }

    if (examples.playlistFolderExample) {
      playlistFolderFormatHelpTexts.push(
        `${translate('Example')}: ${examples.playlistFolderExample}`
      );
    } else {
      playlistFolderFormatErrors.push({ message: translate('InvalidFormat') });
    }

    if (examples.specialsFolderExample) {
      specialsFolderFormatHelpTexts.push(
        `${translate('Example')}: ${examples.specialsFolderExample}`
      );
    } else {
      specialsFolderFormatErrors.push({ message: translate('InvalidFormat') });
    }
  }

  return (
    <FieldSet legend={translate('VideoNaming')}>
      {isFetching ? <LoadingIndicator /> : null}

      {!isFetching && error ? (
        <Alert kind={kinds.DANGER}>
          {translate('NamingSettingsLoadError')}
        </Alert>
      ) : null}

      {hasSettings && !isFetching && !error ? (
        <Form>
          <FormGroup size={sizes.MEDIUM}>
            <FormLabel>{translate('RenameVideos')}</FormLabel>

            <FormInputGroup
              type={inputTypes.CHECK}
              name="renameVideos"
              helpText={translate('RenameVideosHelpText')}
              onChange={handleInputChange}
              {...settings.renameVideos}
            />
          </FormGroup>

          <FormGroup size={sizes.MEDIUM}>
            <FormLabel>{translate('ReplaceIllegalCharacters')}</FormLabel>

            <FormInputGroup
              type={inputTypes.CHECK}
              name="replaceIllegalCharacters"
              helpText={translate('ReplaceIllegalCharactersHelpText')}
              onChange={handleInputChange}
              {...settings.replaceIllegalCharacters}
            />
          </FormGroup>

          {replaceIllegalCharacters ? (
            <FormGroup size={sizes.MEDIUM}>
              <FormLabel>{translate('ColonReplacement')}</FormLabel>

              <FormInputGroup
                type={inputTypes.SELECT}
                name="colonReplacementFormat"
                values={colonReplacementOptions}
                helpText={translate('ColonReplacementFormatHelpText')}
                onChange={handleInputChange}
                {...settings.colonReplacementFormat}
              />
            </FormGroup>
          ) : null}

          {replaceIllegalCharacters &&
          settings.colonReplacementFormat.value === 5 ? (
            <FormGroup size={sizes.MEDIUM}>
              <FormLabel>{translate('CustomColonReplacement')}</FormLabel>

              <FormInputGroup
                type={inputTypes.TEXT}
                name="customColonReplacementFormat"
                helpText={translate('CustomColonReplacementFormatHelpText')}
                onChange={handleInputChange}
                {...settings.customColonReplacementFormat}
              />
            </FormGroup>
          ) : null}

          {renameVideos ? (
            <>
              <FormGroup size={sizes.LARGE}>
            <FormLabel>{translate('StandardVideoFormat')}</FormLabel>

                <FormInputGroup
                  inputClassName={styles.namingInput}
                  type={inputTypes.TEXT}
                  name="standardVideoFormat"
                  buttons={
                    <FormInputButton onPress={onStandardNamingModalOpenClick}>
                      ?
                    </FormInputButton>
                  }
                  onChange={handleInputChange}
                  {...settings.standardVideoFormat}
                  helpTexts={standardVideoFormatHelpTexts}
                  errors={[
                    ...standardVideoFormatErrors,
                    ...settings.standardVideoFormat.errors,
                  ]}
                />
              </FormGroup>

              <FormGroup size={sizes.LARGE}>
            <FormLabel>{translate('DailyVideoFormat')}</FormLabel>

                <FormInputGroup
                  inputClassName={styles.namingInput}
                  type={inputTypes.TEXT}
                  name="dailyVideoFormat"
                  buttons={
                    <FormInputButton onPress={onDailyNamingModalOpenClick}>
                      ?
                    </FormInputButton>
                  }
                  onChange={handleInputChange}
                  {...settings.dailyVideoFormat}
                  helpTexts={dailyVideoFormatHelpTexts}
                  errors={[
                    ...dailyVideoFormatErrors,
                    ...settings.dailyVideoFormat.errors,
                  ]}
                />
              </FormGroup>

              <FormGroup size={sizes.LARGE}>
            <FormLabel>{translate('EpisodicVideoFormat')}</FormLabel>

                <FormInputGroup
                  inputClassName={styles.namingInput}
                  type={inputTypes.TEXT}
                  name="episodicVideoFormat"
                  buttons={
                    <FormInputButton onPress={onEpisodicNamingModalOpenClick}>
                      ?
                    </FormInputButton>
                  }
                  onChange={handleInputChange}
                  {...settings.episodicVideoFormat}
                  helpTexts={episodicVideoFormatHelpTexts}
                  errors={[
                    ...episodicVideoFormatErrors,
                    ...settings.episodicVideoFormat.errors,
                  ]}
                />
              </FormGroup>
            </>
          ) : null}

          <FormGroup
            advancedSettings={advancedSettings}
            isAdvanced={true}
            size={sizes.MEDIUM}
          >
            <FormLabel>{translate('ChannelFolderFormat')}</FormLabel>

            <FormInputGroup
              inputClassName={styles.namingInput}
              type={inputTypes.TEXT}
              name="channelFolderFormat"
              buttons={
                <FormInputButton onPress={onChannelFolderNamingModalOpenClick}>
                  ?
                </FormInputButton>
              }
              onChange={handleInputChange}
              {...settings.channelFolderFormat}
              helpTexts={[
                translate('ChannelFolderFormatHelpText'),
                ...channelFolderFormatHelpTexts,
              ]}
              errors={[
                ...channelFolderFormatErrors,
                ...settings.channelFolderFormat.errors,
              ]}
            />
          </FormGroup>

          <FormGroup size={sizes.MEDIUM}>
            <FormLabel>{translate('PlaylistFolderFormat')}</FormLabel>

            <FormInputGroup
              inputClassName={styles.namingInput}
              type={inputTypes.TEXT}
              name="playlistFolderFormat"
              buttons={
                <FormInputButton onPress={onPlaylistFolderNamingModalOpenClick}>
                  ?
                </FormInputButton>
              }
              onChange={handleInputChange}
              {...settings.playlistFolderFormat}
              helpTexts={playlistFolderFormatHelpTexts}
              errors={[
                ...playlistFolderFormatErrors,
                ...settings.playlistFolderFormat.errors,
              ]}
            />
          </FormGroup>

          <FormGroup
            advancedSettings={advancedSettings}
            isAdvanced={true}
            size={sizes.MEDIUM}
          >
            <FormLabel>{translate('SpecialsFolderFormat')}</FormLabel>

            <FormInputGroup
              inputClassName={styles.namingInput}
              type={inputTypes.TEXT}
              name="specialsFolderFormat"
              buttons={
                <FormInputButton onPress={onSpecialsFolderNamingModalOpenClick}>
                  ?
                </FormInputButton>
              }
              onChange={handleInputChange}
              {...settings.specialsFolderFormat}
              helpTexts={specialsFolderFormatHelpTexts}
              errors={[
                ...specialsFolderFormatErrors,
                ...settings.specialsFolderFormat.errors,
              ]}
            />
          </FormGroup>

          <FormGroup size={sizes.MEDIUM}>
            <FormLabel>{translate('MultiVideoStyle')}</FormLabel>

            <FormInputGroup
              type={inputTypes.SELECT}
              name="multiVideoStyle"
              values={multiVideoStyleOptions}
              onChange={handleInputChange}
              {...settings.multiVideoStyle}
            />
          </FormGroup>

          {namingModalOptions ? (
            <NamingModal
              isOpen={isNamingModalOpen}
              {...namingModalOptions}
              value={settings[namingModalOptions.name].value}
              onInputChange={handleInputChange}
              onModalClose={setNamingModalClosed}
            />
          ) : null}
        </Form>
      ) : null}
    </FieldSet>
  );
}

export default Naming;
