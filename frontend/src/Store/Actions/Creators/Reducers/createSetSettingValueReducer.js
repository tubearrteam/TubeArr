import _ from 'lodash';
import getSectionState from 'Utilities/State/getSectionState';
import updateSectionState from 'Utilities/State/updateSectionState';

function createSetSettingValueReducer(section) {
  return (state, { payload }) => {
    if (section === payload.section) {
      const { name, value } = payload;
      const newState = getSectionState(state, section);
      newState.pendingChanges = Object.assign({}, newState.pendingChanges);

      const currentValue = newState.item ? newState.item[name] : null;
      const pendingState = newState.pendingChanges;

      let parsedValue = null;
      const hasWrappedValue =
        currentValue != null &&
        typeof currentValue === 'object' &&
        Object.prototype.hasOwnProperty.call(currentValue, 'value');
      const comparisonValue = hasWrappedValue ? currentValue.value : currentValue;

      if (_.isNumber(comparisonValue)) {
        if (value == null || value === '') {
          parsedValue = null;
        } else {
          parsedValue = parseInt(value, 10);
        }
      } else if (_.isBoolean(comparisonValue) && typeof value === 'string') {
        parsedValue = value === 'true';
      } else {
        parsedValue = value;
      }

      if (comparisonValue === parsedValue) {
        delete pendingState[name];
      } else {
        pendingState[name] = parsedValue;
      }

      return updateSectionState(state, section, newState);
    }

    return state;
  };
}

export default createSetSettingValueReducer;
