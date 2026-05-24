# hw-inventory

A personal hardware inventory & project-idea tracker.

Track what hardware you own, what state it's in, what you've been doing with it,
and what idle gear is a good candidate for a new project. See [`plan.md`](./plan.md)
for the full design.

## Stack

| Layer    | Choice                                                                 |
|----------|------------------------------------------------------------------------|
| Backend  | C# / .NET 9, ASP.NET Core Minimal API                                  |
| REST docs| Scalar UI at `/scalar` (OpenAPI at `/openapi/v1.json`)                 |
| gRPC     | `Grpc.AspNetCore`, proto-first, same host/port as REST                 |
| MCP      | `ModelContextProtocol` C# SDK, Streamable HTTP at `/mcp`               |
| ORM      | EF Core 9 (SQLite for dev, Postgres for prod)                          |
| Frontend | Vue 3 + TypeScript + Vite, Pinia, Vue Router, Naive UI                 |
| Tests    | xUnit + `WebApplicationFactory<Program>`, vitest + `@vue/test-utils`   |

## Quick start (dev, SQLite)

Requires .NET 9 SDK and Node 20+. The EF Core CLI is only needed if you plan to
**author** new migrations (existing migrations auto-apply at startup):

```bash
dotnet tool install --global dotnet-ef --version 9.*
```

### Backend — empty DB (your own data)

```bash
cd backend
dotnet restore
dotnet run --project HwInventory.Api
```

That's it. On first start the API will:

1. Create `HwInventory.Api/hw_inventory.db` (SQLite file in the project folder).
2. Apply all EF Core migrations to bring the schema up to date.
3. Start listening on `http://127.0.0.1:5080`.

You now have an empty database ready for **real data**. Add it via any of:

- The SPA (`cd frontend && npm run dev`, then open the printed Vite URL).
- The REST API (Scalar UI at `/scalar`, or `curl` against `/api/hardware`, etc.).
- A bulk JSON import (see [Importing your own data](#importing-your-own-data) below).

> **Tip — pick where the DB file lives.** The default is relative to the
> backend project's working directory. Override with `DATABASE_URL`:
> ```bash
> # PowerShell
> $env:DATABASE_URL = "Data Source=D:\hwinv\hw_inventory.db"
> dotnet run --project HwInventory.Api
> ```
> ```bash
> # bash
> DATABASE_URL="Data Source=/var/lib/hwinv/hw_inventory.db" \
>   dotnet run --project HwInventory.Api
> ```

### Backend — demo data (for poking around)

If you'd rather start with the seeded demo set (a few hardware items, projects,
activities — useful for screenshots and trying the dashboard):

```bash
cd backend
dotnet run --project HwInventory.Api -- --seed   # seeds then exits
dotnet run --project HwInventory.Api             # then run normally
```

`--seed` is idempotent-ish: it inserts demo rows on top of whatever's already
there, so don't run it against a DB you care about. To restart from scratch,
stop the app and delete `HwInventory.Api/hw_inventory.db` (and the `-shm` /
`-wal` siblings if present).

### Endpoints (backend)

The backend listens on `http://127.0.0.1:5080` by default:

- REST API:    `http://127.0.0.1:5080/api/...`
- Scalar UI:   `http://127.0.0.1:5080/scalar`
- OpenAPI doc: `http://127.0.0.1:5080/openapi/v1.json`
- gRPC:        `http://127.0.0.1:5080` (HTTP/2)
- MCP:         `http://127.0.0.1:5080/mcp`
- Health:      `http://127.0.0.1:5080/api/health`

### Frontend

```bash
cd frontend
npm install
npm run dev
```

### Importing your own data

The API includes a bulk JSON import that mirrors the export shape, so you can
hand-author a small JSON file with your hardware and POST it to the server:

```bash
# export (also handy for backup or moving between dev/prod)
curl http://127.0.0.1:5080/api/export/json > my-inventory.json

# import (idempotent on categories/tags by slug/name; hardware always inserts)
curl -X POST -H "Content-Type: application/json" \
     --data-binary @my-inventory.json \
     http://127.0.0.1:5080/api/import/json
```

Minimum viable import payload — every top-level key is optional, and IDs are
remapped on insert so you can use any positive integers as long as they're
internally consistent across the file:

```json
{
  "categories": [
    { "id": 1, "name": "Single-board computers", "slug": "sbc" }
  ],
  "tags": [
    { "id": 1, "name": "lab" }
  ],
  "hardware": [
    {
      "id": 1,
      "name": "Raspberry Pi 5 (8 GB)",
      "manufacturer": "Raspberry Pi",
      "model": "RPi 5",
      "serialNumber": "1234-5678",
      "condition": "Working",
      "status": "Available",
      "acquiredAt": "2024-09-01T00:00:00Z",
      "cost": 80.00,
      "currency": "USD",
      "location": "lab shelf B",
      "notes": "Running Pi OS 12",
      "categoryIds": [1],
      "tagIds": [1]
    }
  ]
}
```

Enum values are case-insensitive. Valid `condition`: `Working` | `Partial` |
`Broken` | `Unknown`. Valid `status`: `Available` | `InUse` | `Loaned` |
`Archived` | `Sold` | `Lost`.

CSV export is available for spot-checking:
`/api/export/csv?entity=hardware|activities|projects`. There is intentionally no
CSV *import* — the shape is too lossy for nested relationships; use the JSON
import for round-trippable bulk data.

## Configuration

Environment variables consumed by the backend:

| Variable        | Default                              | Notes                                                |
|-----------------|--------------------------------------|------------------------------------------------------|
| `DATABASE_URL`  | `Data Source=hw_inventory.db`        | Postgres conn string (or EF Core SQLite conn string) |
| `APP_ENV`       | `development`                        | `production` enforces `AUTH_TOKEN` + explicit bind   |
| `AUTH_TOKEN`    | _(unset in dev)_                     | Required when `APP_ENV=production`                   |
| `BIND_ADDRESS`  | `http://127.0.0.1:5080`              | Listening URL (REST + SPA + MCP, HTTP/1.1)           |
| `GRPC_BIND_ADDRESS` | _(unset → gRPC disabled)_        | Optional separate HTTP/2-only port for live gRPC, e.g. `http://0.0.0.0:5081`. Needed because plain HTTP can't ALPN-negotiate; see plan §11. |
| `IDLE_DEFAULT_DAYS` | `90`                             | Threshold for "idle" hardware                        |

## Project layout

See [`plan.md` §6](./plan.md#6-repository-layout) for the full tree.

## Running tests

```bash
cd backend
dotnet test
```

```bash
cd frontend
npm test
```

## Docker

### Pull the prebuilt image from GHCR (recommended)

The image is built and published by GitHub Actions on every push to `main` for
both `linux/amd64` and `linux/arm64`. It bundles the API and the SPA — one
container, one port.

```bash
cp .env.example .env
# edit .env: set GHCR_OWNER, AUTH_TOKEN, HOST_PORT
docker compose pull
docker compose up -d
```

Then open `http://<host>:${HOST_PORT}` (default `:8080`). The SQLite DB lives at
`./data/hw_inventory.db` next to the compose file — back it up by copying that
single file (or via `sqlite3 ... .backup` while the service is live).

### Build locally instead of pulling

Either build the image directly:

```bash
docker build -t hw-inventory:dev .
docker run --rm -p 8080:5080 -v "$PWD/data":/data --env-file .env hw-inventory:dev
```

…or override the compose service to build from source:

```yaml
# compose.override.yml (gitignored)
services:
  app:
    build: .
```

### Upgrading & rollback

- Upgrade:   `docker compose pull && docker compose up -d`
- Rollback:  pin a specific tag in `.env` (`IMAGE_TAG=sha-abc1234`) and `pull && up -d`
- Versioned: tag a release with `git tag v0.1.0 && git push --tags` — the action publishes `:v0.1.0`, `:0.1`, and `:latest`.

### Image tags published

| Tag             | When                                  |
|-----------------|---------------------------------------|
| `:latest`       | every push to `main`                  |
| `:sha-<7>`      | every push and PR                     |
| `:v1.2.3`       | git tag `v1.2.3`                      |
| `:1.2`          | git tag `v1.2.x` (rolling minor)      |
