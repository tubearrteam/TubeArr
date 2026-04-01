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
COPY --from=frontend-build /app/frontend/build /src/backend/wwwroot
RUN dotnet publish backend/TubeArr.Backend.csproj -c Release -o /app/publish

# --- Runtime image ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=backend-build /app/publish .
EXPOSE 5075
ENV ASPNETCORE_URLS=http://+:5075
ENTRYPOINT ["dotnet", "TubeArr.Backend.dll"]
