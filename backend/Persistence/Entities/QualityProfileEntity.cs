namespace TubeArr.Backend.Data;

/// <summary>
/// Tokenized YouTube quality profile. Stored as structured tokens; yt-dlp args are built at download time.
/// List fields are stored as JSON arrays (e.g. ["AV1","VP9"]).
/// </summary>
public sealed class QualityProfileEntity
{
	/// <summary>Seeded built-in profile (migrations); not editable or deletable via API.</summary>
	public const int BuiltInDefaultProfileId = 1_000_001;

	public int Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public bool Enabled { get; set; } = true;

	// Height (yt-dlp: height)
	public int? MaxHeight { get; set; }
	public int? MinHeight { get; set; }

	// FPS (yt-dlp: fps)
	public int? MinFps { get; set; }
	public int? MaxFps { get; set; }

	// Dynamic range (yt-dlp: dynamic_range)
	public bool AllowHdr { get; set; } = true;
	public bool AllowSdr { get; set; } = true;

	// Video codecs (yt-dlp: vcodec). JSON array of "AV1","VP9","AVC". Null/empty = allow all.
	public string? AllowedVideoCodecsJson { get; set; }
	public string? PreferredVideoCodecsJson { get; set; }

	// Audio codecs (yt-dlp: acodec). JSON array of "OPUS","MP4A".
	public string? AllowedAudioCodecsJson { get; set; }
	public string? PreferredAudioCodecsJson { get; set; }

	// Containers (yt-dlp: ext). JSON array of "mp4","webm", etc.
	public string? AllowedContainersJson { get; set; }
	public string? PreferredContainersJson { get; set; }

	// Stream structure
	public bool PreferSeparateStreams { get; set; } = true;
	public bool AllowMuxedFallback { get; set; } = true;

	// Fallback behavior
	public int FallbackMode { get; set; } // 0=Strict, 1=NextBestWithinCeiling, 2=DegradeResolution, 3=NextBest
	public string? DegradeOrderJson { get; set; } // Optional custom order; null = use canonical ladder
	public string? DegradeHeightStepsJson { get; set; } // Optional; null = use canonical ladder filtered by min/max
	public bool FailIfBelowMinHeight { get; set; } = true;
	public bool RetryForBetterFormats { get; set; } = false;
	public int? RetryWindowMinutes { get; set; }

	// Advanced yt-dlp knobs bucketed by workflow.
	public string? SelectionArgs { get; set; } // -f/-S/list/check/format sorting overrides
	public string? MuxArgs { get; set; } // merge/remux/recode/ppa style options
	public string? AudioArgs { get; set; } // -x/audio-format/audio-quality
	public string? TimeArgs { get; set; } // download-sections/split/remove chapters
	public string? SubtitleArgs { get; set; } // write/auto subs/embed subs
	public string? ThumbnailArgs { get; set; } // write/convert/embed thumbnails
	public string? MetadataArgs { get; set; } // add/embed/parse/replace metadata
	public string? CleanupArgs { get; set; } // fixup/ffmpeg-location/keep-video
	public string? SponsorblockArgs { get; set; } // sponsorblock mark/remove options
}
