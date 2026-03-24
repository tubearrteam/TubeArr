import PropTypes from 'prop-types';
import React from 'react';
import Label from 'Components/Label';
import { kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import ChannelPoster from 'Channel/ChannelPoster';
import styles from './ChannelSearchResult.css';

function ChannelSearchResult(props) {
  const {
    match,
    title,
    images,
    alternateTitles,
    youtubeChannelId,
    tags
  } = props;

  let alternateTitle = null;
  let tag = null;

  if (match.key === 'alternateTitles.title') {
    alternateTitle = alternateTitles[match.refIndex];
  } else if (match.key === 'tags.label') {
    tag = tags[match.refIndex];
  }

  return (
    <div className={styles.result}>
      <ChannelPoster
        className={styles.poster}
        images={images}
        size={250}
        lazy={false}
        overflow={true}
      />

      <div className={styles.titles}>
        <div className={styles.title}>
          {title}
        </div>

        {
          alternateTitle ?
            <div className={styles.alternateTitle}>
              {alternateTitle.title}
            </div> :
            null
        }

        {
          match.key === 'youtubeChannelId' && youtubeChannelId ?
            <div className={styles.alternateTitle}>
              {translate('YouTubeChannelId')}: {youtubeChannelId}
            </div> :
            null
        }

        {
          tag ?
            <div className={styles.tagContainer}>
              <Label
                key={tag.id}
                kind={kinds.INFO}
              >
                {tag.label}
              </Label>
            </div> :
            null
        }
      </div>
    </div>
  );
}

ChannelSearchResult.propTypes = {
  title: PropTypes.string.isRequired,
  images: PropTypes.arrayOf(PropTypes.object).isRequired,
  alternateTitles: PropTypes.arrayOf(PropTypes.object).isRequired,
  youtubeChannelId: PropTypes.string,
  tags: PropTypes.arrayOf(PropTypes.object).isRequired,
  match: PropTypes.object.isRequired
};

export default ChannelSearchResult;
