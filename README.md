# TubeArr

TubeArr is a self-hosted web app for managing YouTube channels: add channels, choose what to monitor, queue downloads with **yt-dlp**, and organize media on disk. The UI is a React app; the API and SQLite database are served by an ASP.NET Core backend.

## Quick Start (Docker)

Create a `docker-compose.yml`:

```yaml
services:
  tubearr:
    image: ghcr.io/tubearrteam/tubearr:latest
    container_name: tubearr
    restart: unless-stopped
    ports:
      - "5075:5075"
    volumes:
      - ./config:/config
      - /path/to/youtube:/downloads    # <-- change this
    environment:
      - TZ=America/New_York
      - ConnectionStrings__TubeArr=Data Source=/config/TubeArr.db
```

```bash
docker compose up -d
```

Open **http://localhost:5075** and configure:

1. **Settings → Download client** — yt-dlp path is `/usr/local/bin/yt-dlp` (pre-installed in the image)
2. **Settings → Media management** — set your root folder to `/downloads`
3. **Settings → YouTube** — optionally add a YouTube Data API key for faster metadata lookups
4. **Add a channel** and start downloading

The SQLite database and all config persist in `./config/`.

### Build from source

If you prefer to build locally:

```bash
git clone https://github.com/tubearrteam/TubeArr.git
cd TubeArr
docker compose up -d --build
```

## Requirements (bare metal)

- **.NET 8 SDK** (backend)
- **Node.js** and **npm** (frontend build and dev tooling)
- **yt-dlp** (downloads; configure the full path in the app)
- **ffmpeg** (used by yt-dlp for muxing/post-processing; install separately and ensure it is discoverable—often via `PATH` or yt-dlp’s `--ffmpeg-location` in your quality profile settings)

The default database is SQLite, stored in a file whose path comes from configuration (see [Configuration](#configuration)).

## Installation (bare metal)

1. Clone the repository and open a terminal at the repo root (where `package.json` is).

2. Install JavaScript dependencies:

   ```bash
   npm install
   ```

3. Choose how you want to run the app.

### Production-style run (single port)

From the repo root, install packages, build the frontend for production, then run the backend so it serves the API and static UI (default **http://localhost:5075/**):

```bash
npm install
npm run build:frontend
dotnet run --project backend/TubeArr.Backend.csproj --urls http://localhost:5075
```

On Windows, run the same commands in PowerShell or CMD.

### Development (hot reload + separate dev server)

Run **two** terminals from the repo root:

- **Backend** (API on port 5075):

  ```bash
  npm run dev:backend
  ```

- **Frontend** (webpack dev server on port **3000**, proxies API calls to the backend):

  ```bash
  npm run dev:frontend
  ```

Open **http://localhost:3000** in the browser.

Install **yt-dlp** and **ffmpeg** on your system (or any location outside the repo you prefer), then enter the **yt-dlp** executable path in **Settings → Download client / yt-dlp** (an absolute path works well).

## Configuration

### File-based settings (`appsettings.json`)

At the backend project, [backend/appsettings.json](backend/appsettings.json) controls startup defaults:

| Area | Purpose |
|------|--------|
| `ConnectionStrings:TubeArr` | SQLite connection string (default is a repo-relative database file name; see `appsettings` for the exact value). |
| `YouTube:ApiKey` | Optional bootstrap API key; the app also stores YouTube settings in the database (see below). |
| `TubeArr:UpdateCheckUrl` | Optional URL for application update checks. |
| `TubeArr:DownloadHistoryRetentionDays` | How long to retain download history (default 90). |

For local development, [backend/appsettings.Development.json](backend/appsettings.Development.json) points at a separate SQLite file when `ASPNETCORE_ENVIRONMENT=Development`.

You can override connection strings or keys using environment variables or user secrets (standard ASP.NET Core patterns).

### Settings in the web UI

After the app is running, use **Settings** in the sidebar. Important areas:

- **YouTube** — Data API key, whether to use the API, and which operations use the API (priority list). An API key improves reliability and speed for search, channel resolve, metadata, and listings; quota limits apply on Google’s side.
- **Download client / yt-dlp** — Path to the `yt-dlp` executable, optional cookies file for authenticated or age-restricted content, parallel queue workers, and a built-in test action. Configure **ffmpeg** location via your quality profile / yt-dlp options as needed.
- **Media management** — Root folders and naming rules so each channel has a disk location.
- **Quality profiles** — Format preferences and yt-dlp-related options used when enqueueing downloads.

### Database and backups

The SQLite file holds channels, videos, queue state, and settings. Use whatever backup strategy fits your host (file copy while the app is stopped, or your own snapshot tooling). The app may include backup/restore features under system settings—use those if present in your build.

## Adding a channel

1. Open **Add Channel** from the main navigation (or the add flow your build exposes).
2. Search or paste a **YouTube channel URL** / handle and select the correct result.
3. Set a **root folder** (and folder name if prompted) so TubeArr knows where files should live.
4. Assign a **quality profile** for downloads.
5. Set **monitoring** options before adding (see [Monitoring](#monitoring)). You can often trigger an initial search for missing or cutoff-unmet videos from the same flow.

After the channel exists, you can edit it from the channel page to change monitoring, filters, or paths.

## Monitoring

Monitoring controls which videos are tracked for future downloads and RSS/metadata updates—not every YouTube video is necessarily “wanted” on disk.

**Channel-level**

- **Monitored** — Master switch for the channel. If off, the channel is ignored for ongoing monitoring.
- **Monitor new items / presets** — Controls how new uploads and playlists are handled (e.g. all new videos vs specific playlists or manual picks). Use the channel editor and tooltips in the UI for the exact options your version exposes.
- **Round-robin (latest N videos)** — When set to a positive number, only the **N newest** videos stay monitored; older monitored videos can be unmonitored automatically. Useful for large channels where you only want a rolling window.
- **Filter out Shorts** — Skips YouTube Shorts so they are not monitored.
- **Filter out livestreams** — Skips live / archived live content when you do not want those in the library.

**Bulk actions**

- From the **channel index**, you can apply monitoring presets or round-robin settings to multiple channels at once (see bulk monitoring in the UI).

**Operational tips**

- Prefer configuring **yt-dlp** and **cookies** before relying on restricted or members-only content.
- If metadata or search feels slow or fails, check **YouTube** settings (API enabled, key valid, priority list) and API quota in Google Cloud Console.
- Use **System → Tasks / Queue** (or equivalent) to see scheduled work, download queue progress, and failures.

## Tests

From the repo root:

```bash
npm test
```

This runs the .NET test project under `backend/TubeArr.Backend.Tests/`.
