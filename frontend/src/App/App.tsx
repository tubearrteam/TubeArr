import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { History } from 'history';
import React from 'react';
import DocumentTitle from 'react-document-title';
import { Provider } from 'react-redux';
import { HistoryRouter } from 'redux-first-history/rr6';
import { Store } from 'redux';
import PageConnector from 'Components/Page/PageConnector';
import ApplyTheme from './ApplyTheme';
import { AppHistoryContext } from './AppHistoryContext';
import AppRoutes from './AppRoutes';

interface AppProps {
  store: Store;
  history: History;
}

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30 * 1000,
      gcTime: 5 * 60 * 1000,
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});

function App({ store, history }: AppProps) {
  return (
    <DocumentTitle title={window.TubeArr.instanceName}>
      <QueryClientProvider client={queryClient}>
        <Provider store={store}>
          <AppHistoryContext.Provider value={history}>
            <HistoryRouter history={history}>
              <ApplyTheme />
              <PageConnector>
                <AppRoutes />
              </PageConnector>
            </HistoryRouter>
          </AppHistoryContext.Provider>
        </Provider>
      </QueryClientProvider>
    </DocumentTitle>
  );
}

export default App;
