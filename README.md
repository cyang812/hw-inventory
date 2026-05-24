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

Requires .NET 9 SDK, Node 20+, and (optionally) the EF Core CLI:

```bash
dotnet tool install --global dotnet-ef --version 9.*
```

Restore, migrate, seed, and run the backend:

```bash
cd backend
dotnet restore
dotnet ef database update --project HwInventory.Api
dotnet run --project HwInventory.Api -- --seed
dotnet run --project HwInventory.Api
```

The backend listens on `http://127.0.0.1:5080` by default:

- REST API:    `http://127.0.0.1:5080/api/...`
- Scalar UI:   `http://127.0.0.1:5080/scalar`
- OpenAPI doc: `http://127.0.0.1:5080/openapi/v1.json`
- gRPC:        `http://127.0.0.1:5080` (HTTP/2)
- MCP:         `http://127.0.0.1:5080/mcp`
- Health:      `http://127.0.0.1:5080/api/health`

Frontend:

```bash
cd frontend
npm install
npm run dev
```

## Configuration

Environment variables consumed by the backend:

| Variable        | Default                              | Notes                                                |
|-----------------|--------------------------------------|------------------------------------------------------|
| `DATABASE_URL`  | `Data Source=hw_inventory.db`        | Postgres conn string (or EF Core SQLite conn string) |
| `APP_ENV`       | `development`                        | `production` enforces `AUTH_TOKEN` + explicit bind   |
| `AUTH_TOKEN`    | _(unset in dev)_                     | Required when `APP_ENV=production`                   |
| `BIND_ADDRESS`  | `http://127.0.0.1:5080`              | Listening URL                                        |
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
