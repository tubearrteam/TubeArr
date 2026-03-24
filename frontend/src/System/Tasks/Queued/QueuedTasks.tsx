import React, { useEffect } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import AppState from 'App/State/AppState';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import { kinds } from 'Helpers/Props';
import Command from 'Commands/Command';
import { fetchCommands } from 'Store/Actions/commandActions';
import translate from 'Utilities/String/translate';
import QueuedTaskRow from './QueuedTaskRow';

const defaultColumns = [
  {
    name: 'trigger',
    label: '',
    isVisible: true,
  },
  {
    name: 'commandName',
    label: () => translate('Name'),
    isVisible: true,
  },
  {
    name: 'queued',
    label: () => translate('Queued'),
    isVisible: true,
  },
  {
    name: 'started',
    label: () => translate('Started'),
    isVisible: true,
  },
  {
    name: 'ended',
    label: () => translate('Ended'),
    isVisible: true,
  },
  {
    name: 'duration',
    label: () => translate('Duration'),
    isVisible: true,
  },
  {
    name: 'actions',
    isVisible: true,
  },
];

interface QueuedTasksProps {
  legend?: string;
  emptyMessage?: string;
  filterPredicate?: (item: Command) => boolean;
  columns?: typeof defaultColumns;
  rowVariant?: 'default' | 'metadata';
  useFieldSet?: boolean;
}

export default function QueuedTasks({
  legend = translate('Queue'),
  emptyMessage,
  filterPredicate,
  columns = defaultColumns,
  rowVariant = 'default',
  useFieldSet = true,
}: QueuedTasksProps) {
  const dispatch = useDispatch();
  const { isFetching, isPopulated, items } = useSelector(
    (state: AppState) => state.commands
  );
  const filteredItems = filterPredicate ? items.filter(filterPredicate) : items;

  useEffect(() => {
    dispatch(fetchCommands());
  }, [dispatch]);

  const content = (
    <>
      {isFetching && !isPopulated && <LoadingIndicator />}

      {isPopulated && !!filteredItems.length && (
        <Table columns={columns}>
          <TableBody>
            {filteredItems.map((item) => {
              return (
                <QueuedTaskRow
                  key={item.id}
                  variant={rowVariant}
                  {...item}
                />
              );
            })}
          </TableBody>
        </Table>
      )}

      {isPopulated && !filteredItems.length && emptyMessage ? (
        <Alert kind={kinds.INFO}>{emptyMessage}</Alert>
      ) : null}
    </>
  );

  if (!useFieldSet) {
    return content;
  }

  return <FieldSet legend={legend}>{content}</FieldSet>;
}
