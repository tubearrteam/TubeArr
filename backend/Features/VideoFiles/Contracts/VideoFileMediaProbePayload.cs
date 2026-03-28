namespace TubeArr.Backend.Contracts;

/// <summary>Serialized to <see cref="Data.VideoFileEntity.MediaInfoJson"/> and used to populate <see cref="VideoFileDto"/>.</summary>
public sealed class VideoFileMediaProbePayload
{
	public int DurationSeconds { get; set; }
	public VideoFileMediaInfoSnapshot MediaInfo { get; set; } = new();
	public VideoFileProbeFormatLabel[] CustomFormats { get; set; } = Array.Empty<VideoFileProbeFormatLabel>();
}

public sealed class VideoFileProbeFormatLabel
{
	public int Id { get; set; }
	public string Name { get; set; } = "";
}

/// <summary>Shape aligned with frontend <c>typings/MediaInfo.ts</c> / <c>VideoFile/MediaInfo.tsx</c>.</summary>
public sealed class VideoFileMediaInfoSnapshot
{
	public double AudioBitrate { get; set; }
	public double AudioChannels { get; set; }
	public string AudioCodec { get; set; } = "";
	public string AudioLanguages { get; set; } = "";
	public int AudioStreamCount { get; set; }
	public int VideoBitDepth { get; set; }
	public double VideoBitrate { get; set; }
	public string VideoCodec { get; set; } = "";
	public double VideoFps { get; set; }
	public string VideoDynamicRange { get; set; } = "";
	public string VideoDynamicRangeType { get; set; } = "";
	public string Resolution { get; set; } = "";
	public string RunTime { get; set; } = "";
	public string ScanType { get; set; } = "";
	public string Subtitles { get; set; } = "";
}
