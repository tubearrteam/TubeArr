import React from 'react';
import Link from 'Components/Link/Link';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import translate from 'Utilities/String/translate';
import SettingsToolbarConnector from 'Settings/SettingsToolbarConnector';
import styles from 'Settings/Settings.css';

function ToolsSettings() {
  return (
    <PageContent title={translate('Tools')}>
      <SettingsToolbarConnector
        hasPendingChanges={false}
      />

      <PageContentBody>
        <Link
          className={styles.link}
          to="/settings/tools/ytdlp"
        >
          {translate('YtDlp')}
        </Link>

        <div className={styles.summary}>
          {translate('YtDlpSettingsDescription')}
        </div>

        <Link
          className={styles.link}
          to="/settings/tools/ffmpeg"
        >
          {translate('FFmpeg')}
        </Link>

        <div className={styles.summary}>
          {translate('FFmpegSettingsSummary')}
        </div>

        <Link
          className={styles.link}
          to="/settings/tools/slskd"
        >
          {translate('Slskd')}
        </Link>

        <div className={styles.summary}>
          {translate('SlskdSettingsDescription')}
        </div>
      </PageContentBody>
    </PageContent>
  );
}

ToolsSettings.propTypes = {};

export default ToolsSettings;
