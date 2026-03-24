import React from 'react';
import DescriptionList from 'Components/DescriptionList/DescriptionList';
import DescriptionListItem from 'Components/DescriptionList/DescriptionListItem';
import translate from 'Utilities/String/translate';

function ChannelTypePopoverContent() {
  return (
    <DescriptionList>
      <DescriptionListItem
        title={translate('Standard')}
        data={translate('StandardVideoTypeDescription')}
      />

      <DescriptionListItem
        title={translate('Episodic')}
        data={translate('EpisodicVideoTypeDescription')}
      />

      <DescriptionListItem
        title={translate('Daily')}
        data={translate('DailyVideoTypeDescription')}
      />

      <DescriptionListItem
        title={translate('Streaming')}
        data={translate('StreamingVideoTypeDescription')}
      />
    </DescriptionList>
  );
}

export default ChannelTypePopoverContent;
