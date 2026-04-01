import PropTypes from 'prop-types';
import React from 'react';
import Label from 'Components/Label';
import { kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import styles from './ImportChannelTitle.css';

function ImportChannelTitle(props) {
  const {
    title,
    year,
    network,
    isExistingChannel
  } = props;

  const yearInTitle = year > 0 && String(title).includes(String(year));
  const tooltipParts = [title];
  if (!yearInTitle && year > 0) {
    tooltipParts.push(`(${year})`);
  }
  if (network) {
    tooltipParts.push(network);
  }
  const fullLabel = tooltipParts.join(' ');

  return (
    <div
      className={styles.titleContainer}
      title={fullLabel}
    >
      <div className={styles.title}>
        {title}
      </div>

      {
        !yearInTitle &&
        year > 0 ?
          <span className={styles.year}>
            ({year})
          </span> :
          null
      }

      {
        network ?
          <Label>{network}</Label> :
          null
      }

      {
        isExistingChannel ?
          <Label
            kind={kinds.WARNING}
          >
            {translate('Existing')}
          </Label> :
          null
      }
    </div>
  );
}

ImportChannelTitle.propTypes = {
  title: PropTypes.string.isRequired,
  year: PropTypes.number.isRequired,
  network: PropTypes.string,
  isExistingChannel: PropTypes.bool.isRequired
};

export default ImportChannelTitle;
