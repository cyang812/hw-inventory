# hw-inventory — single image
#   stage 1: build the Vue SPA      → /fe/dist
#   stage 2: publish the .NET API   → /app/publish
#   stage 3: runtime, served on :5080, SQLite file at /data/hw_inventory.db
#
# Build stages pin to $BUILDPLATFORM so multi-arch images build on the host's
# native arch (no QEMU). Only the runtime stage is per-target-platform — it
# just copies files, so no compilation happens under emulation.

ARG NODE_IMAGE=node:20-alpine
ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:9.0
ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:9.0

# --------- stage 1: frontend ---------
FROM --platform=$BUILDPLATFORM ${NODE_IMAGE} AS frontend
WORKDIR /fe

COPY frontend/package.json frontend/package-lock.json* ./
RUN npm ci --no-audit --no-fund

COPY frontend/ ./
RUN npm run build

# --------- stage 2: backend ---------
FROM --platform=$BUILDPLATFORM ${DOTNET_SDK_IMAGE} AS backend
WORKDIR /src

COPY backend/HwInventory.sln ./backend/
COPY backend/HwInventory.Api/HwInventory.Api.csproj ./backend/HwInventory.Api/
COPY backend/HwInventory.Tests/HwInventory.Tests.csproj ./backend/HwInventory.Tests/
COPY proto/ ./proto/
RUN dotnet restore backend/HwInventory.Api/HwInventory.Api.csproj

COPY backend/ ./backend/
RUN dotnet publish backend/HwInventory.Api/HwInventory.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# --------- stage 3: runtime (per-arch) ---------
FROM ${DOTNET_RUNTIME_IMAGE} AS runtime
WORKDIR /app

# Curl is handy for the HEALTHCHECK and for poking the API from inside the container.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

COPY --from=backend  /app/publish ./
COPY --from=frontend /fe/dist     ./wwwroot/

# Persisted data lives outside the image so upgrades don't touch the DB file.
RUN mkdir -p /data
VOLUME ["/data"]

ENV BIND_ADDRESS=http://0.0.0.0:5080 \
    DATABASE_URL="Data Source=/data/hw_inventory.db" \
    APP_ENV=production \
    IDLE_DEFAULT_DAYS=90 \
    DOTNET_NOLOGO=1 \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 5080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -fsS http://127.0.0.1:5080/api/health || exit 1

ENTRYPOINT ["dotnet", "HwInventory.Api.dll"]
