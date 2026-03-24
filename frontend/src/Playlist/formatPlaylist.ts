import padNumber from 'Utilities/Number/padNumber';
import translate from 'Utilities/String/translate';

export default function formatPlaylist(
  playlistNumber: number,
  shortFormat?: boolean
) {
  if (playlistNumber === 0) {
    return translate('Specials');
  }

  if (playlistNumber > 0) {
    return shortFormat
      ? `PL${padNumber(playlistNumber, 2)}`
      : translate('PlaylistNumberToken', { playlistNumber });
  }

  return null;
}
