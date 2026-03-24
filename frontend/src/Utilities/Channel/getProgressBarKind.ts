import { kinds } from 'Helpers/Props';
import { ChannelStatus } from 'Channel/Channel';

function getProgressBarKind(
  status: ChannelStatus,
  monitored: boolean,
  progress: number,
  isDownloading: boolean
) {
  if (isDownloading) {
    return kinds.PURPLE;
  }

  if (progress === 100) {
    return status === 'ended' ? kinds.SUCCESS : kinds.PRIMARY;
  }

  if (monitored) {
    return kinds.DANGER;
  }

  return kinds.WARNING;
}

export default getProgressBarKind;
