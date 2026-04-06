# Changelog

## Unreleased

### Fixed / Implemented

- **Health checks** (#13): yt-dlp version check, FFmpeg path validation, cookies file readability, and root folder write permissions via `TubeArrHealthCheckRunner`.
- **Video deduplication** (#17): Deduplicate videos across ingestion sources by YouTube video ID; detect cross-channel conflicts and reassign orphaned videos.
- **Shorts and livestream classification** (#19): Detect `isShortFormContent`, `isLive`, `isUpcoming`, and `liveBroadcastDetails` from watch-page metadata; enforce monitoring policies per video type.
- **Format fallback logic** (#25): `FallbackMode` enum (Strict / NextBestWithinCeiling / DegradeResolution / NextBest) with `DownloadRetryPolicy` that only retries transient network failures, not format errors.
- **Partial download resume** (#26): Pass `--continue` to yt-dlp for resume on interruption; clean up temp config files in a try-finally block.
- **Stalled download detection** (#27): Kill yt-dlp processes that produce no progress output for 10 minutes; mark items for retry.
- **Library import scan** (#30): `LibraryImportScanService` scans unmapped root-folder subdirectories, resolves channel candidates, and returns importable folder previews.
- **NFO schema** (#36): Standardised `<tvshow>`, `<season>`, and `<episodedetails>` output in `NfoWriter` with XML escaping and stable YouTube `<uniqueid>` elements.
- **Plex match fallback** (#42): Multi-stage episode matching: GUID → YouTube video ID → filename → `VideoFiles` path → numbering → calendar-day date fallback.
- **Stable Plex rating keys** (#43): Deterministic keys (`ch_`, `pl_`, `v_` + YouTube ID) in `PlexIdentifier` survive restarts and rescans.
- **Season/episode ordering** (#44): `StableTvNumbering` assigns indices by playlist priority → playlist position → added date → upload date → video ID, using serializable transactions.
- **Artwork serving** (#46): `PlexArtworkResolver` checks for sidecar JPEG (`-thumb.jpg`) then falls back to YouTube CDN; served via `/tv/artwork/episode-thumb`.
- **UI terminology** (#54): Frontend source tree restructured around `Channel/`, `Playlist/`, and `Video/` components.
- **Download diagnostics** (#66): yt-dlp invocation and selected format IDs logged per download; `formatSummary` field exposed in queue and history API contracts.
- **Overlapping task prevention** (#68): `ScheduledTasksHostedService` uses a `ConcurrentDictionary` + `IsCommandNameRunning` dual guard to skip already-running tasks.
- **Scheduled task retries** (#69): Failed tasks are retried up to 2 times with a 5-second delay between attempts.
