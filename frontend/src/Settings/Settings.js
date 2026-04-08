import React from 'react';
import Link from 'Components/Link/Link';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import translate from 'Utilities/String/translate';
import SettingsToolbarConnector from './SettingsToolbarConnector';
import styles from './Settings.css';

function Settings() {
  return (
    <PageContent title={translate('Settings')}>
      <SettingsToolbarConnector
        hasPendingChanges={false}
      />

      <PageContentBody>
        <Link
          className={styles.link}
          to="/settings/mediamanagement"
        >
          {translate('MediaManagement')}
        </Link>

        <div className={styles.summary}>
          {translate('MediaManagementSettingsSummary')}
        </div>

        <Link
          className={styles.link}
          to="/settings/profiles"
        >
          {translate('Profiles')}
        </Link>

        <div className={styles.summary}>
          {translate('ProfilesSettingsSummary')}
        </div>

        {/* Quality, CustomFormats: to enable, uncomment this block + route + import in App/AppRoutes.tsx + sidebar children in PageSidebar.js */}
        {/* <Link
          className={styles.link}
          to="/settings/quality"
        >
          {translate('Quality')}
        </Link>

        <div className={styles.summary}>
          {translate('QualitySettingsSummary')}
        </div>

        <Link
          className={styles.link}
          to="/settings/customformats"
        >
          {translate('CustomFormats')}
        </Link>

        <div className={styles.summary}>
          {translate('CustomFormatsSettingsSummary')}
        </div> */}

        <Link
          className={styles.link}
          to="/settings/connect"
        >
          {translate('Connect')}
        </Link>

        <div className={styles.summary}>
          {translate('ConnectSettingsSummary')}
        </div>

        {/* Metadata, MetadataSource: to enable, uncomment this block + route + import in App/AppRoutes.tsx + sidebar children in PageSidebar.js */}
        {/* <Link
          className={styles.link}
          to="/settings/metadata"
        >
          {translate('Metadata')}
        </Link>

        <div className={styles.summary}>
          {translate('MetadataSettingsChannelSummary')}
        </div>

        <Link
          className={styles.link}
          to="/settings/metadatasource"
        >
          {translate('MetadataSource')}
        </Link>

        <div className={styles.summary}>
          {translate('MetadataSourceSettingsChannelSummary')}
        </div> */}

        <Link
          className={styles.link}
          to="/settings/youtube"
        >
          {translate('YouTube')}
        </Link>

        <div className={styles.summary}>
          {translate('YouTubeSettingsSummary')}
        </div>

        <Link
          className={styles.link}
          to="/settings/tools"
        >
          {translate('Tools')}
        </Link>

        <div className={styles.summary}>
          {translate('ToolsSettingsSummary')}
        </div>

        <Link
          className={styles.link}
          to="/settings/tags"
        >
          {translate('Tags')}
        </Link>

        <div className={styles.summary}>
          {translate('TagsSettingsSummary')}
        </div>

        <Link
          className={styles.link}
          to="/settings/general"
        >
          {translate('General')}
        </Link>

        <div className={styles.summary}>
          {translate('GeneralSettingsSummary')}
        </div>

        <Link
          className={styles.link}
          to="/settings/ui"
        >
          {translate('Ui')}
        </Link>

        <div className={styles.summary}>
          {translate('UiSettingsSummary')}
        </div>
      </PageContentBody>
    </PageContent>
  );
}

Settings.propTypes = {
};

export default Settings;
