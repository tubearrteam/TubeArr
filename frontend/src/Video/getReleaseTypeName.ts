import translate from 'Utilities/String/translate';

export default function getReleaseTypeName(
  releaseType?: string
): string | null {
  switch (releaseType) {
    case 'singleVideo':
      return translate('SingleVideo');
    case 'multiVideo':
      return translate('MultiVideo');
    case 'playlistPack':
      return translate('PlaylistPack');
    default:
      return translate('Unknown');
  }
}
