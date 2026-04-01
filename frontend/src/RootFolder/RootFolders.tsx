import React, { useEffect } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import Alert from 'Components/Alert';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import { kinds } from 'Helpers/Props';
import { fetchRootFolders } from 'Store/Actions/rootFolderActions';
import createRootFoldersSelector from 'Store/Selectors/createRootFoldersSelector';
import translate from 'Utilities/String/translate';
import RootFolderRow from './RootFolderRow';

type RootFoldersProps = {
  /** When true, skip the initial fetch (parent is responsible for loading the list). */
  disableAutoFetch?: boolean;
  libraryImportScanningId?: number | null;
  onLibraryImportScanPress?: (id: number) => void;
};

const rootFolderColumns = [
  {
    name: 'path',
    label: () => translate('Path'),
    isVisible: true,
  },
  {
    name: 'freeSpace',
    label: () => translate('FreeSpace'),
    isVisible: true,
  },
  {
    name: 'unmappedFolders',
    label: () => translate('UnmappedFolders'),
    isVisible: true,
  },
  {
    name: 'actions',
    isVisible: true,
  },
];

function RootFolders(props: RootFoldersProps) {
  const {
    disableAutoFetch = false,
    libraryImportScanningId = null,
    onLibraryImportScanPress
  } = props;

  const { isFetching, isPopulated, error, items } = useSelector(
    createRootFoldersSelector()
  );

  const dispatch = useDispatch();

  useEffect(() => {
    if (!disableAutoFetch) {
      dispatch(fetchRootFolders());
    }
  }, [dispatch, disableAutoFetch]);

  if (isFetching && !isPopulated) {
    return <LoadingIndicator />;
  }

  if (!isFetching && !!error) {
    return (
      <Alert kind={kinds.DANGER}>{translate('RootFoldersLoadError')}</Alert>
    );
  }

  return (
    <Table columns={rootFolderColumns}>
      <TableBody>
        {items.map((rootFolder) => {
          return (
            <RootFolderRow
              key={rootFolder.id}
              id={rootFolder.id}
              path={rootFolder.path}
              accessible={rootFolder.accessible}
              freeSpace={rootFolder.freeSpace}
              unmappedFolders={rootFolder.unmappedFolders}
              libraryImportScanning={libraryImportScanningId === rootFolder.id}
              onLibraryImportScanPress={onLibraryImportScanPress}
            />
          );
        })}
      </TableBody>
    </Table>
  );
}

export default RootFolders;
