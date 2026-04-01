/** Matches ChannelDetailsPlaylistConnector playlist membership for a video. */
export default function videoMatchesChannelPlaylist(video, channel, playlistNumber) {
  if (video.channelId !== channel.id) {
    return false;
  }
  if (playlistNumber === 1) {
    if (channel.filterOutShorts && channel.hasShortsTab === true && video.isShort === true) {
      return false;
    }
    if (channel.filterOutLivestreams && video.isLivestream === true) {
      return false;
    }
    return true;
  }
  if (playlistNumber === -1) {
    return video.isShort === true;
  }
  if (playlistNumber === -2) {
    return video.isLivestream === true;
  }
  if (playlistNumber > 1) {
    const customNums = video.customPlaylistNumbers;
    if (Array.isArray(customNums) && customNums.includes(playlistNumber)) {
      return true;
    }
    const curated = video.curatedPlaylistNumbers;
    if (Array.isArray(curated) && curated.length > 0) {
      return curated.includes(playlistNumber);
    }
  }
  return video.playlistNumber === playlistNumber;
}
