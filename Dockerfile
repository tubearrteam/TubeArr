# Multi-stage build for TubeArr (frontend + backend)

# --- Frontend build ---
FROM node:20 AS frontend-build
WORKDIR /app
COPY frontend ./frontend
COPY package.json package-lock.json ./
RUN npm install --legacy-peer-deps
RUN npm run build:frontend

# --- Backend build ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /src
COPY backend ./backend
COPY --from=frontend-build /app/_output/UI /src/backend/wwwroot
RUN dotnet publish backend/TubeArr.Backend.csproj -c Release -o /app/publish

# --- Runtime image ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

ARG YT_DLP_VERSION=2026.03.17

RUN apt-get update && apt-get install -y --no-install-recommends \
    python3 \
    ffmpeg \
    curl \
    && curl -L "https://github.com/yt-dlp/yt-dlp/releases/download/${YT_DLP_VERSION}/yt-dlp" -o /usr/local/bin/yt-dlp \
    && chmod a+rx /usr/local/bin/yt-dlp \
    && apt-get purge -y curl && apt-get autoremove -y \
    && apt-get clean && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=backend-build /app/publish .

RUN mkdir -p /config /downloads

VOLUME ["/config", "/downloads"]

EXPOSE 5075
ENV ASPNETCORE_URLS=http://+:5075

ENTRYPOINT ["dotnet", "TubeArr.Backend.dll"]
