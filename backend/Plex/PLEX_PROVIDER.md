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

If **`tubearr-*.log`** still shows no Plex lines, check **`logs/plex-http.log`**: it is appended on every HTTP request whose path contains `/tv` (no Serilog filtering). A line on startup records the content root and log file path. If **`plex-http.log`** is empty, Plex is not reaching this host URL (firewall, wrong port, or provider URL in Plex). If **`enabled=false`** appears in warnings, enable the provider: `PUT /api/v1/config/plex-provider` with `"enabled": true` (or the Settings UI).

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
- season: `pl_<playlistId>`
- episode: `v_<videoId>`

### Recommended library naming

TubeArr’s naming pipeline should keep the YouTube IDs embedded in folder/file names so matching is deterministic:

`<Channel Name> [<channelId>]/Season XX/<Channel Name> - sXXeYYY - <Sanitized Video Title> [<videoId>].ext`

### How matching works (high level)

Plex’s match `POST` commonly includes **`filename`** (often a relative path to a media file). **`ExtractMatchPath`** prefers **`filename`** over **`path`** / **`Media[].Part[].file`**.

**Filename / path is treated as authoritative** for identity whenever present: **`[UC…]`**, configured **`Channel.Path`** prefix, show folder name (skipping `Season NN`), then Plex’s **`guid`** / titles. That order helps **automatic** matching when Plex’s titles or keys disagree with your on-disk layout.

- **Show** (`type` 2): `[UC…]` / channel path / folder name from the path hint, then `guid` (`…/show/ch_…`), then case-insensitive `title`.
- **Season** (`type` 3): same path-first channel resolution, then `parentRatingKey` / `parentGuid`, then **`parentTitle`** + `parentGuid`. Season 01 is “channel uploads”.
- **Episode** (`type` 4): **`[videoId]`** in filename/path; then **`VideoFiles`** by exact path or **by filename suffix** when Plex’s path differs from the DB path; then channel from **path** before **`grandparentRatingKey`** / **`grandparentTitle`** + `grandparentGuid`; then **`date`** / **`originallyAvailableAt`** only when `parentIndex` or `index` is missing/zero; then stable `PlexSeasonIndex` / `PlexEpisodeIndex`. **Episode titles** in metadata use `Video.Title` when set; otherwise **`{basename}.nfo`** next to the primary media file (Kodi-style `<episodedetails><title>…</title>` as written by TubeArr’s NFO export); otherwise the **primary `VideoFiles.Path`** is parsed for TubeArr-style names (`… - sXXeYY - Title [id]`, `YYYYMMDD - Title [id]`) so Plex isn’t stuck on “Episode N” after auto-match when the DB title was never filled.

### Stable numbering (important)

TubeArr persists:

- `Playlists.SeasonIndex` (per channel; **assigned once** and never reused)
- `Videos.PlexSeasonIndex` / `Videos.PlexEpisodeIndex` (**assigned once** based on TubeArr primary playlist mapping + stable playlist order)

This makes Plex matching deterministic and prevents renumber drift when playlists reorder or change activity.

### Troubleshooting Plex logs

- **`failed to parse JSON response: 'syntax error' at 1:1`** (Plex `MetadataAgent`): almost always means the response body was **not JSON** — commonly **`index.html`** from the SPA fallback (first character `<`). That happens when the **built UI** is present and a **`/tv/...`** request did not match the Plex endpoints (wrong URL, missing **UrlBase** prefix, or a path collision with static files). TubeArr **does not** run static-file middleware or the HTML fallback for `/tv` (or `/api`). Confirm the provider URL Plex uses matches how TubeArr is exposed (including **Settings → URL Base** / reverse proxy). **`curl -sS -D- "http://<host>:<port>/tv"`** should return **`Content-Type: application/json`**, not `text/html`.
- **`Invalid metadata type (-1)`** for **`tv.plex.agents.none://`** items (Plex shows a numeric id, e.g. `93288`): that is Plex’s **internal** id for an item still on the **“None”** metadata agent (not yet tied to TubeArr’s scheme). Plex may call the custom provider with a **numeric** `ratingKey`. TubeArr only implements **`ch_` / `pl_` / `v_`** keys returned from **match** — it cannot map Plex’s numeric ids. Fix by getting a **successful match** (Fix Match / refresh) so items use **`tv.plex.agents.custom.tubearr`** and our rating keys.
- **`primary provider tv.plex.agents.custom.tubearr failed to return a result`**: Usually **404** on `GET /tv/library/metadata/{ratingKey}` because the key wasn’t recognized (same as above), or the channel/playlist/video id in the key is missing from the TubeArr DB. With **verbose** Plex logging enabled, TubeArr logs a specific warning when the rating key looks like a Plex-internal numeric id.

