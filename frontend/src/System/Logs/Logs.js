import React, { Component } from 'react';
import { Route } from 'react-router-dom';
import Switch from 'Components/Router/Switch';
import LogFilesConnector from './Files/LogFilesConnector';
import UpdateLogFilesConnector from './Updates/UpdateLogFilesConnector';

class Logs extends Component {

  //
  // Render

  render() {
    return (
      <Switch>
        <Route
          path="/system/logs/files/update"
          component={UpdateLogFilesConnector}
        />

        <Route
          exact={true}
          path="/system/logs/files"
          component={LogFilesConnector}
        />
      </Switch>
    );
  }
}

export default Logs;
