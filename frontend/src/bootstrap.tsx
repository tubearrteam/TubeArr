import { createBrowserHistory } from 'history';
import { createReduxHistoryContext } from 'redux-first-history';
import React from 'react';
import { createRoot } from 'react-dom/client';
import createAppStore from 'Store/createAppStore';
import App from './App/App';

import 'Diag/ConsoleApi';

export async function bootstrap() {
  const browserHistory = createBrowserHistory();
  const historyContext = createReduxHistoryContext({ history: browserHistory });
  const { store, history } = createAppStore(historyContext);
  const container = document.getElementById('root');

  const root = createRoot(container!);
  root.render(<App store={store} history={history} />);
}
