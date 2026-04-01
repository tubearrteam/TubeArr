import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Button from 'Components/Link/Button';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { inputTypes, kinds, sizes } from 'Helpers/Props';
import RootFolders from 'RootFolder/RootFolders';
import SettingsToolbarConnector from 'Settings/SettingsToolbarConnector';
import translate from 'Utilities/String/translate';
import Naming from './Naming/Naming';
import AddRootFolder from './RootFolder/AddRootFolder';

const videoTitleRequiredOptions = [
  {
    key: 'always',
    get value() {
      return translate('Always');
    }
  },
  {
    key: 'bulkPlaylistReleases',
    get value() {
      return translate('OnlyForBulkPlaylistReleases');
    }
  },
  {
    key: 'never',
    get value() {
      return translate('Never');
    }
  }
];

const rescanAfterRefreshOptions = [
  {
    key: 'always',
    get value() {
      return translate('Always');
    }
  },
  {
    key: 'afterManual',
    get value() {
      return translate('AfterManualRefresh');
    }
  },
  {
    key: 'never',
    get value() {
      return translate('Never');
    }
  }
];

const downloadPropersAndRepacksOptions = [
  {
    key: 'preferAndUpgrade',
    get value() {
      return translate('PreferAndUpgrade');
    }
  },
  {
    key: 'doNotUpgrade',
    get value() {
      return translate('DoNotUpgradeAutomatically');
    }
  },
  {
    key: 'doNotPrefer',
    get value() {
      return translate('DoNotPrefer');
    }
  }
];

const fileDateOptions = [
  {
    key: 'none',
    get value() {
      return translate('None');
    }
  },
  {
    key: 'localAirDate',
    get value() {
      return translate('LocalAirDate');
    }
  },
  {
    key: 'utcAirDate',
    get value() {
      return translate('UtcAirDate');
    }
  }
];

class MediaManagement extends Component {

  //
  // Render

  render() {
    const {
      advancedSettings,
      isFetching,
      error,
      settings,
      plexProvider,
      hasSettings,
      isWindows,
      customNfosSavedOff,
      isRemovingManagedNfos,
      lastManagedNfoRemoval,
      managedNfoRemovalError,
      onRemoveManagedNfosPress,
      onClearNfoRemovalFeedback,
      onInputChange,
      onSavePress,
      ...otherProps
    } = this.props;

    return (
      <PageContent title={translate('MediaManagementSettings')}>
        <SettingsToolbarConnector
          advancedSettings={advancedSettings}
          {...otherProps}
          onSavePress={onSavePress}
        />

        <PageContentBody>
          <Naming />

          {
            isFetching ?
              <FieldSet legend={translate('NamingSettings')}>
                <LoadingIndicator />
              </FieldSet> : null
          }

          {
            !isFetching && error ?
              <FieldSet legend={translate('NamingSettings')}>
                <Alert kind={kinds.DANGER}>
                  {translate('MediaManagementSettingsLoadError')}
                </Alert>
              </FieldSet> : null
          }

          {
            hasSettings && !isFetching && !error ?
              <Form
                id="mediaManagementSettings"
                {...otherProps}
              >
                <FieldSet legend={translate('PlexMetadataProvider')}>
                  {
                    plexProvider && plexProvider.isFetching ?
                      <LoadingIndicator /> : null
                  }

                  {
                    plexProvider && !plexProvider.isFetching && plexProvider.error ?
                      <Alert kind={kinds.DANGER}>
                        {translate('PlexProviderSettingsLoadError')}
                      </Alert> : null
                  }

                  {
                    plexProvider && plexProvider.hasSettings && !plexProvider.isFetching && !plexProvider.error ?
                      <>
                        <FormGroup size={sizes.MEDIUM}>
                          <FormLabel>{translate('EnablePlexMetadataProvider')}</FormLabel>

                          <FormInputGroup
                            type={inputTypes.CHECK}
                            name="plexProvider.enabled"
                            helpText={translate('EnablePlexMetadataProviderHelpText')}
                            onChange={onInputChange}
                            {...plexProvider.settings.enabled}
                          />
                        </FormGroup>

                        <FormGroup size={sizes.MEDIUM}>
                          <FormLabel>{translate('DownloadLibraryThumbnails')}</FormLabel>

                          <FormInputGroup
                            type={inputTypes.CHECK}
                            name="downloadLibraryThumbnails"
                            helpText={translate('DownloadLibraryThumbnailsHelpText')}
                            onChange={onInputChange}
                            {...settings.downloadLibraryThumbnails}
                          />
                        </FormGroup>

                        <FormGroup
                          advancedSettings={advancedSettings}
                          isAdvanced={true}
                          size={sizes.MEDIUM}
                        >
                          <FormLabel>{translate('PlexProviderBasePath')}</FormLabel>

                          <FormInputGroup
                            type={inputTypes.TEXT}
                            name="plexProvider.basePath"
                            helpText={translate('PlexProviderBasePathHelpText')}
                            onChange={onInputChange}
                            {...plexProvider.settings.basePath}
                          />
                        </FormGroup>

                        <FormGroup
                          advancedSettings={advancedSettings}
                          isAdvanced={true}
                          size={sizes.MEDIUM}
                        >
                          <FormLabel>{translate('PlexProviderIncludeChildrenByDefault')}</FormLabel>

                          <FormInputGroup
                            type={inputTypes.CHECK}
                            name="plexProvider.includeChildrenByDefault"
                            helpText={translate('PlexProviderIncludeChildrenByDefaultHelpText')}
                            onChange={onInputChange}
                            {...plexProvider.settings.includeChildrenByDefault}
                          />
                        </FormGroup>

                        <FormGroup
                          advancedSettings={advancedSettings}
                          isAdvanced={true}
                          size={sizes.MEDIUM}
                        >
                          <FormLabel>{translate('PlexProviderVerboseLogging')}</FormLabel>

                          <FormInputGroup
                            type={inputTypes.CHECK}
                            name="plexProvider.verboseRequestLogging"
                            helpText={translate('PlexProviderVerboseLoggingHelpText')}
                            onChange={onInputChange}
                            {...plexProvider.settings.verboseRequestLogging}
                          />
                        </FormGroup>

                        <FormGroup
                          advancedSettings={advancedSettings}
                          isAdvanced={true}
                          size={sizes.MEDIUM}
                        >
                          <FormLabel>{translate('PlexProviderExposeArtworkUrls')}</FormLabel>

                          <FormInputGroup
                            type={inputTypes.CHECK}
                            name="plexProvider.exposeArtworkUrls"
                            helpText={translate('PlexProviderExposeArtworkUrlsHelpText')}
                            onChange={onInputChange}
                            {...plexProvider.settings.exposeArtworkUrls}
                          />
                        </FormGroup>
                      </> : null
                  }
                </FieldSet>

                {
                  advancedSettings ?
                    <FieldSet legend={translate('Folders')}>
                      <FormGroup
                        advancedSettings={advancedSettings}
                        isAdvanced={true}
                        size={sizes.MEDIUM}
                      >
                        <FormLabel>{translate('CreateEmptyChannelFolders')}</FormLabel>

                        <FormInputGroup
                          type={inputTypes.CHECK}
                          name="createEmptyChannelFolders"
                          helpText={translate('CreateEmptyChannelFoldersHelpText')}
                          onChange={onInputChange}
                          {...settings.createEmptyChannelFolders}
                        />
                      </FormGroup>

                      <FormGroup
                        advancedSettings={advancedSettings}
                        isAdvanced={true}
                        size={sizes.MEDIUM}
                      >
                        <FormLabel>{translate('DeleteEmptyFolders')}</FormLabel>

                        <FormInputGroup
                          type={inputTypes.CHECK}
                          name="deleteEmptyFolders"
                          helpText={translate('DeleteEmptyChannelFoldersHelpText')}
                          onChange={onInputChange}
                          {...settings.deleteEmptyFolders}
                        />
                      </FormGroup>
                    </FieldSet> : null
                }

                {
                  advancedSettings ?
                    <FieldSet
                      legend={translate('Importing')}
                    >
                      <FormGroup
                        advancedSettings={advancedSettings}
                        isAdvanced={true}
                        size={sizes.SMALL}
                      >
                        <FormLabel>{translate('VideoTitleRequired')}</FormLabel>

                        <FormInputGroup
                          type={inputTypes.SELECT}
                          name="videoTitleRequired"
                          helpText={translate('VideoTitleRequiredHelpText')}
                          values={videoTitleRequiredOptions}
                          onChange={onInputChange}
                          {...settings.videoTitleRequired}
                        />
                      </FormGroup>

                      <FormGroup
                        advancedSettings={advancedSettings}
                        isAdvanced={true}
                        size={sizes.MEDIUM}
                      >
                        <FormLabel>{translate('SkipFreeSpaceCheck')}</FormLabel>

                        <FormInputGroup
                          type={inputTypes.CHECK}
                          name="skipFreeSpaceCheckWhenImporting"
                          helpText={translate('SkipFreeSpaceCheckHelpText')}
                          onChange={onInputChange}
                          {...settings.skipFreeSpaceCheckWhenImporting}
                        />
                      </FormGroup>

                      <FormGroup
                        advancedSettings={advancedSettings}
                        isAdvanced={true}
                        size={sizes.MEDIUM}
                      >
                        <FormLabel>{translate('MinimumFreeSpace')}</FormLabel>

                        <FormInputGroup
                          type={inputTypes.NUMBER}
                          unit='MB'
                          name="minimumFreeSpaceWhenImporting"
                          helpText={translate('MinimumFreeSpaceHelpText')}
                          onChange={onInputChange}
                          {...settings.minimumFreeSpaceWhenImporting}
                        />
                      </FormGroup>

                      <FormGroup
                        advancedSettings={advancedSettings}
                        isAdvanced={true}
                        size={sizes.MEDIUM}
                      >
                        <FormLabel>{translate('UseHardlinksInsteadOfCopy')}</FormLabel>

                        <FormInputGroup
                          type={inputTypes.CHECK}
                          name="copyUsingHardlinks"
                          helpText={translate('CopyUsingHardlinksChannelHelpText')}
                          helpTextWarning={translate('CopyUsingHardlinksHelpTextWarning')}
                          onChange={onInputChange}
                          {...settings.copyUsingHardlinks}
                        />
                      </FormGroup>

                      <FormGroup
                        advancedSettings={advancedSettings}
                        isAdvanced={true}
                        size={sizes.MEDIUM}
                      >
                        <FormLabel>{translate('ImportUsingScript')}</FormLabel>

                        <FormInputGroup
                          type={inputTypes.CHECK}
                          name="useScriptImport"
                          helpText={translate('ImportUsingScriptHelpText')}
                          onChange={onInputChange}
                          {...settings.useScriptImport}
                        />
                      </FormGroup>

                      {
                        settings.useScriptImport.value ?
                          <FormGroup
                            advancedSettings={advancedSettings}
                            isAdvanced={true}
                          >
                            <FormLabel>{translate('ImportScriptPath')}</FormLabel>

                            <FormInputGroup
                              type={inputTypes.PATH}
                              includeFiles={true}
                              name="scriptImportPath"
                              helpText={translate('ImportScriptPathHelpText')}
                              onChange={onInputChange}
                              {...settings.scriptImportPath}
                            />
                          </FormGroup> : null
                      }

                      <FormGroup size={sizes.MEDIUM}>
                        <FormLabel>{translate('ImportExtraFiles')}</FormLabel>

                        <FormInputGroup
                          type={inputTypes.CHECK}
                          name="importExtraFiles"
                          helpText={translate('ImportExtraFilesVideoHelpText')}
                          onChange={onInputChange}
                          {...settings.importExtraFiles}
                        />
                      </FormGroup>

                      {
                        settings.importExtraFiles.value ?
                          <FormGroup
                            advancedSettings={advancedSettings}
                            isAdvanced={true}
                          >
                            <FormLabel>{translate('ImportExtraFiles')}</FormLabel>

                            <FormInputGroup
                              type={inputTypes.TEXT}
                              name="extraFileExtensions"
                              helpTexts={[
                                translate('ExtraFileExtensionsHelpText'),
                                translate('ExtraFileExtensionsHelpTextsExamples')
                              ]}
                              onChange={onInputChange}
                              {...settings.extraFileExtensions}
                            />
                          </FormGroup> : null
                      }
                    </FieldSet> : null
                }

                <FieldSet
                  legend={translate('FileManagement')}
                >
                  <FormGroup size={sizes.MEDIUM}>
                    <FormLabel>{translate('UnmonitorDeletedVideos')}</FormLabel>

                    <FormInputGroup
                      type={inputTypes.CHECK}
                      name="autoUnmonitorPreviouslyDownloadedVideos"
                      helpText={translate('UnmonitorDeletedVideosHelpText')}
                      onChange={onInputChange}
                      {...settings.autoUnmonitorPreviouslyDownloadedVideos}
                    />
                  </FormGroup>

                  <FormGroup
                    advancedSettings={advancedSettings}
                    isAdvanced={true}
                    size={sizes.MEDIUM}
                  >
                    <FormLabel>{translate('DownloadPropersAndRepacks')}</FormLabel>

                    <FormInputGroup
                      type={inputTypes.SELECT}
                      name="downloadPropersAndRepacks"
                      helpTexts={[
                        translate('DownloadPropersAndRepacksHelpText'),
                        translate('DownloadPropersAndRepacksHelpTextCustomFormat')
                      ]}
                      helpTextWarning={
                        settings.downloadPropersAndRepacks.value === 'doNotPrefer' ?
                          translate('DownloadPropersAndRepacksHelpTextWarning') :
                          undefined
                      }
                      values={downloadPropersAndRepacksOptions}
                      onChange={onInputChange}
                      {...settings.downloadPropersAndRepacks}
                    />
                  </FormGroup>

                  <FormGroup
                    advancedSettings={advancedSettings}
                    isAdvanced={true}
                    size={sizes.MEDIUM}
                  >
                    <FormLabel>{translate('AnalyseVideoFiles')}</FormLabel>

                    <FormInputGroup
                      type={inputTypes.CHECK}
                      name="enableMediaInfo"
                      helpText={translate('AnalyseVideoFilesHelpText')}
                      onChange={onInputChange}
                      {...settings.enableMediaInfo}
                    />
                  </FormGroup>

                  <FormGroup
                    advancedSettings={advancedSettings}
                    isAdvanced={true}
                    size={sizes.MEDIUM}
                  >
                    <FormLabel>{translate('UseCustomNfos')}</FormLabel>

                    <FormInputGroup
                      type={inputTypes.CHECK}
                      name="useCustomNfos"
                      helpText={translate('UseCustomNfosHelpText')}
                      onChange={onInputChange}
                      {...settings.useCustomNfos}
                    />

                    {
                      customNfosSavedOff ?
                        <div style={{ marginTop: '0.75rem' }}>
                          {
                            lastManagedNfoRemoval?.message ?
                              <Alert kind={kinds.SUCCESS}>
                                {lastManagedNfoRemoval.message}
                                {' '}
                                <Button
                                  kind={kinds.DEFAULT}
                                  onPress={onClearNfoRemovalFeedback}
                                >
                                  {translate('Close')}
                                </Button>
                              </Alert> :
                              null
                          }
                          {
                            managedNfoRemovalError ?
                              <Alert kind={kinds.DANGER}>
                                {translate('RemoveManagedNfosFailed')}
                              </Alert> :
                              null
                          }
                          <FormLabel>{translate('RemoveManagedNfosFromLibrary')}</FormLabel>
                          <p className="help-text">{translate('RemoveManagedNfosFromLibraryHelpText')}</p>
                          <Button
                            kind={kinds.WARNING}
                            isDisabled={isRemovingManagedNfos}
                            onPress={onRemoveManagedNfosPress}
                          >
                            {translate('RemoveManagedNfosFromLibraryButton')}
                          </Button>
                        </div> :
                        null
                    }
                  </FormGroup>

                  <FormGroup
                    advancedSettings={advancedSettings}
                    isAdvanced={true}
                  >
                    <FormLabel>{translate('RescanChannelFolderAfterRefresh')}</FormLabel>

                    <FormInputGroup
                      type={inputTypes.SELECT}
                      name="rescanAfterRefresh"
                      helpText={translate('RescanAfterRefreshChannelHelpText')}
                      helpTextWarning={translate('RescanAfterRefreshHelpTextWarning')}
                      values={rescanAfterRefreshOptions}
                      onChange={onInputChange}
                      {...settings.rescanAfterRefresh}
                    />
                  </FormGroup>

                  <FormGroup
                    advancedSettings={advancedSettings}
                    isAdvanced={true}
                  >
                    <FormLabel>{translate('ChangeFileDate')}</FormLabel>

                    <FormInputGroup
                      type={inputTypes.SELECT}
                      name="fileDate"
                      helpText={translate('ChangeFileDateHelpText')}
                      values={fileDateOptions}
                      onChange={onInputChange}
                      {...settings.fileDate}
                    />
                  </FormGroup>

                  <FormGroup
                    advancedSettings={advancedSettings}
                    isAdvanced={true}
                  >
                    <FormLabel>{translate('RecyclingBin')}</FormLabel>

                    <FormInputGroup
                      type={inputTypes.PATH}
                      name="recycleBin"
                      helpText={translate('RecyclingBinHelpText')}
                      onChange={onInputChange}
                      {...settings.recycleBin}
                    />
                  </FormGroup>

                  <FormGroup
                    advancedSettings={advancedSettings}
                    isAdvanced={true}
                  >
                    <FormLabel>{translate('RecyclingBinCleanup')}</FormLabel>

                    <FormInputGroup
                      type={inputTypes.NUMBER}
                      name="recycleBinCleanupDays"
                      helpText={translate('RecyclingBinCleanupHelpText')}
                      helpTextWarning={translate('RecyclingBinCleanupHelpTextWarning')}
                      min={0}
                      onChange={onInputChange}
                      {...settings.recycleBinCleanupDays}
                    />
                  </FormGroup>
                </FieldSet>

                {
                  advancedSettings && !isWindows ?
                    <FieldSet
                      legend={translate('Permissions')}
                    >
                      <FormGroup
                        advancedSettings={advancedSettings}
                        isAdvanced={true}
                        size={sizes.MEDIUM}
                      >
                        <FormLabel>{translate('SetPermissions')}</FormLabel>

                        <FormInputGroup
                          type={inputTypes.CHECK}
                          name="setPermissionsLinux"
                          helpText={translate('SetPermissionsLinuxHelpText')}
                          helpTextWarning={translate('SetPermissionsLinuxHelpTextWarning')}
                          onChange={onInputChange}
                          {...settings.setPermissionsLinux}
                        />
                      </FormGroup>

                      <FormGroup
                        advancedSettings={advancedSettings}
                        isAdvanced={true}
                      >
                        <FormLabel>{translate('ChmodFolder')}</FormLabel>

                        <FormInputGroup
                          type={inputTypes.UMASK}
                          name="chmodFolder"
                          helpText={translate('ChmodFolderHelpText')}
                          helpTextWarning={translate('ChmodFolderHelpTextWarning')}
                          onChange={onInputChange}
                          {...settings.chmodFolder}
                        />
                      </FormGroup>

                      <FormGroup
                        advancedSettings={advancedSettings}
                        isAdvanced={true}
                      >
                        <FormLabel>{translate('ChownGroup')}</FormLabel>

                        <FormInputGroup
                          type={inputTypes.TEXT}
                          name="chownGroup"
                          helpText={translate('ChownGroupHelpText')}
                          helpTextWarning={translate('ChownGroupHelpTextWarning')}
                          values={fileDateOptions}
                          onChange={onInputChange}
                          {...settings.chownGroup}
                        />
                      </FormGroup>
                    </FieldSet> : null
                }
              </Form> : null
          }

          <FieldSet legend={translate('RootFolders')}>
            <RootFolders />
            <AddRootFolder />
          </FieldSet>
        </PageContentBody>
      </PageContent>
    );
  }

}

MediaManagement.propTypes = {
  advancedSettings: PropTypes.bool.isRequired,
  isFetching: PropTypes.bool.isRequired,
  error: PropTypes.object,
  settings: PropTypes.object.isRequired,
  plexProvider: PropTypes.object,
  hasSettings: PropTypes.bool.isRequired,
  isWindows: PropTypes.bool.isRequired,
  customNfosSavedOff: PropTypes.bool,
  isRemovingManagedNfos: PropTypes.bool,
  lastManagedNfoRemoval: PropTypes.object,
  managedNfoRemovalError: PropTypes.object,
  onRemoveManagedNfosPress: PropTypes.func,
  onClearNfoRemovalFeedback: PropTypes.func,
  onSavePress: PropTypes.func.isRequired,
  onInputChange: PropTypes.func.isRequired
};

MediaManagement.defaultProps = {
  customNfosSavedOff: false,
  isRemovingManagedNfos: false,
  lastManagedNfoRemoval: null,
  managedNfoRemovalError: null,
  onRemoveManagedNfosPress: undefined,
  onClearNfoRemovalFeedback: undefined
};

export default MediaManagement;
