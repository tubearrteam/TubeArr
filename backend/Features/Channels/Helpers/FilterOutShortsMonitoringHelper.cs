using TubeArr.Backend.Data;

namespace TubeArr.Backend;

/// <summary>
/// When filter flags are enabled, Shorts/livestreams must stay unmonitored
/// and are excluded from round-robin caps / channel-wide monitor presets.
/// </summary>
public static class FilterOutShortsMonitoringHelper
{
	public static void ApplyChannelPolicyToVideos(ChannelEntity channel, IEnumerable<VideoEntity> videos)
	{
		if (channel is null || (!channel.FilterOutShorts && !channel.FilterOutLivestreams))
			return;
		foreach (var v in videos)
		{
			if ((channel.FilterOutShorts && v.IsShort) ||
			    (channel.FilterOutLivestreams && v.IsLivestream))
				v.Monitored = false;
		}
	}

	public static void ClampVideoMonitored(VideoEntity video, bool channelFilterOutShorts, bool channelFilterOutLivestreams)
	{
		if ((channelFilterOutShorts && video.IsShort) ||
		    (channelFilterOutLivestreams && video.IsLivestream))
			video.Monitored = false;
	}
}
