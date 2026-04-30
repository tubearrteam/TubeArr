import { createStore } from 'redux';
import createReducers, { defaultState } from 'Store/Actions/createReducers';
import createMiddlewares from 'Store/Middleware/middlewares';
import { setOperationProgressDispatch } from 'Utilities/operationProgressBus';

function createAppStore({ createReduxHistory, routerMiddleware, routerReducer }) {
  const appStore = createStore(
    createReducers(routerReducer),
    defaultState,
    createMiddlewares(routerMiddleware)
  );

  setOperationProgressDispatch(appStore.dispatch);

  const history = createReduxHistory(appStore);

  return { store: appStore, history };
}

export default createAppStore;
