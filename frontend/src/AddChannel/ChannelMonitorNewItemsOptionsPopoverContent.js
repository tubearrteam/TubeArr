import React from 'react';
import DescriptionList from 'Components/DescriptionList/DescriptionList';
import DescriptionListItem from 'Components/DescriptionList/DescriptionListItem';
import translate from 'Utilities/String/translate';

/** Descriptions for Monitor New Items (all new videos vs none). Used in Edit Channel and Import List modals. */
function ChannelMonitorNewItemsOptionsPopoverContent() {
  return (
    <DescriptionList>
      <DescriptionListItem
        title={translate('MonitorAllNewVideos')}
        data={translate('MonitorAllNewVideosDescription')}
      />

      <DescriptionListItem
        title={translate('MonitorNoNewVideos')}
        data={translate('MonitorNoNewVideosDescription')}
      />
    </DescriptionList>
  );
}

export default ChannelMonitorNewItemsOptionsPopoverContent;
