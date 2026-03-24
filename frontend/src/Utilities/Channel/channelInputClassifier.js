/**
 * Channel input classifier for add-channel search protection.
 * Categorizes user input so we can avoid YouTube search API when the input
 * is a direct channel ID, channel URL, handle, or other resolvable form.
 * UC... is the canonical stored identifier for YouTube channels.
 */

// Canonical channel ID format: UC + 22 base64url-style characters
const CHANNEL_ID_REGEX = /^UC[0-9A-Za-z_-]{22}$/;

const YOUTUBE_DOMAIN = /^(https?:\/\/)?(www\.|m\.)?youtube\.com(\/|$)/i;
const CHANNEL_PATH = /youtube\.com\/channel\/(UC[0-9A-Za-z_-]{22})/i;
const HANDLE_PATH = /youtube\.com\/@([^/?#]+)/i;
const USER_PATH = /youtube\.com\/user\/([^/?#]+)/i;
const CUSTOM_PATH = /youtube\.com\/c\/([^/?#]+)/i;
const WATCH_PATH = /youtube\.com\/watch\?/i;
const SHORT_PATH = /youtu\.be\//i;

export const InputKind = Object.freeze({
  Empty: 'Empty',
  ChannelId: 'ChannelId',
  ChannelUrl: 'ChannelUrl',
  HandleUrl: 'HandleUrl',
  HandlePath: 'HandlePath',
  LegacyUserUrl: 'LegacyUserUrl',
  LegacyCustomUrl: 'LegacyCustomUrl',
  GenericYouTubeUrl: 'GenericYouTubeUrl',
  SearchTerm: 'SearchTerm',
  Unknown: 'Unknown'
});

/**
 * Normalize raw input: trim, optionally add protocol, strip trailing slash,
 * lowercase domain for matching. Does not alter path/handle case.
 */
export function normalizeInput(input) {
  if (input == null || typeof input !== 'string') {
    return '';
  }
  let s = input.trim();
  if (!s) return '';

  // Ensure URL-like strings have protocol for parsing
  if (/^[a-zA-Z0-9][a-zA-Z0-9-]*\./.test(s) && !/^https?:\/\//i.test(s)) {
    s = 'https://' + s;
  }
  // Strip trailing slash for consistent path matching
  s = s.replace(/\/+$/, '');
  return s;
}

/**
 * Classify normalized input into one of InputKind.
 * Returns { kind, channelId?, normalizedUrl? }.
 * channelId is only set when we can extract a UC... id without network.
 * normalizedUrl is set when we can build a fetchable YouTube URL for HTTP extraction.
 */
export function classifyInput(rawInput) {
  const input = normalizeInput(rawInput);
  if (!input) {
    return { kind: InputKind.Empty };
  }

  // Bare UC... channel ID (no URL structure)
  if (CHANNEL_ID_REGEX.test(input)) {
    return { kind: InputKind.ChannelId, channelId: input };
  }

  // @handle (no domain) – handle path only
  if (/^@[^/?#\s]+$/i.test(input)) {
    const handle = input.slice(1);
    const normalizedUrl = `https://www.youtube.com/@${handle}`;
    return { kind: InputKind.HandlePath, normalizedUrl, handle };
  }

  // Full URL parsing (domain must be youtube)
  const lower = input.toLowerCase();
  if (!YOUTUBE_DOMAIN.test(input)) {
    // Not a YouTube URL; treat as search term if it looks like a URL to something else
    if (/^https?:\/\//i.test(input) || /^[a-zA-Z0-9][a-zA-Z0-9-]*\./.test(input)) {
      return { kind: InputKind.Unknown };
    }
    return { kind: InputKind.SearchTerm };
  }

  // /channel/UC... – direct channel URL
  const channelMatch = input.match(CHANNEL_PATH);
  if (channelMatch) {
    const channelId = channelMatch[1];
    if (CHANNEL_ID_REGEX.test(channelId)) {
      return { kind: InputKind.ChannelUrl, channelId, normalizedUrl: `https://www.youtube.com/channel/${channelId}` };
    }
  }

  // Explicit /channel/ path (could be in pathname)
  const pathChannelMatch = input.match(/\/channel\/(UC[0-9A-Za-z_-]{22})(?:\/|$|\?|#)/i);
  if (pathChannelMatch) {
    const channelId = pathChannelMatch[1];
    if (CHANNEL_ID_REGEX.test(channelId)) {
      return { kind: InputKind.ChannelUrl, channelId, normalizedUrl: `https://www.youtube.com/channel/${channelId}` };
    }
  }

  // @handle or /@name/videos
  const handleMatch = input.match(HANDLE_PATH);
  if (handleMatch) {
    const handle = handleMatch[1];
    const normalizedUrl = input.includes('/videos') || input.includes('/streams') || input.includes('/playlists')
      ? `https://www.youtube.com/@${handle}/videos`
      : `https://www.youtube.com/@${handle}`;
    return { kind: InputKind.HandleUrl, normalizedUrl, handle };
  }

  // /user/...
  const userMatch = input.match(USER_PATH);
  if (userMatch) {
    const userPart = userMatch[1];
    const normalizedUrl = `https://www.youtube.com/user/${userPart}`;
    return { kind: InputKind.LegacyUserUrl, normalizedUrl };
  }

  // /c/...
  if (CUSTOM_PATH.test(input)) {
    const customPart = input.match(CUSTOM_PATH)[1];
    const url = `https://www.youtube.com/c/${customPart}`;
    return { kind: InputKind.LegacyCustomUrl, normalizedUrl: url };
  }

  // Other YouTube URLs (not watch, not short) – may still be channel-adjacent
  if (YOUTUBE_DOMAIN.test(input) && !WATCH_PATH.test(input) && !SHORT_PATH.test(input)) {
    return { kind: InputKind.GenericYouTubeUrl, normalizedUrl: input.startsWith('http') ? input : `https://${input}` };
  }

  // Watch / short URLs are not reliable for channel resolution without fetch
  if (WATCH_PATH.test(input) || SHORT_PATH.test(input)) {
    return { kind: InputKind.GenericYouTubeUrl, normalizedUrl: input.startsWith('http') ? input : `https://${input}` };
  }

  return { kind: InputKind.SearchTerm };
}

/**
 * Whether the classification indicates we should try direct or HTTP resolution
 * instead of search (i.e. do not call search API until resolution fails or debounce).
 */
export function isResolvableWithoutSearch(classification) {
  const { kind } = classification;
  return (
    kind === InputKind.ChannelId ||
    kind === InputKind.ChannelUrl ||
    kind === InputKind.HandleUrl ||
    kind === InputKind.HandlePath ||
    kind === InputKind.LegacyUserUrl ||
    kind === InputKind.LegacyCustomUrl ||
    kind === InputKind.GenericYouTubeUrl
  );
}

/**
 * Whether we have a channelId already (no network needed for resolution).
 */
export function hasDirectChannelId(classification) {
  return classification.kind === InputKind.ChannelId || classification.kind === InputKind.ChannelUrl;
}

export { CHANNEL_ID_REGEX };
