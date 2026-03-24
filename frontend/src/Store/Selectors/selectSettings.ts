import { cloneDeep, isEmpty } from 'lodash';
import { Error } from 'App/State/AppSectionState';
import {
  Failure,
  Pending,
  PendingField,
  PendingSection,
  ValidationError,
  ValidationFailure,
  ValidationWarning,
} from 'typings/pending';

interface ValidationFailures {
  errors: ValidationError[];
  warnings: ValidationWarning[];
}

function getValidationFailures(saveError?: Error): ValidationFailures {
  if (!saveError || saveError.status !== 400) {
    return {
      errors: [],
      warnings: [],
    };
  }

  const failures = saveError.responseJSON as ValidationFailure[] | null | undefined;
  const failuresArray = Array.isArray(failures) ? failures : [];
  const failuresCopy = cloneDeep(failuresArray);
  const safeFailures = Array.isArray(failuresCopy) ? failuresCopy : [];
  return safeFailures.reduce(
    (acc: ValidationFailures, failure: ValidationFailure) => {
      if (failure.isWarning) {
        acc.warnings.push(failure as ValidationWarning);
      } else {
        acc.errors.push(failure as ValidationError);
      }

      return acc;
    },
    {
      errors: [],
      warnings: [],
    }
  );
}

function getFailures(failures: ValidationFailure[], key: string) {
  const result = [];

  for (let i = failures.length - 1; i >= 0; i--) {
    if (failures[i].propertyName.toLowerCase() === key.toLowerCase()) {
      result.unshift(mapFailure(failures[i]));

      failures.splice(i, 1);
    }
  }

  return result;
}

function mapFailure(failure: ValidationFailure): Failure {
  return {
    errorMessage: failure.errorMessage,
    infoLink: failure.infoLink,
    detailedDescription: failure.detailedDescription,

    // TODO: Remove these renamed properties
    message: failure.errorMessage,
    link: failure.infoLink,
    detailedMessage: failure.detailedDescription,
  };
}

interface ModelBaseSetting {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  [id: string]: any;
}

function selectSettingsImpl<T extends ModelBaseSetting>(
  item: T,
  pendingChanges: Partial<ModelBaseSetting>,
  saveError?: Error
) {
  const safeItem = item ?? {};
  const safePendingChanges = pendingChanges ?? {};
  const { errors, warnings } = getValidationFailures(saveError);

  // Merge all settings from the item along with pending
  // changes to ensure any settings that were not included
  // with the item are included.
  const allSettings = Object.assign({}, safeItem, safePendingChanges);

  const keys = Object.keys(allSettings);
  const keysArray = typeof keys !== 'undefined' && keys !== null && Array.isArray(keys) ? keys : [];
  const settings = keysArray.reduce(
    (acc: PendingSection<T>, key) => {
      if (key === 'fields') {
        return acc;
      }

      // Return a flattened value
      if (key === 'implementationName') {
        acc.implementationName = safeItem[key];

        return acc;
      }

      const setting: Pending<T> = {
        value: safeItem[key],
        pending: false,
        errors: getFailures(errors, key),
        warnings: getFailures(warnings, key),
      };

      if (Object.prototype.hasOwnProperty.call(safePendingChanges, key)) {
        setting.previousValue = setting.value;
        setting.value = safePendingChanges[key];
        setting.pending = true;
      }

      // @ts-expect-error - This is a valid key
      acc[key] = setting;
      return acc;
    },
    {} as PendingSection<T>
  );

  if (safeItem && 'fields' in safeItem) {
    const fieldsArray = Array.isArray(safeItem.fields) ? safeItem.fields : [];
    const fields = (fieldsArray ?? []).reduce((acc: PendingField<T>[], f) => {
        const field: PendingField<T> = Object.assign(
          { pending: false, errors: [], warnings: [] },
          f
        );

        if ('fields' in safePendingChanges) {
          const pendingChangesFields = safePendingChanges.fields as Record<
            string,
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            any
          >;

          if (pendingChangesFields.hasOwnProperty(field.name)) {
            field.previousValue = field.value;
            field.value = pendingChangesFields[field.name];
            field.pending = true;
          }
        }

        field.errors = getFailures(errors, field.name);
        field.warnings = getFailures(warnings, field.name);

        acc.push(field);
        return acc;
      }, []);

    if (fields.length) {
      settings.fields = fields;
    }
  }

  const validationErrors = errors;
  const validationWarnings = warnings;

  return {
    settings,
    validationErrors,
    validationWarnings,
    hasPendingChanges: !isEmpty(safePendingChanges),
    hasSettings: !isEmpty(settings),
    pendingChanges: safePendingChanges,
  };
}

const EMPTY_SETTINGS_RESULT = {
  settings: {},
  validationErrors: [],
  validationWarnings: [],
  hasPendingChanges: false,
  hasSettings: false,
  pendingChanges: {},
};

function selectSettings<T extends ModelBaseSetting>(
  item: T,
  pendingChanges: Partial<ModelBaseSetting>,
  saveError?: Error
) {
  try {
    return selectSettingsImpl(item, pendingChanges, saveError);
  } catch (err) {
    if (process.env.NODE_ENV !== 'production') {
      console.error('selectSettings failed', err);
    }
    return EMPTY_SETTINGS_RESULT as unknown as ReturnType<typeof selectSettingsImpl<T>>;
  }
}

export default selectSettings;
