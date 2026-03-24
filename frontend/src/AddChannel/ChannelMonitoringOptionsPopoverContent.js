import React from 'react';
import DescriptionList from 'Components/DescriptionList/DescriptionList';
import DescriptionListItem from 'Components/DescriptionList/DescriptionListItem';
import translate from 'Utilities/String/translate';

/** Descriptions for standard monitoring options (used by Add Channel, Edit, and Monitoring modals). Specials removed. */
function ChannelMonitoringOptionsPopoverContent() {
  return (
    <DescriptionList>
      <DescriptionListItem
        title={translate('MonitorAllVideos')}
        data={translate('MonitorAllVideosDescription')}
      />

      <DescriptionListItem
        title={translate('MonitorFutureVideos')}
        data={translate('MonitorFutureVideosDescription')}
      />

      <DescriptionListItem
        title={translate('MonitorMissingVideos')}
        data={translate('MonitorMissingVideosDescription')}
      />

      <DescriptionListItem
        title={translate('MonitorExistingVideos')}
        data={translate('MonitorExistingVideosDescription')}
      />

      <DescriptionListItem
        title={translate('MonitorRecentVideos')}
        data={translate('MonitorRecentVideosDescription')}
      />

      <DescriptionListItem
        title={translate('MonitorRoundRobinVideos')}
        data={translate('MonitorRoundRobinVideosDescription')}
      />

      <DescriptionListItem
        title={translate('MonitorNoVideos')}
        data={translate('MonitorNoVideosDescription')}
      />
    </DescriptionList>
  );
}

export default ChannelMonitoringOptionsPopoverContent;
