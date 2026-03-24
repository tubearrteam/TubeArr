import React from 'react';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import SettingsToolbarConnector from 'Settings/SettingsToolbarConnector';
import translate from 'Utilities/String/translate';

function MetadataSourceSettings() {
  return (
    <PageContent title={translate('MetadataSourceSettings')} >
      <SettingsToolbarConnector
        showSave={false}
      />

      <PageContentBody>
        <div>
          TubeArr uses YouTube as its only metadata provider.
        </div>
      </PageContentBody>
    </PageContent>
  );
}

export default MetadataSourceSettings;
