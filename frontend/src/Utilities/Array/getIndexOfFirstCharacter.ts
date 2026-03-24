import Channel from 'Channel/Channel';

const STARTS_WITH_NUMBER_REGEX = /^\d/;

export default function getIndexOfFirstCharacter(
  items: Channel[],
  character: string
) {
  return items.findIndex((item) => {
    const sortTitle = String(item.sortTitle ?? item.title ?? '').trim();
    const firstCharacter = sortTitle.charAt(0);

    if (character === '#') {
      return STARTS_WITH_NUMBER_REGEX.test(firstCharacter);
    }

    return firstCharacter === character;
  });
}
