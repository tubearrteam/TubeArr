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
  filterYtDlpAssets,
  getClientBinaryPlatform
} from 'Utilities/BinaryReleaseAssets';
import styles from './YtDlpSettings.css';

const YT_DLP_LOGO_URL = 'https://upload.wikimedia.org/wikipedia/commons/3/38/Yt-dlp_logo.svg';

class YtDlpSettings extends Component {

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
      isUpdating,
      updateMessage,
      updateSuccess,
      onInputChange,
      onSavePress,
      onTestPress,
      onFetchReleases,
      onDownloadSelectionChange,
      onDownloadPress,
      onUpdatePress,
      onExportCookiesPress,
      isExportingCookies,
      exportCookiesMessage,
      ...otherProps
    } = this.props;

    const selectedRelease = releases.find((r) => r.tag_name === selectedReleaseTag) || null;
    const assets = selectedRelease?.assets || [];
    const platformHint = getClientBinaryPlatform();
    const compatibleAssets = filterYtDlpAssets(assets);
    const displayAssets = this.state.showAllBinaryAssets ? assets : compatibleAssets;
    const canDownload = selectedAsset?.browser_download_url && !isDownloading;
    const executablePath = settings?.executablePath?.value ?? this.props.item?.executablePath ?? '';
    const canUpdate = !!executablePath && !isUpdating;

    const cookieExportBrowserOptions = [
      { key: 'chrome', value: translate('YtDlpBrowserChrome') },
      { key: 'edge', value: translate('YtDlpBrowserEdge') },
      { key: 'chromium', value: translate('YtDlpBrowserChromium') }
    ];

    return (
      <PageContent title={translate('YtDlp')}>
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
                {translate('YtDlpSettingsLoadError')}
              </Alert> :
              null
          }

          {
            hasSettings && !isFetching && !error ?
              <>
                <header className={styles.header}>
                  <img
                    className={styles.logo}
                    src={YT_DLP_LOGO_URL}
                    alt={translate('YtDlpLogoAlt')}
                  />
                  <div className={styles.headerText}>
                    <h1 className={styles.title}>{translate('YtDlp')}</h1>
                    <p className={styles.description}>
                      {translate('YtDlpSettingsDescription')}
                    </p>
                  </div>
                </header>

                <Form
                  id="ytdlpSettings"
                  {...otherProps}
                >
                  <FieldSet legend={translate('YtDlpDownloadBinary')}>
                    <FormGroup>
                      <FormLabel>{translate('YtDlpReleases')}</FormLabel>
                      <div className={styles.downloadRow}>
                        <Button
                          kind={kinds.DEFAULT}
                          onPress={onFetchReleases}
                          isDisabled={isFetchingReleases}
                          isSpinning={isFetchingReleases}
                        >
                          {isFetchingReleases ? translate('Loading') : translate('YtDlpFetchReleases')}
                        </Button>
                        {releasesError && <span className={styles.releasesError}>{releasesError}</span>}
                      </div>
                    </FormGroup>
                    {releases.length > 0 && (
                      <>
                        <FormGroup>
                          <FormLabel>{translate('YtDlpRelease')}</FormLabel>
                          <SelectInput
                            name="selectedReleaseTag"
                            value={selectedReleaseTag || ''}
                            values={[
                              { key: '', value: translate('YtDlpSelectRelease') },
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
                              {translate('BinaryAssetsCompatibleWith', { platform: platformHint.label })}
                            </p>
                            <FormGroup>
                              <FormLabel>{translate('YtDlpBinaryAsset')}</FormLabel>
                              <SelectInput
                                name="selectedAsset"
                                value={selectedAsset ? selectedAsset.browser_download_url : ''}
                                values={[
                                  { key: '', value: translate('YtDlpSelectBinary') },
                                  ...displayAssets.map((a) => ({
                                    key: a.browser_download_url,
                                    value: `${a.name}${a.size ? ` (${Math.round(a.size / 1024)} KB)` : ''}`
                                  }))
                                ]}
                                onChange={({ name, value }) => {
                                  const pool = this.state.showAllBinaryAssets ? assets : compatibleAssets;
                                  const asset = value ? pool.find((a) => a.browser_download_url === value) || null : null;
                                  onDownloadSelectionChange({ selectedAsset: asset });
                                }}
                              />
                            </FormGroup>
                            {!this.state.showAllBinaryAssets && compatibleAssets.length === 0 && (
                              <p className={styles.releasesError}>{translate('BinaryAssetsNoCompatibleMatch')}</p>
                            )}
                            <div className={styles.seeAllRow}>
                              <Link
                                className={styles.seeAllLink}
                                onPress={() => {
                                  const nextShowAll = !this.state.showAllBinaryAssets;
                                  if (!nextShowAll && selectedAsset) {
                                    const filtered = filterYtDlpAssets(assets);
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

                  <FieldSet legend={translate('YtDlpSectionCookies')}>
                    <FormGroup>
                      <FormLabel>{translate('YtDlpCookiesPath')}</FormLabel>
                      <FormInputGroup
                        key={`ytdlp-cookies-${settings.cookiesPath?.value ?? ''}`}
                        type={inputTypes.PATH}
                        name="cookiesPath"
                        placeholder={translate('YtDlpCookiesPathPlaceholder')}
                        helpText={translate('YtDlpCookiesPathHelpText')}
                        onChange={onInputChange}
                        {...settings.cookiesPath}
                      />
                    </FormGroup>

                    <FormGroup>
                      <FormLabel>{translate('YtDlpCookieExportBrowser')}</FormLabel>
                      <FormInputGroup
                        type={inputTypes.SELECT}
                        name="cookiesExportBrowser"
                        values={cookieExportBrowserOptions}
                        helpText={translate('YtDlpCookieExportBrowserHelpText')}
                        onChange={onInputChange}
                        {...(settings.cookiesExportBrowser || { value: 'chrome' })}
                      />
                    </FormGroup>

                    <FormGroup>
                      <FormLabel>{translate('YtDlpExportBrowserCookies')}</FormLabel>
                      <div>
                        <Button
                          kind={kinds.DEFAULT}
                          onPress={onExportCookiesPress}
                          isDisabled={isExportingCookies || isSaving}
                          isSpinning={isExportingCookies}
                        >
                          {isExportingCookies ? translate('Exporting') : translate('YtDlpExportBrowserCookies')}
                        </Button>
                        {exportCookiesMessage != null && !isExportingCookies && (
                          <div
                            className={
                              exportCookiesMessage.includes('Successfully')
                                ? `${styles.testResult} ${styles.testResultSuccess}`
                                : `${styles.testResult} ${styles.testResultFailure}`
                            }
                          >
                            {exportCookiesMessage}
                          </div>
                        )}
                      </div>
                    </FormGroup>
                  </FieldSet>

                  <FieldSet legend={translate('YtDlpUpdate')}>
                    <FormGroup>
                      <FormLabel>{translate('YtDlpUpdateDescription')}</FormLabel>
                      <div>
                        <Button
                          kind={kinds.DEFAULT}
                          onPress={onUpdatePress}
                          isDisabled={!canUpdate}
                          isSpinning={isUpdating}
                        >
                          {isUpdating ? translate('Updating') : translate('YtDlpUpdateButton')}
                        </Button>
                        {updateMessage != null && !isUpdating && (
                          <div
                            className={
                              updateSuccess
                                ? `${styles.testResult} ${styles.testResultSuccess}`
                                : `${styles.testResult} ${styles.testResultFailure}`
                            }
                          >
                            {updateMessage}
                          </div>
                        )}
                      </div>
                    </FormGroup>
                  </FieldSet>

                  <FieldSet legend={translate('Path')}>
                    <FormGroup>
                      <FormLabel>{translate('ExecutablePath')}</FormLabel>
                      <FormInputGroup
                        type={inputTypes.PATH}
                        name="executablePath"
                        placeholder={translate('YtDlpExecutablePathPlaceholder')}
                        helpText={translate('YtDlpExecutablePathHelpText')}
                        onChange={onInputChange}
                        {...settings.executablePath}
                      />
                    </FormGroup>
                  </FieldSet>

                  <FieldSet legend={translate('YtDlpDownloadQueue')}>
                    <div className={styles.downloadQueueWarning}>
                      <Alert kind={kinds.WARNING}>
                        {translate('YtDlpSimultaneousDownloadsWarning')}
                      </Alert>
                    </div>
                    <FormGroup>
                      <FormLabel>{translate('YtDlpSimultaneousDownloads')}</FormLabel>
                      <FormInputGroup
                        type={inputTypes.NUMBER}
                        name="downloadQueueParallelWorkers"
                        min={1}
                        max={10}
                        helpText={translate('YtDlpSimultaneousDownloadsHelpText')}
                        onChange={onInputChange}
                        {...(settings.downloadQueueParallelWorkers || { value: 1 })}
                      />
                    </FormGroup>
                    <FormGroup>
                      <FormLabel>{translate('YtDlpRetryMaxAttempts')}</FormLabel>
                      <FormInputGroup
                        type={inputTypes.NUMBER}
                        name="downloadTransientMaxRetries"
                        min={0}
                        max={10}
                        helpText={translate('YtDlpRetryMaxAttemptsHelpText')}
                        onChange={onInputChange}
                        {...(settings.downloadTransientMaxRetries || { value: 3 })}
                      />
                    </FormGroup>
                    <FormGroup>
                      <FormLabel>{translate('YtDlpRetryDelaysJson')}</FormLabel>
                      <FormInputGroup
                        type={inputTypes.TEXT}
                        name="downloadRetryDelaysSecondsJson"
                        placeholder="[30,60,120]"
                        helpText={translate('YtDlpRetryDelaysJsonHelpText')}
                        onChange={onInputChange}
                        {...(settings.downloadRetryDelaysSecondsJson || { value: '[30,60,120]' })}
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

YtDlpSettings.propTypes = {
  isFetching: PropTypes.bool.isRequired,
  error: PropTypes.object,
  settings: PropTypes.object.isRequired,
  hasSettings: PropTypes.bool.isRequired,
  isSaving: PropTypes.bool.isRequired,
  isTesting: PropTypes.bool.isRequired,
  testMessage: PropTypes.string,
  testSuccess: PropTypes.bool,
  item: PropTypes.object,
  releases: PropTypes.array.isRequired,
  isFetchingReleases: PropTypes.bool.isRequired,
  releasesError: PropTypes.string,
  selectedReleaseTag: PropTypes.string,
  selectedAsset: PropTypes.object,
  isDownloading: PropTypes.bool.isRequired,
  downloadError: PropTypes.string,
  downloadSuccess: PropTypes.string,
  isUpdating: PropTypes.bool.isRequired,
  updateMessage: PropTypes.string,
  updateSuccess: PropTypes.bool,
  onInputChange: PropTypes.func.isRequired,
  onSavePress: PropTypes.func.isRequired,
  onTestPress: PropTypes.func.isRequired,
  onFetchReleases: PropTypes.func.isRequired,
  onDownloadSelectionChange: PropTypes.func.isRequired,
  onDownloadPress: PropTypes.func.isRequired,
  onUpdatePress: PropTypes.func.isRequired
};

export default YtDlpSettings;
