## TubeArr Plex Custom Metadata Provider

TubeArr includes a **local Plex Custom Metadata Provider** so Plex can use TubeArr as the authoritative metadata source for YouTube-backed TV libraries.

This does **not** replace TubeArr’s existing **NFO + local artwork sidecars**. Sidecars continue to be written for compatibility/local use. For **episode** `thumb`, Plex still needs an **HTTP(S) URL** (per [Metadata.md](https://github.com/plexinc/tmdb-example-provider/blob/main/docs/Metadata.md)), so when a **`{mediaBasename}-thumb.jpg`** file exists next to the **primary** media file (same convention as TubeArr’s Plex artwork export), TubeArr points `thumb` at **`GET /tv/artwork/episode-thumb?youtubeVideoId=…`**, which streams that JPEG. If there is no sidecar, episode thumbs use **stored** `Video.ThumbnailUrl`, then **`https://i.ytimg.com/vi/{youtubeVideoId}/maxresdefault.jpg`**. Show/season artwork continues to use **remote URLs** from stored channel/playlist fields (`ThumbnailUrl`, `BannerUrl`).

### Plex UI URL

When Plex asks for the provider **URL**, use the **TV provider root** (the same folder that serves `GET /tv`), for example:

`http://<host>:<port>/tv`

Example: `http://localhost:5075/tv`

Plex validates `GET /tv` against the [Media Provider schema](https://github.com/plexinc/tmdb-example-provider/blob/main/docs/MediaProvider.md): metadata types must be numeric (`2`/`3`/`4` for show/season/episode), each type must include a `Scheme` array, and each `Feature` must include a `key` path (`/library/metadata`, `/library/metadata/matches`). Metadata item `key` fields are relative to that provider root (e.g. `/library/metadata/ch_UC…`).

### Logging

Logs go to the **console** and to **`backend/logs/tubearr-YYYYMMDD.log`** (rolling daily, 14 files kept) via Serilog. Category levels are under **`Serilog:MinimumLevel`** in `appsettings.json` / `appsettings.Development.json`. Plex routes log as **`TubeArr.Backend.Plex.PlexProviderLog`** (also override **`TubeArr.Backend.Plex`**). Each Plex hit emits a **`[WRN] Plex tv: …`** line so activity is visible even when the default minimum is **Warning**. Deeper traces use **Debug** on that category.

Optional **`logs/plex-http.log`** (raw request lines for paths containing `/tv`, plus one startup line) is written only when **`Plex:HttpProbeLogging`** is **`true`** in configuration (default **`false`** in `appsettings.json`; **`true`** in `appsettings.Development.json`). If probe logging is off, rely on Serilog and **`TubeArr.Backend.Plex`** categories instead. If **`plex-http.log`** is empty while the flag is on, Plex is not reaching this host URL (firewall, wrong port, or provider URL in Plex). If **`enabled=false`** appears in warnings, enable the provider: `PUT /api/v1/config/plex-provider` with `"enabled": true` (or the Settings UI).

### Enabling

TubeArr exposes config endpoints:

- `GET /api/v1/config/plex-provider`
- `PUT /api/v1/config/plex-provider`

Set:

- `enabled`: `true`
- `basePath`: optional (example: `plex`) to also serve the provider under `/{basePath}/tv`

The provider is always available at `/tv` when enabled.

### Plex provider endpoints

- `GET /tv`
- `POST /tv/library/metadata/matches`
- `GET /tv/library/metadata/{ratingKey}`
- `GET /tv/library/metadata/{ratingKey}/children` (season lists for a show, episodes for a season; Plex follows each show/season item’s `key`, which uses this path per Metadata.md). Supports `X-Plex-Container-Size` (default 20) and `X-Plex-Container-Start` (1-based; default 1), or the same as query parameters.
- `GET /tv/library/metadata/{ratingKey}/grandchildren` (**show only**): all episodes for the channel in stable season/episode order (Plex TV libraries call this; without it, refresh can fail). Same paging headers as `/children`.
- `GET /tv/artwork/episode-thumb?youtubeVideoId={id}`: serves the on-disk **`{basename}-thumb.jpg`** next to the episode’s primary file when both exist (provider must be **enabled**); used as the episode `thumb` URL in metadata when that sidecar is present.
- `GET /tv/health`

### Identifier scheme

- **identifier**: `tv.plex.agents.custom.tubearr`
- **scheme**: `tv.plex.agents.custom.tubearr`

ratingKey formats:

- show: `ch_<channelId>`
- season: `pl_<youtubePlaylistId>` for native YouTube playlists; `cst_<id>` for **rule-based custom playlists** (`ChannelCustomPlaylists.Id`, same **Season 10001+** slots as NFO folder layout)
- episode: `v_<videoId>`

### Recommended library naming

TubeArr’s naming pipeline should keep the YouTube IDs embedded in folder/file names so matching is deterministic:

`<Channel Name> [<channelId>]/Season XX/<Channel Name> - sXXeYYY - <Sanitized Video Title> [<videoId>].ext`

### How matching works (high level)

Plex’s match `POST` commonly includes **`filename`** (often a relative path to a media file). **`ExtractMatchPath`** prefers **`filename`** over **`path`** / **`Media[].Part[].file`**.

**Filename / path is treated as authoritative** for identity whenever present: **`[UC…]`**, configured **`Channel.Path`** prefix, show folder name (skipping `Season NN`), then Plex’s **`guid`** / titles. That order helps **automatic** matching when Plex’s titles or keys disagree with your on-disk layout.

- **Show** (`type` 2): `[UC…]` / channel path / folder name from the path hint, then `guid` (`…/show/ch_…`), then case-insensitive `title`.
- **Season** (`type` 3): same path-first channel resolution, then `parentRatingKey` / `parentGuid`, then **`parentTitle`** + `parentGuid`. Season 01 is “channel uploads”. A **`Season 10001+`** folder in the path maps to the **custom playlist** at that NFO slot (`cst_<id>`). **`index`** can also match native `SeasonIndex` (2+) or those custom slots.
- **Episode** (`type` 4): **`[videoId]`** in filename/path; then **`VideoFiles`** by exact path or **by filename suffix** when Plex’s path differs from the DB path; then channel from **path** before **`grandparentRatingKey`** / **`grandparentTitle`** + `grandparentGuid`; then **`date`** / **`originallyAvailableAt`** only when `parentIndex` or `index` is missing/zero; then stable `PlexSeasonIndex` / `PlexEpisodeIndex`. **Episode titles** use `Video.Title` when it looks like a real title (not an empty/whitespace-only value that equals the YouTube id); then **`{basename}.nfo`** title when present; then **`snippet.title`** / **`snippet.localized.title`** from persisted **`YouTubeDataApiVideoResourceJson`**; then first line of overview/description; then the video id; then `Episode N`. **Episode `summary`** (description) is truncated to a fixed max length so large channels do not produce oversized metadata JSON for Plex/proxies.

### Stable numbering (important)

TubeArr persists:

- `Playlists.SeasonIndex` (per channel; **assigned once** and never reused)
- `Videos.PlexSeasonIndex` / `Videos.PlexEpisodeIndex` (**assigned once** from the same folder rules as NFO: custom rules first → **Season 10001+** + `PlexPrimaryCustomPlaylistId`, else primary YouTube playlist → native season, else channel uploads = Season 01)

**Season order for native YouTube playlists** is driven by `StableTvNumbering.EnsureChannelPlaylistSeasonIndicesMatchPriorityAsync`, which uses the same ordering as **on-disk curated playlist folders**: `ChannelDtoMapper.OrderPlaylistsForFileOrganization` / `LoadOrderedPlaylistIdsForFileOrganizationAsync` (priority, then the channel’s multi-match strategy chain)—**not** the channel UI’s “latest upload” interleave. Plex lists seasons using those `SeasonIndex` values so **Season 02+ line up with `Season 02` / `Season 03` … folders** for `PlaylistEntity` rows.

**Custom (rule-based) playlist folders** use **`Season 10001+`** on disk; Plex lists them after native seasons with **`cst_<ChannelCustomPlaylists.Id>`** keys and the same slot order as NFO (`Priority`, then id).

This makes Plex matching deterministic and prevents renumber drift when playlists reorder or change activity.

**Custom season (10001+) episode numbers** use the same ordering as stable numbering when no native playlist row owns that season: all videos with that `PlexSeasonIndex`, **upload date then video id**. NFO `<episode>` and `{Playlist Index}` follow that list—not the native primary playlist’s `PlaylistVideos` order.

**Season 01 (channel uploads) episode numbers** stay aligned across Plex (`PlexEpisodeIndex`), custom NFO `<episode>`, and `{Playlist Index}` in filenames: all use the same ordered list (videos whose `PlexSeasonIndex` is 01, by upload date then id). Videos on other seasons are not counted into that episode sequence. `Videos.PlexIndexLocked` skips rewriting that row’s episode index when resequencing.

**Recompute after rule changes:** `POST /api/v1/channels/{id}/plex-indices/refresh` clears Plex placement on non-locked videos in that channel and re-runs the same assignment as above. The channel page toolbar exposes **Recompute Plex indices** (same action).

### Troubleshooting Plex logs

- **`failed to parse JSON response: 'syntax error' at 1:1`** (Plex `MetadataAgent`): almost always means the response body was **not JSON** — commonly **`index.html`** from the SPA fallback (first character `<`). That happens when the **built UI** is present and a **`/tv/...`** request did not match the Plex endpoints (wrong URL, missing **UrlBase** prefix, or a path collision with static files). TubeArr **does not** run static-file middleware or the HTML fallback for `/tv` (or `/api`). Confirm the provider URL Plex uses matches how TubeArr is exposed (including **Settings → URL Base** / reverse proxy). **`curl -sS -D- "http://<host>:<port>/tv"`** should return **`Content-Type: application/json`**, not `text/html`.
- **`Invalid metadata type (-1)`** for **`tv.plex.agents.none://`** items (Plex shows a numeric id, e.g. `93288`): that is Plex’s **internal** id for an item still on the **“None”** metadata agent (not yet tied to TubeArr’s scheme). Plex may call the custom provider with a **numeric** `ratingKey`. TubeArr only implements **`ch_` / `pl_` / `cst_` / `v_`** keys returned from **match** — it cannot map Plex’s numeric ids. Fix by getting a **successful match** (Fix Match / refresh) so items use **`tv.plex.agents.custom.tubearr`** and our rating keys.
- **`primary provider tv.plex.agents.custom.tubearr failed to return a result`**: Usually **404** on `GET /tv/library/metadata/{ratingKey}` because the key wasn’t recognized (same as above), or the channel/playlist/video id in the key is missing from the TubeArr DB. With **verbose** Plex logging enabled, TubeArr logs a specific warning when the rating key looks like a Plex-internal numeric id.

