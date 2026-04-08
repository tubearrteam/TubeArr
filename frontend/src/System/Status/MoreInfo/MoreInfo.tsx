import React from 'react';
import DescriptionList from 'Components/DescriptionList/DescriptionList';
import DescriptionListItemDescription from 'Components/DescriptionList/DescriptionListItemDescription';
import DescriptionListItemTitle from 'Components/DescriptionList/DescriptionListItemTitle';
import FieldSet from 'Components/FieldSet';
import Link from 'Components/Link/Link';
import getPathWithUrlBase from 'Utilities/getPathWithUrlBase';
import translate from 'Utilities/String/translate';

function MoreInfo() {
  return (
    <FieldSet legend={translate('MoreInfo')}>
      <DescriptionList>
        <DescriptionListItemTitle>
          {translate('PlexMatchDebug')}
        </DescriptionListItemTitle>
        <DescriptionListItemDescription>
          <Link to={getPathWithUrlBase('/system/plex-debug')}>
            {translate('PlexMatchDebugBlurb')}
          </Link>
        </DescriptionListItemDescription>

        <DescriptionListItemTitle>
          {translate('YouTubeDataApi')}
        </DescriptionListItemTitle>
        <DescriptionListItemDescription>
          <Link to="https://developers.google.com/youtube/v3">
            developers.google.com/youtube/v3
          </Link>
        </DescriptionListItemDescription>
      </DescriptionList>
    </FieldSet>
  );
}

export default MoreInfo;
