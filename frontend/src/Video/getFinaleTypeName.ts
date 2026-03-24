import translate from 'Utilities/String/translate';

export default function getFinaleTypeName(finaleType?: string): string | null {
  switch (finaleType) {
    case 'channel':
      return translate('ChannelFinale');
    case 'playlist':
      return translate('PlaylistFinale');
    case 'midplaylist':
      return translate('MidplaylistFinale');
    default:
      return null;
  }
}
