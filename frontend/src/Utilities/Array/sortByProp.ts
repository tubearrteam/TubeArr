import { StringKey } from 'typings/Helpers/KeysMatching';

export function sortByProp<
  // eslint-disable-next-line no-use-before-define
  T extends Partial<Record<K, string | null | undefined>>,
  K extends StringKey<T>
>(sortKey: K) {
  return (a: T, b: T) => {
    const left = a[sortKey] ?? '';
    const right = b[sortKey] ?? '';

    return left.localeCompare(right, undefined, { numeric: true });
  };
}

export default sortByProp;
