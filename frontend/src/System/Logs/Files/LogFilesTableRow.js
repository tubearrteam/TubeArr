import PropTypes from 'prop-types';
import React from 'react';
import { useSelector } from 'react-redux';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableRow from 'Components/Table/TableRow';
import createUISettingsSelector from 'Store/Selectors/createUISettingsSelector';
import formatDateTime from 'Utilities/Date/formatDateTime';
import formatLogFileListTime from 'Utilities/Date/formatLogFileListTime';
import translate from 'Utilities/String/translate';
import styles from './LogFilesTableRow.css';

function LogFilesTableRow(props) {
  const { filename, lastWriteTime, downloadUrl } = props;
  const { longDateFormat, timeFormat } = useSelector(createUISettingsSelector());

  const title = formatDateTime(lastWriteTime, longDateFormat, timeFormat, {
    includeSeconds: true,
    includeRelativeDay: true,
  });

  return (
    <TableRow>
      <TableRowCell className={styles.filenameCell}>
        {filename}
      </TableRowCell>

      <TableRowCell className={styles.lastWriteCell} title={title}>
        {formatLogFileListTime(lastWriteTime)}
      </TableRowCell>

      <TableRowCell className={styles.download}>
        <a
          className={styles.downloadLink}
          href={downloadUrl}
          target="_blank"
          rel="noreferrer"
        >
          {translate('Download')}
        </a>
      </TableRowCell>
    </TableRow>
  );
}

LogFilesTableRow.propTypes = {
  filename: PropTypes.string.isRequired,
  lastWriteTime: PropTypes.string.isRequired,
  downloadUrl: PropTypes.string.isRequired,
};

export default LogFilesTableRow;
