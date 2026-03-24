import React, { Component } from 'react';
import { Route } from 'react-router-dom';
import ImportChannelConnector from 'AddChannel/ImportChannel/Import/ImportChannelConnector';
import ImportChannelSelectFolderConnector from 'AddChannel/ImportChannel/SelectFolder/ImportChannelSelectFolderConnector';
import Switch from 'Components/Router/Switch';

class ImportChannel extends Component {

  //
  // Render

  render() {
    return (
      <Switch>
        <Route
          exact={true}
          path="/add/import"
          component={ImportChannelSelectFolderConnector}
        />

        <Route
          path="/add/import/:rootFolderId"
          component={ImportChannelConnector}
        />
      </Switch>
    );
  }
}

export default ImportChannel;
