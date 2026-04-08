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

# --- Runtime image ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime

ARG YT_DLP_VERSION=2026.03.17

RUN apk add --no-cache \
      python3 \
      ffmpeg \
      curl \
    && curl -L "https://github.com/yt-dlp/yt-dlp/releases/download/${YT_DLP_VERSION}/yt-dlp" -o /usr/local/bin/yt-dlp \
    && chmod a+rx /usr/local/bin/yt-dlp \
    && apk del curl

WORKDIR /app
COPY --from=backend-build /app/publish .
COPY --from=frontend-build /app/_output/UI /_output/UI

RUN addgroup -S tubearr && adduser -S -G tubearr -h /app -s /sbin/nologin tubearr \
    && mkdir -p /config /downloads \
    && chown -R tubearr:tubearr /app /config /downloads /_output

VOLUME ["/config", "/downloads"]

EXPOSE 5075
ENV ASPNETCORE_URLS=http://+:5075
ENV TubeArr__BundledFfmpegPath=/usr/bin/ffmpeg

USER tubearr

ENTRYPOINT ["dotnet", "TubeArr.Backend.dll"]
