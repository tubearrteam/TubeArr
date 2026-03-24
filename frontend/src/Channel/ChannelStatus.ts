import { icons } from 'Helpers/Props';
import { ChannelStatus } from 'Channel/Channel';
import translate from 'Utilities/String/translate';

export function getChannelStatusDetails(status: ChannelStatus) {
  let statusDetails = {
    icon: icons.CHANNEL_CONTINUING,
    title: translate('Continuing'),
    message: translate('ContinuingChannelDescription'),
  };

  if (status === 'deleted') {
    statusDetails = {
      icon: icons.CHANNEL_DELETED,
      title: translate('Deleted'),
      message: translate('DeletedChannelDescription'),
    };
  } else if (status === 'ended') {
    statusDetails = {
      icon: icons.CHANNEL_ENDED,
      title: translate('Ended'),
      message: translate('EndedChannelDescription'),
    };
  } else if (status === 'upcoming') {
    statusDetails = {
      icon: icons.CHANNEL_CONTINUING,
      title: translate('Upcoming'),
      message: translate('UpcomingChannelDescription'),
    };
  }

  return statusDetails;
}
