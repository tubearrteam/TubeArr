using TubeArr.Backend.Data;

namespace TubeArr.Backend;

/// <summary>
/// When filter flags are enabled, Shorts/livestreams must stay unmonitored
/// and are excluded from round-robin caps / channel-wide monitor presets.
/// Shorts are only treated separately when the channel has a YouTube Shorts tab (<see cref="ChannelEntity.HasShortsTab"/>);
/// otherwise &quot;filter shorts&quot; does not apply (main feed may mix formats; /shorts can mirror /videos).
/// </summary>
public static class FilterOutShortsMonitoringHelper
{
	public static void ApplyChannelPolicyToVideos(ChannelEntity channel, IEnumerable<VideoEntity> videos)
	{
		if (channel is null)
			return;
		var filterShorts = channel.FilterOutShorts && channel.HasShortsTab == true;
		if (!filterShorts && !channel.FilterOutLivestreams)
			return;
		foreach (var v in videos)
		{
			if ((filterShorts && v.IsShort) ||
			    (channel.FilterOutLivestreams && v.IsLivestream))
				v.Monitored = false;
		}
	}

	public static void ClampVideoMonitored(
		VideoEntity video,
		bool channelFilterOutShorts,
		bool channelFilterOutLivestreams,
		bool? channelHasShortsTab)
	{
		var filterShorts = channelFilterOutShorts && channelHasShortsTab == true;
		if ((filterShorts && video.IsShort) ||
		    (channelFilterOutLivestreams && video.IsLivestream))
			video.Monitored = false;
	}
}
