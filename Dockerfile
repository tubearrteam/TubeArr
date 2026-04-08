# Multi-stage build for TubeArr (frontend + backend)

# --- Frontend build ---
# node:20 keeps webpack memory use lower than 24.x on small buildx workers (OOM "Killed" seen with 24 + cold install).
FROM node:20 AS frontend-build
WORKDIR /app
COPY frontend ./frontend
COPY package.json package-lock.json ./
RUN npm install --legacy-peer-deps
RUN npm run build:frontend

# --- Backend build ---
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS backend-build
WORKDIR /src
COPY backend ./backend
COPY --from=frontend-build /app/_output/UI /src/backend/wwwroot
RUN dotnet publish backend/TubeArr.Backend.csproj -c Release -o /app/publish

# --- Deno (official Alpine image: glibc libs + dynamic linker for the deno binary) ---
FROM denoland/deno:alpine AS deno-upstream
RUN mkdir -p /export-lib && cp /lib/ld-linux*.so.* /export-lib/

# --- Runtime image ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime

ARG YT_DLP_VERSION=2026.03.17
# Must match yt-dlp's [project.optional-dependencies] default group for this YT_DLP_VERSION (see upstream pyproject.toml).
ARG YT_DLP_EJS_VERSION=0.8.0

# Runtime deps for yt-dlp + Deno (EJS): ffmpeg/ffprobe, optional mutagen + AtomicParsley (thumbnails),
# aria2c/curl/wget-style external downloaders, CA bundle + tzdata for HTTPS and logs.
# yt-dlp-ejs: explicit PyPI install (same pin as pip "yt-dlp[default]"); zipimport binary also bundles EJS scripts.
RUN apk add --no-cache \
      aria2 \
      atomicparsley \
      bash \
      ca-certificates \
      curl \
      ffmpeg \
      py3-mutagen \
      py3-pip \
      python3 \
      tzdata \
      wget \
    && curl -fsSL "https://github.com/yt-dlp/yt-dlp/releases/download/${YT_DLP_VERSION}/yt-dlp" -o /usr/local/bin/yt-dlp \
    && chmod a+rx /usr/local/bin/yt-dlp \
    && python3 -m pip install --no-cache-dir --break-system-packages "yt-dlp-ejs==${YT_DLP_EJS_VERSION}"

COPY --from=deno-upstream /bin/deno /usr/local/bin/deno
COPY --from=deno-upstream /usr/local/lib/glibc/ /usr/local/lib/glibc/
COPY --from=deno-upstream /export-lib/ /lib/
COPY --from=deno-upstream /lib64/ /lib64/

ENV DENO_DIR=/app/.deno \
    DENO_NO_UPDATE_CHECK=1 \
    DENO_NO_PROMPT=1

WORKDIR /app
COPY --from=backend-build /app/publish .
COPY --from=frontend-build /app/_output/UI /_output/UI

RUN addgroup -S tubearr && adduser -S -G tubearr -h /app -s /sbin/nologin tubearr \
    && mkdir -p /config /downloads /app/.deno \
    && chown -R tubearr:tubearr /app /config /downloads /_output

VOLUME ["/config", "/downloads"]

EXPOSE 5075
ENV ASPNETCORE_URLS=http://+:5075
ENV TubeArr__BundledFfmpegPath=/usr/bin/ffmpeg
ENV TubeArr__BundledYtDlpPath=/usr/local/bin/yt-dlp

USER tubearr

ENTRYPOINT ["dotnet", "TubeArr.Backend.dll"]
