import React from 'react';
import { useSelector } from 'react-redux';
import { CommandBody } from 'Commands/Command';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import createMultiChannelSelector from 'Store/Selectors/createMultiChannelSelector';
import sortByProp from 'Utilities/Array/sortByProp';
import translate from 'Utilities/String/translate';
import QueuedTaskRowMetadataProgress from './QueuedTaskRowMetadataProgress';
import styles from './QueuedTaskRowNameCell.css';

function formatTitles(titles: string[]) {
  if (!titles) {
    return null;
  }

  if (titles.length > 11) {
    return (
      <span title={titles.join(', ')}>
        {titles.slice(0, 10).join(', ')}, {titles.length - 10} more
      </span>
    );
  }

  return <span>{titles.join(', ')}</span>;
}

export interface QueuedTaskRowNameCellProps {
  commandName: string;
  body: CommandBody;
  clientUserAgent?: string;
  className?: string;
}

export default function QueuedTaskRowNameCell(
  props: QueuedTaskRowNameCellProps
) {
  const { commandName, body, clientUserAgent, className } = props;
  const channelIds = [...(body.channelIds ?? [])];

  if (body.channelId) {
    channelIds.push(body.channelId);
  }

  const channels = useSelector(createMultiChannelSelector(channelIds));
  const sortedChannels = channels.sort(sortByProp('sortTitle'));

  return (
    <TableRowCell className={className}>
      <span className={styles.commandName}>
        {commandName}
        {sortedChannels.length ? (
          <span> - {formatTitles(sortedChannels.map((c) => c.title))}</span>
        ) : null}
        {body.playlistNumber ? (
          <span>
            {' '}
            {translate('PlaylistNumberToken', {
              playlistNumber: body.playlistNumber,
            })}
          </span>
        ) : null}
      </span>

      {clientUserAgent ? (
        <span
          className={styles.userAgent}
          title={translate('TaskUserAgentTooltip')}
        >
          {translate('From')}: {clientUserAgent}
        </span>
      ) : null}

      {body.metadataProgress ? (
        <QueuedTaskRowMetadataProgress metadataProgress={body.metadataProgress} />
      ) : null}
    </TableRowCell>
  );
}
