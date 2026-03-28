import classNames from 'classnames';
import React, { useCallback } from 'react';
import { useDispatch } from 'react-redux';
import { useSelect } from 'App/SelectContext';
import IconButton from 'Components/Link/IconButton';
import Column from 'Components/Table/Column';
import TableOptionsModalWrapper from 'Components/Table/TableOptions/TableOptionsModalWrapper';
import VirtualTableHeader from 'Components/Table/VirtualTableHeader';
import VirtualTableHeaderCell from 'Components/Table/VirtualTableHeaderCell';
import VirtualTableSelectAllHeaderCell from 'Components/Table/VirtualTableSelectAllHeaderCell';
import { icons } from 'Helpers/Props';
import { SortDirection } from 'Helpers/Props/sortDirections';
import {
  setChannelSort,
  setChannelTableOption,
} from 'Store/Actions/channelIndexActions';
import { CheckInputChanged } from 'typings/inputs';
import hasGrowableColumns from './hasGrowableColumns';
import ChannelIndexTableOptions from './ChannelIndexTableOptions';
import styles from './ChannelIndexTableHeader.css';

interface ChannelIndexTableHeaderProps {
  showBanners: boolean;
  columns: Column[];
  sortKey?: string;
  sortDirection?: SortDirection;
  isSelectMode: boolean;
}

function ChannelIndexTableHeader(props: ChannelIndexTableHeaderProps) {
  const { showBanners, columns, sortKey, sortDirection, isSelectMode } = props;
  const dispatch = useDispatch();
  const [selectState, selectDispatch] = useSelect();

  const onSortPress = useCallback(
    (value: string) => {
      dispatch(setChannelSort({ sortKey: value }));
    },
    [dispatch]
  );

  const onTableOptionChange = useCallback(
    (payload: unknown) => {
      dispatch(setChannelTableOption(payload));
    },
    [dispatch]
  );

  const onSelectAllChange = useCallback(
    ({ value }: CheckInputChanged) => {
      selectDispatch({
        type: value ? 'selectAll' : 'unselectAll',
      });
    },
    [selectDispatch]
  );

  return (
    <VirtualTableHeader>
      {isSelectMode ? (
        <VirtualTableSelectAllHeaderCell
          allSelected={selectState.allSelected}
          allUnselected={selectState.allUnselected}
          onSelectAllChange={onSelectAllChange}
        />
      ) : null}

      {columns.map((column) => {
        if (!column) {
          return null;
        }

        const { name, label, isSortable, isVisible } = column;

        if (!isVisible) {
          return null;
        }

        if (name === 'actions') {
          return (
            <VirtualTableHeaderCell
              key={name}
              className={styles[name]}
              name={name}
              isSortable={false}
            >
              <TableOptionsModalWrapper
                columns={columns}
                optionsComponent={ChannelIndexTableOptions}
                onTableOptionChange={onTableOptionChange}
              >
                <IconButton name={icons.ADVANCED_SETTINGS} />
              </TableOptionsModalWrapper>
            </VirtualTableHeaderCell>
          );
        }

        return (
          <VirtualTableHeaderCell
            key={name}
            className={classNames(
              // eslint-disable-next-line @typescript-eslint/ban-ts-comment
              // @ts-ignore
              styles[name],
              name === 'sortTitle' && showBanners && styles.banner,
              name === 'sortTitle' &&
                showBanners &&
                !hasGrowableColumns(columns) &&
                styles.bannerGrow
            )}
            name={name}
            sortKey={sortKey}
            sortDirection={sortDirection}
            isSortable={isSortable}
            onSortPress={onSortPress}
          >
            {typeof label === 'function' ? label() : label}
          </VirtualTableHeaderCell>
        );
      })}
    </VirtualTableHeader>
  );
}

export default ChannelIndexTableHeader;
