import titleCase from 'Utilities/String/titleCase';

const knownHosts = {
  'youtube.com': 'YouTube',
  'youtu.be': 'YouTube',
  'm.youtube.com': 'YouTube',
};

/**
 * Display name for the hosting site, derived from the channel URL / domain when possible.
 */
export default function getChannelNetworkLabel(channel) {
  if (channel.youtubeChannelId) {
    return 'YouTube';
  }

  const raw = channel.network;
  if (!raw) {
    return '';
  }

  if (raw.includes('.') && !raw.includes(' ')) {
    try {
      const url = raw.startsWith('http') ? raw : `https://${raw}`;
      const host = new URL(url).hostname.replace(/^www\./, '');
      return knownHosts[host] ?? titleCase(host.split('.')[0]);
    } catch {
      return raw;
    }
  }

  return raw;
}
