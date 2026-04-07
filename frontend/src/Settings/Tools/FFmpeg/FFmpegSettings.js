import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Alert from 'Components/Alert';
import Button from 'Components/Link/Button';
import Link from 'Components/Link/Link';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import SelectInput from 'Components/Form/SelectInput';
import { inputTypes, kinds } from 'Helpers/Props';
import SettingsToolbarConnector from 'Settings/SettingsToolbarConnector';
import translate from 'Utilities/String/translate';
import {
  buildHostBinaryPlatform,
  filterFfmpegAssets
} from 'Utilities/BinaryReleaseAssets';
import styles from './FFmpegSettings.css';

const FFMPEG_LOGO_URL = 'https://upload.wikimedia.org/wikipedia/commons/1/1e/FFmpeg_Latest_logo.svg';

class FFmpegSettings extends Component {

  state = {
    showAllBinaryAssets: false
  };

  componentDidUpdate(prevProps) {
    if (prevProps.selectedReleaseTag !== this.props.selectedReleaseTag) {
      this.setState({ showAllBinaryAssets: false });
    }
  }

  render() {
    const {
      isFetching,
      error,
      settings,
      hasSettings,
      isSaving,
      isTesting,
      testMessage,
      testSuccess,
      releases,
      isFetchingReleases,
      releasesError,
      selectedReleaseTag,
      selectedAsset,
      isDownloading,
      downloadError,
      downloadSuccess,
      onInputChange,
      onSavePress,
      onTestPress,
      onFetchReleases,
      onDownloadSelectionChange,
      onDownloadPress,
      hostBinaryPlatform,
      ...otherProps
    } = this.props;

    const selectedRelease = releases.find((r) => r.tag_name === selectedReleaseTag) || null;
    const assets = selectedRelease?.assets || [];
    const platform = hostBinaryPlatform ?? buildHostBinaryPlatform(null);
    const compatibleAssets = filterFfmpegAssets(assets, platform);
    const displayAssets = this.state.showAllBinaryAssets ? assets : compatibleAssets;
    const canDownload = selectedAsset?.browser_download_url && !isDownloading;

    return (
      <PageContent title={translate('FFmpeg')}>
        <SettingsToolbarConnector
          {...otherProps}
          onSavePress={onSavePress}
        />

        <PageContentBody>
          {
            isFetching ?
              <LoadingIndicator /> :
              null
          }

          {
            !isFetching && error ?
              <Alert kind={kinds.DANGER}>
                {translate('FFmpegSettingsLoadError')}
              </Alert> :
              null
          }

          {
            hasSettings && !isFetching && !error ?
              <>
                <header className={styles.header}>
                  <img
                    className={styles.logo}
                    src={FFMPEG_LOGO_URL}
                    alt={translate('FFmpegLogoAlt')}
                  />
                  <div className={styles.headerText}>
                    <h1 className={styles.title}>{translate('FFmpeg')}</h1>
                    <p className={styles.description}>
                      {translate('FFmpegSettingsDescription')}
                    </p>
                  </div>
                </header>

                <Form
                  id="ffmpegSettings"
                  {...otherProps}
                >
                  <FieldSet legend={translate('FFmpegDownloadBinary')}>
                    <FormGroup>
                      <FormLabel>{translate('FFmpegReleases')}</FormLabel>
                      <div className={styles.downloadRow}>
                        <Button
                          kind={kinds.DEFAULT}
                          onPress={onFetchReleases}
                          isDisabled={isFetchingReleases}
                          isSpinning={isFetchingReleases}
                        >
                          {isFetchingReleases ? translate('Loading') : translate('FFmpegFetchReleases')}
                        </Button>
                        {releasesError && <span className={styles.releasesError}>{releasesError}</span>}
                      </div>
                    </FormGroup>
                    {releases.length > 0 && (
                      <>
                        <FormGroup>
                          <FormLabel>{translate('FFmpegRelease')}</FormLabel>
                          <SelectInput
                            name="selectedReleaseTag"
                            value={selectedReleaseTag || ''}
                            values={[
                              { key: '', value: translate('FFmpegSelectRelease') },
                              ...releases.map((r) => ({ key: r.tag_name, value: `${r.tag_name} (${r.name})` }))
                            ]}
                            onChange={({ name, value }) => {
                              onDownloadSelectionChange({ selectedReleaseTag: value || null, selectedAsset: null });
                            }}
                          />
                        </FormGroup>
                        {assets.length > 0 && (
                          <>
                            <p className={styles.platformHint}>
                              {translate('BinaryAssetsCompatibleWith', { platform: platform.label })}
                            </p>
                            {!this.state.showAllBinaryAssets && compatibleAssets.length === 0 && (
                              <Alert kind={kinds.WARNING}>
                                {translate('FfmpegBinaryAssetsNoOsMatch')}
                              </Alert>
                            )}
                            <FormGroup>
                              <FormLabel>{translate('FFmpegBinaryAsset')}</FormLabel>
                              <SelectInput
                                name="selectedAsset"
                                value={selectedAsset ? selectedAsset.browser_download_url : ''}
                                values={[
                                  { key: '', value: translate('FFmpegSelectBinary') },
                                  ...displayAssets.map((a) => ({
                                    key: a.browser_download_url,
                                    value: `${a.name}${a.size ? ` (${Math.round(a.size / 1024 / 1024)} MB)` : ''}`
                                  }))
                                ]}
                                onChange={({ name, value }) => {
                                  const pool = this.state.showAllBinaryAssets ? assets : compatibleAssets;
                                  const asset = value ? pool.find((a) => a.browser_download_url === value) || null : null;
                                  onDownloadSelectionChange({ selectedAsset: asset });
                                }}
                              />
                            </FormGroup>
                            <div className={styles.seeAllRow}>
                              <Link
                                className={styles.seeAllLink}
                                onPress={() => {
                                  const nextShowAll = !this.state.showAllBinaryAssets;
                                  if (!nextShowAll && selectedAsset) {
                                    const filtered = filterFfmpegAssets(assets, platform);
                                    const ok = filtered.some((x) => x.browser_download_url === selectedAsset.browser_download_url);
                                    if (!ok) {
                                      onDownloadSelectionChange({ selectedAsset: null });
                                    }
                                  }
                                  this.setState({ showAllBinaryAssets: nextShowAll });
                                }}
                              >
                                {this.state.showAllBinaryAssets
                                  ? translate('BinaryAssetsShowCompatibleOnly')
                                  : translate('BinaryAssetsSeeAll')}
                              </Link>
                            </div>
                          </>
                        )}
                        <FormGroup>
                          <Button
                            kind={kinds.PRIMARY}
                            onPress={onDownloadPress}
                            isDisabled={!canDownload}
                            isSpinning={isDownloading}
                          >
                            {isDownloading ? translate('Downloading') : translate('Download')}
                          </Button>
                          {downloadError && <div className={styles.downloadError}>{downloadError}</div>}
                          {downloadSuccess && <div className={styles.downloadSuccess}>{downloadSuccess}</div>}
                        </FormGroup>
                        <FormGroup>
                          <FormLabel>{translate('Test')}</FormLabel>
                          <div>
                            <Button
                              kind={kinds.DEFAULT}
                              onPress={onTestPress}
                              isDisabled={isTesting || isSaving}
                              isSpinning={isTesting}
                            >
                              {isTesting ? translate('Testing') : translate('TestConnection')}
                            </Button>
                            {testMessage != null && !isTesting && (
                              <div
                                className={
                                  testSuccess
                                    ? `${styles.testResult} ${styles.testResultSuccess}`
                                    : `${styles.testResult} ${styles.testResultFailure}`
                                }
                              >
                                {testSuccess ? translate('Success') : translate('Error')}: {testMessage}
                              </div>
                            )}
                          </div>
                        </FormGroup>
                      </>
                    )}
                  </FieldSet>

                  <FieldSet legend={translate('Path')}>
                    <FormGroup>
                      <FormLabel>{translate('ExecutablePath')}</FormLabel>
                      <FormInputGroup
                        type={inputTypes.PATH}
                        name="executablePath"
                        placeholder={translate('FFmpegExecutablePathPlaceholder')}
                        helpText={translate('FFmpegExecutablePathHelpText')}
                        onChange={onInputChange}
                        {...settings.executablePath}
                      />
                    </FormGroup>
                  </FieldSet>
                </Form>
              </>
              : null
          }
        </PageContentBody>
      </PageContent>
    );
  }
}

FFmpegSettings.propTypes = {
  hostBinaryPlatform: PropTypes.shape({
    os: PropTypes.string.isRequired,
    arch: PropTypes.string.isRequired,
    label: PropTypes.string.isRequired
  }),
  isFetching: PropTypes.bool.isRequired,
  error: PropTypes.object,
  settings: PropTypes.object.isRequired,
  hasSettings: PropTypes.bool.isRequired,
  isSaving: PropTypes.bool.isRequired,
  isTesting: PropTypes.bool.isRequired,
  testMessage: PropTypes.string,
  testSuccess: PropTypes.bool,
  releases: PropTypes.array.isRequired,
  isFetchingReleases: PropTypes.bool.isRequired,
  releasesError: PropTypes.string,
  selectedReleaseTag: PropTypes.string,
  selectedAsset: PropTypes.object,
  isDownloading: PropTypes.bool.isRequired,
  downloadError: PropTypes.string,
  downloadSuccess: PropTypes.string,
  onInputChange: PropTypes.func.isRequired,
  onSavePress: PropTypes.func.isRequired,
  onTestPress: PropTypes.func.isRequired,
  onFetchReleases: PropTypes.func.isRequired,
  onDownloadSelectionChange: PropTypes.func.isRequired,
  onDownloadPress: PropTypes.func.isRequired
};

export default FFmpegSettings;
