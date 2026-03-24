import React from 'react';
import Link from 'Components/Link/Link';
import translate from 'Utilities/String/translate';
import styles from './MetadataAttribution.css';

export default function MetadataAttribution() {
  return (
    <div className={styles.container}>
      <div className={styles.attribution}>
        {translate('MetadataProvidedBy', { provider: translate('YouTubeDataApi') })}{' '}
        <Link to="https://developers.google.com/youtube/v3">
          {translate('Documentation')}
        </Link>
      </div>
    </div>
  );
}
