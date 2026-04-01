/** Backend may return PascalCase keys; normalize for UI and create-channel payloads. */
export function normalizeChannelSearchItem(item: unknown) {
  if (!item || typeof item !== 'object') return item;
  const o = item as Record<string, unknown>;
  return {
    ...o,
    youtubeChannelId: o.youtubeChannelId ?? o.YoutubeChannelId,
    title: o.title ?? o.Title,
    titleSlug: o.titleSlug ?? o.TitleSlug,
    description: o.description ?? o.Description,
    thumbnailUrl: o.thumbnailUrl ?? o.ThumbnailUrl,
    channelUrl: o.channelUrl ?? o.ChannelUrl,
    handle: o.handle ?? o.Handle,
    subscriberCount: o.subscriberCount ?? o.SubscriberCount,
    videoCount: o.videoCount ?? o.VideoCount,
    channelType: o.channelType ?? o.ChannelType
  };
}

export function normalizeChannelSearchItems(arr: unknown) {
  return Array.isArray(arr) ? arr.map((x) => normalizeChannelSearchItem(x)) : [];
}
