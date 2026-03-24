import _ from 'lodash';
import getSectionState from 'Utilities/State/getSectionState';

function getProviderState(payload, getState, section, keyValueOnly=true) {
  const {
    id,
    ...otherPayload
  } = payload;

  const state = getSectionState(getState(), section, true);
  const rawPending = (section === 'channels' && id != null && state.pendingChanges != null && typeof state.pendingChanges[id] === 'object')
    ? state.pendingChanges[id]
    : (state.pendingChanges || {});
  const pendingChanges = Object.assign({}, rawPending, otherPayload);
  const sourcePending = (section === 'channels' && id != null && state.pendingChanges != null && state.pendingChanges[id] != null)
    ? state.pendingChanges[id]
    : state.pendingChanges;
  const pendingFields = (sourcePending && sourcePending.fields) || {};
  delete pendingChanges.fields;

  const item = id ? _.find(state.items, { id }) : state.selectedSchema || state.schema || {};

  if (item.fields) {
    pendingChanges.fields = _.reduce(item.fields, (result, field) => {
      const name = field.name;

      const value = pendingFields.hasOwnProperty(name) ?
        pendingFields[name] :
        field.value;

      // Only send the name and value to the server
      if (keyValueOnly) {
        result.push({
          name,
          value
        });
      } else {
        result.push({
          ...field,
          value
        });
      }

      return result;
    }, []);
  }

  const result = Object.assign({}, item, pendingChanges);

  delete result.presets;

  if (section === 'channels' && result.monitorNewItems !== undefined) {
    const v = result.monitorNewItems;
    if (v === 'all') {
      result.monitorNewItems = 1;
    } else if (v === 'none') {
      result.monitorNewItems = 0;
    }
  }

  return result;
}

export default getProviderState;
