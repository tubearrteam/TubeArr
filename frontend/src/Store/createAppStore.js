import { createStore } from 'redux';
import createReducers, { defaultState } from 'Store/Actions/createReducers';
import createMiddlewares from 'Store/Middleware/middlewares';

function createAppStore({ createReduxHistory, routerMiddleware, routerReducer }) {
  const appStore = createStore(
    createReducers(routerReducer),
    defaultState,
    createMiddlewares(routerMiddleware)
  );

  const history = createReduxHistory(appStore);

  return { store: appStore, history };
}

export default createAppStore;
