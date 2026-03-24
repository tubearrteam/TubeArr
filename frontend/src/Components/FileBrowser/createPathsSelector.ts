import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';

function createPathsSelector() {
  return createSelector(
    (state: AppState) => state.paths,
    (paths) => {
      const {
        isFetching,
        isPopulated,
        error,
        parent,
        currentPath,
        directories,
        files,
      } = paths;

      const filteredPaths = [...(directories ?? []), ...(files ?? [])]
        .filter((item) => item != null && typeof (item as { path?: string }).path === 'string')
        .filter(({ path }) => path.toLowerCase().startsWith(currentPath.toLowerCase()));

      return {
        isFetching,
        isPopulated,
        error,
        parent,
        currentPath,
        directories,
        files,
        paths: filteredPaths,
      };
    }
  );
}

export default createPathsSelector;
