import React from 'react';
import Label from 'Components/Label';
import Link from 'Components/Link/Link';
import { kinds, sizes } from 'Helpers/Props';
import Channel from 'Channel/Channel';
import styles from './ChannelDetailsLinks.css';

type ChannelDetailsLinksProps = Pick<
  Channel,
  'youtubeChannelId'
>;

function ChannelDetailsLinks(props: ChannelDetailsLinksProps) {
  const { youtubeChannelId } = props;

  if (!youtubeChannelId) {
    return null;
  }

  return (
    <div className={styles.links}>
      <Link
        className={styles.link}
        to={`https://www.youtube.com/channel/${youtubeChannelId}`}
      >
        <Label
          className={styles.linkLabel}
          kind={kinds.INFO}
          size={sizes.LARGE}
        >
          YouTube
        </Label>
      </Link>
    </div>
  );
}

export default ChannelDetailsLinks;
