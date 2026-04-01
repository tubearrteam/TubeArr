import React from 'react';
import { Route, Routes, useParams } from 'react-router-dom';
import ImportChannelConnector from 'AddChannel/ImportChannel/Import/ImportChannelConnector';
import ImportChannelSelectFolderConnector from 'AddChannel/ImportChannel/SelectFolder/ImportChannelSelectFolderConnector';

function ImportChannelFolderConnector() {
  const { rootFolderId } = useParams();
  const match = { params: { rootFolderId: rootFolderId ?? '' } };
  return <ImportChannelConnector match={match} />;
}

export default function ImportChannel() {
  return (
    <Routes>
      <Route index element={<ImportChannelSelectFolderConnector />} />
      <Route path=":rootFolderId" element={<ImportChannelFolderConnector />} />
    </Routes>
  );
}
