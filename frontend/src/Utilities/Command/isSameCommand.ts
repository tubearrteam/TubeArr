import { CommandBody } from 'Commands/Command';

function normalizeArrayValue(value: unknown[]) {
  return [...value]
    .map((item) => {
      return typeof item === 'string' || typeof item === 'number'
        ? `${typeof item}:${item}`
        : JSON.stringify(item);
    })
    .sort();
}

function isSameCommand(
  commandA: Partial<CommandBody>,
  commandB: Partial<CommandBody>
) {
  if (
    commandA.name?.toLocaleLowerCase() !== commandB.name?.toLocaleLowerCase()
  ) {
    return false;
  }

  for (const key in commandB) {
    if (key !== 'name') {
      const value = commandB[key];

      if (Array.isArray(value)) {
        const sortedB = normalizeArrayValue(value);
        const commandAProp = commandA[key];
        const sortedA = Array.isArray(commandAProp)
          ? normalizeArrayValue(commandAProp)
          : [];

        if (sortedA === sortedB) {
          return true;
        }

        if (sortedA == null || sortedB == null) {
          return false;
        }

        if (sortedA.length !== sortedB.length) {
          return false;
        }

        for (let i = 0; i < sortedB.length; ++i) {
          if (sortedB[i] !== sortedA[i]) {
            return false;
          }
        }
      } else if (value !== commandA[key]) {
        return false;
      }
    }
  }

  return true;
}

export default isSameCommand;
