# AGENTS.md

## Cursor Cloud specific instructions

### Project overview

TubeArr is a self-hosted YouTube channel manager (Sonarr/Radarr style). Single repo with an ASP.NET Core 8 backend (C#) and a React 19 + TypeScript frontend built with Webpack 5. SQLite database (embedded, auto-created). See `README.md` for full feature list.

### Prerequisites

- **.NET 8 SDK** — backend build/run/test
- **Node.js 24** (per `.nvmrc`) — frontend build and dev tooling
- **npm** with `legacy-peer-deps=true` (already set in `.npmrc`)

### Running the dev environment

Two-process dev mode (hot reload on both sides):

```bash
# Terminal 1 — Backend API on port 5075
npm run dev:backend

# Terminal 2 — Webpack dev server on port 3000 (proxies /api and /signalr to backend)
npm run dev:frontend
```

Access the app at `http://localhost:3000` during development. The backend alone at `http://localhost:5075` serves the last production build from `_output/UI/`.

### Key commands

| Task | Command |
|------|---------|
| Install deps | `npm install && dotnet restore TubeArr.sln` |
| Build frontend | `npm run build:frontend` |
| Build backend | `dotnet build backend/TubeArr.Backend.csproj` |
| Run tests | `npm test` |
| Dev backend | `npm run dev:backend` |
| Dev frontend | `npm run dev:frontend` |

### Test notes

- `npm test` runs `dotnet test` on the xUnit test project (backend/TubeArr.Backend.Tests).
- 4 tests fail on Linux by design: 3 are Windows-path parsing tests (`PlexFilenameParserTests.show_folder_name_from_path` with `D:\` paths) and 1 requires yt-dlp installed (`YtDlpCookiesPathResolverTests`). These are pre-existing platform-specific failures.
- There are no frontend-specific test commands.

### Gotchas

- Node.js must be v24+ (`.nvmrc` says `24`). If using nvm: `nvm use 24`.
- `.npmrc` sets `legacy-peer-deps=true` — do not remove this, peer dep resolution will fail without it.
- The SQLite DB is auto-created at `backend/TubeArr.dev.db` in dev mode. No migrations command needed — EF Core runs them on startup.
- `yt-dlp` and `ffmpeg` are optional for UI/API development. They are only needed for actual video downloading. System Status page will show health warnings without them.
- The webpack dev server proxies `/api` and `/signalr` to the backend at `http://localhost:5075` (configurable via `TUBEARR_BACKEND_URL` env var).
