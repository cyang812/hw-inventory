# hw-inventory — Plan

A personal hardware inventory & project-idea tracker.
Goal: know **what hardware I own**, **what state it's in**, **what I've been doing with it**,
and **what idle gear is a good candidate for a new idea/project** (new FW for an MCU, new OS
on a phone, repurposing a laptop, etc.).

---

## 1. Vision & scope

The **individual hardware item** is the unit of truth. Everything else — categories, tags,
activities, configs, loans, projects — hangs off it. A single item can sit in multiple
classification buckets at once: a Raspberry Pi 4 is both an `sbc` and an `arm-cortex-a72`
board; a Dell XPS is both a `laptop` and an `x86_64` machine. The app must let you slice,
filter, and find an individual item by any of its axes independently.

**In scope**
- Track individual hardware items, each addressable and queryable on its own.
- Classify each item along two **M:N** axes:
  - **Category** — curated taxonomy. Covers both hardware-class buckets
    (`mcu`, `sbc`, `laptop`, `phone`, `ssd`, `dev-kit`, …) **and** CPU-platform buckets
    (`arm-cortex-a72`, `riscv-rv32imac`, `xtensa-lx7`, `x86_64`, `apple-m1`, …) in one
    flat list. `parent_id` is reserved for future grouping ("CPU Platform" /
    "Hardware Class") but the UI shows it flat until there's a reason to group.
  - **Tag** — free-form, user-coined labels (`hobby`, `work`, `gpio`, `low-power`, …).
- Free-form `specs` per item (JSON) plus a few structured identifier fields
  (`sku`, `asset_tag`, `revision`, `identifiers` JSON for MAC/IMEI/hostname/…).
- Track **activities** ("flashed firmware", "used as host", "repaired") on a timeline.
  Each activity kind declares whether it updates `last_used_at`, `last_activity_at`, or both —
  so moving an item to a drawer never makes it look "recently used".
- Track **firmware / OS / configuration history** per hardware.
- Track **projects/ideas** linked to one or more pieces of hardware, with status
  (`idea → planned → in_progress → paused → done → abandoned`).
- Track **loans** — when an item leaves your possession and when it should come back.
- External **links** per hardware (datasheet, vendor page, GitHub repo, manual).
- Dashboard with: idle gear, project board, simple stats, "suggestion" panel
  (idle hardware with no active project), overdue loans.
- **Soft-delete** for Hardware and Project (`archived_at` + lifecycle status).
- JSON / CSV **import & export** via API endpoints (not raw DB file download).
- Quick-action toolbar on Hardware detail: *Mark used today*, *Move location*,
  *Link to project*, *Archive / sold / lost*.
- Local-first: SQLite dev, Postgres prod; web UI in the browser.
- API-first: REST + gRPC + MCP so any client (browser, CLI, AI agent, mobile) talks the
  same service layer without rewriting logic.
- **MCP server** at `/mcp` — AI agents can query the inventory and perform safe,
  intent-shaped writes (log activity, record firmware, link project, manage loans).

**Out of scope / non-goals**
- Multi-user accounts — single-user with optional bearer token when deployed.
- File attachment **upload UI** — schema is in place; upload workflow is deferred.
- Structured `Location` table — free-text for now; `location_id` reserved for later.
- Sync between multiple instances.
- Barcode / QR scanning workflows.
- Public sharing.
- Category hierarchy UI — `parent_id` exists on `Category` but the UI stays flat
  until the list is large enough to warrant grouping.

---

## 2. Stack & tooling

| Layer        | Choice                                                                                              |
|--------------|-----------------------------------------------------------------------------------------------------|
| Backend      | **C# / .NET 9**, **ASP.NET Core Minimal API**                                                       |
| REST docs    | **Scalar** UI (`.NET 9` built-in OpenAPI + `Scalar.AspNetCore`) at `/scalar`                        |
| gRPC         | **`Grpc.AspNetCore`** (proto-first); used by CLI & future mobile/desktop clients                    |
| MCP          | **`ModelContextProtocol`** (official C# SDK) — Streamable HTTP transport at `/mcp`, exposes a curated tool/resource surface for AI agents |
| ORM          | **EF Core 9** with **Npgsql** (Postgres prod) / **Microsoft.EntityFrameworkCore.Sqlite** (dev)      |
| Migrations   | **EF Core Migrations** (`dotnet ef migrations add / database update`)                               |
| DB (dev)     | **SQLite** (`./hw_inventory.db`)                                                                    |
| DB (prod)    | **Postgres** via `DATABASE_URL` env var                                                             |
| .NET deps    | **NuGet** via `dotnet restore`; single `.csproj` (or minimal multi-project solution)                |
| Frontend     | **Vue 3 + TypeScript + Vite**, **Pinia**, **Vue Router**, **Naive UI**                              |
| API client   | `openapi-typescript` (type-only `.d.ts` from OpenAPI schema) + hand-written typed `fetch` wrapper   |
| Lint/format  | `dotnet format` + `.editorconfig` (backend); `eslint` + `prettier` (frontend)                      |
| Tests        | **xUnit** + **`WebApplicationFactory<Program>`** (backend integration); **vitest** + `@vue/test-utils` (frontend) |
| Container    | Single multi-stage `Dockerfile` (backend + built SPA), published to **GHCR** by GitHub Actions as a multi-arch (`amd64`+`arm64`) image; used for deployment, not for day-to-day dev. See §10. |

Rationale:
- **ASP.NET Core Minimal API** gives low-ceremony endpoint definitions, first-class OpenAPI
  support in .NET 9 (no extra package needed), and excellent performance.
- **gRPC** (`Grpc.AspNetCore`) runs on the same port as REST (HTTP/2 + HTTP/1.1 via
  `UseRouting`). The browser UI uses REST; the Phase 3 CLI and any future mobile/desktop
  client use gRPC for strongly-typed, efficient calls. `.proto` files live in `proto/`.
- **MCP server** (`ModelContextProtocol` C# SDK) lives in the same process and is mounted
  at `/mcp` using the **Streamable HTTP** transport. AI agents (VS Code Copilot, Claude.ai,
  custom clients) can query the inventory and perform safe, intent-shaped writes
  (`mark_used_today`, `log_activity`, …) through the same `Services/*` code path as REST
  and gRPC.
- **EF Core 9** with `IDesignTimeDbContextFactory` lets the same migration path target both
  SQLite (dev) and Postgres (prod) — the JSON column type (`JsonDocument` / `string`) is
  handled via a value converter so it works on both.
- **Naive UI** is a mature Vue 3 component lib (DataTable, Form, Tag, Drawer, Modal) and
  is MIT-licensed; frontend is intentionally backend-language-agnostic.

---

## 3. Data model

### Entities

```text
Category(id, name, slug, parent_id?, icon?, description)
Tag(id, name, color?)

Hardware(
    id, name,
    manufacturer, model,
    serial_number?, sku?, asset_tag?, revision?,
    identifiers (JSON: {mac?, imei?, hostname?, service_tag?, ...}),
    specs (JSON),
    links (JSON array of {label, url, kind?}),       -- datasheet, vendor, repo, manual
    acquired_at?, purchased_from?, purchase_url?,
    cost?, currency?, warranty_expires_at?,
    location?,                                       -- free text in v1
    condition (enum), status (enum),
    notes (markdown),
    last_used_at?,                                   -- updated only by "usage" activities
    last_activity_at?,                               -- updated by ANY activity
    archived_at?,                                    -- soft delete
    created_at, updated_at
)
HardwareCategory(hardware_id, category_id)                        -- M:N
                                                                  -- UNIQUE(hardware_id, category_id)
HardwareTag(hardware_id, tag_id)                                  -- M:N
                                                                  -- UNIQUE(hardware_id, tag_id)
Project(
    id, title, slug, description (markdown), status (enum),
    priority (enum), started_at?, target_date?, completed_at?,
    notes (markdown), links (JSON array of {label, url}),
    archived_at?,                                    -- soft delete
    created_at, updated_at
)
HardwareProject(hardware_id, project_id, role?, created_at)       -- M:N
                                                                  -- UNIQUE(hardware_id, project_id)

Activity(
    id, hardware_id, project_id?, kind (enum),
    description, metadata (JSON),                    -- e.g. {firmware_version, from, to, ...}
    occurred_at, created_at
)

HardwareConfig(                                      -- firmware/OS/bootloader history
    id, hardware_id, kind (enum: firmware|os|bootloader|config),
    name, version?, notes?,
    installed_at?, is_current,
    activity_id?,                                    -- optional link to the flashing activity
    created_at
)

Loan(
    id, hardware_id, loaned_to, loaned_at,
    due_at?, returned_at?, notes,
    created_at
)

Attachment(                                          -- schema in v1, upload UI in phase 2
    id, hardware_id?, project_id?, activity_id?,
    storage_backend (enum: local|s3), storage_key,   -- relative key, never absolute path
    original_filename, mime_type, size_bytes, sha256?,
    label?, created_at
)
```

### Enums

- `Hardware.condition`: `working | partial | broken | unknown`
- `Hardware.status`:    `available | in_use | loaned | archived | sold | lost`
  *(no `idle` — "idle" is a derived filter on `last_used_at`, not a stored state)*
- `Project.status`:     `idea | planned | in_progress | paused | done | abandoned`
- `Project.priority`:   `low | medium | high`
- `Activity.kind`:      `used | flashed | repaired | measured | configured | inspected | moved | note`
  - Updates **both** `last_used_at` and `last_activity_at`: `used`, `flashed`, `repaired`,
    `measured`, `configured`.
  - Updates **only** `last_activity_at`: `inspected`, `moved`, `note`.
- `HardwareConfig.kind`: `firmware | os | bootloader | config`

### Derived behaviour

- `last_used_at` / `last_activity_at`:
  - Stored columns (indexed for fast sorts/filters).
  - Maintained in a **service-layer hook** on activity create/update/delete (works for both
    SQLite and Postgres).
  - For "I forgot to log it", users create a **backdated** Activity rather than editing
    the timestamp directly. This keeps the timeline truthful.
- **Idle hardware**:
  `status IN ('available','in_use') AND archived_at IS NULL
   AND COALESCE(last_used_at, created_at) < now() - :days`  (default 90).
- **Suggestion**: idle hardware with **no** project in status `idea|planned|in_progress`.
- **Overdue loan**: `returned_at IS NULL AND due_at < now()`.

### Notes on design choices

- `specs` is intentionally a JSON blob in v1 (different shape per category — MCU has flash/RAM,
  laptop has CPU/RAM/SSD). Per-category templates can be layered on later without migration.
- `Category` is **M:N** with Hardware (a single item can be both a hardware-class category
  like `sbc` and a CPU-platform category like `arm-cortex-a72`). `Category.parent_id` is
  reserved for future grouping ("CPU Platform" / "Hardware Class"); v1 UI shows it flat.
- `Activity` is a single timeline table rather than separate tables per kind. `metadata` JSON
  carries kind-specific payload (e.g., flashed firmware version, measured value, move from→to).
- `HardwareConfig.is_current` is a denormalized flag for the common "what's on it right now?"
  query; enforced by service code (only one current row per `(hardware_id, kind)`).
- `Attachment.storage_key` is a *relative* key (e.g., `hardware/123/photo.jpg`); the resolver
  joins with `ATTACHMENTS_DIR` (or an S3 bucket later). Never store absolute paths.
- All M:N tables get explicit `UNIQUE` constraints so link operations are safely idempotent.

### EF Core / C# mapping notes

These pin down the implementation-level choices the schema above leaves open. None of
them change the conceptual model — they make sure the implementation is consistent.

- **Primary keys** — `int` auto-increment on every entity (`Id` column). Short URLs,
  smallest storage, simplest EF Core default.
- **Naming** — entity properties are PascalCase in C# (`LastUsedAt`, `SerialNumber`),
  but DB columns stay snake_case (matching the schema above) via
  `UseSnakeCaseNamingConvention()` from the `EFCore.NamingConventions` NuGet package.
  REST DTOs are serialised camelCase (`lastUsedAt`) by `System.Text.Json`'s default policy.
- **Enums** — stored as **strings** (`HasConversion<string>()`), not ints. Keeps the DB
  readable, makes enum reorderings safe, and produces clean string enums in OpenAPI.
- **JSON columns** (`Hardware.identifiers`, `Hardware.specs`, `Hardware.links`,
  `Project.links`, `Activity.metadata`) — typed as `JsonDocument` on the entity, mapped
  via EF Core's built-in JSON support: `jsonb` on Postgres, `TEXT` on SQLite. A single
  value-converter handles both providers. v1 keeps these free-form; per-category typed
  shapes can be layered on later with `OwnsOne` without a migration if needed.
- **Timestamps** — `DateTimeOffset` (always UTC) everywhere. Stored as `timestamptz`
  on Postgres, ISO-8601 `TEXT` on SQLite. Frontend renders in local time.
- **`UpdatedAt` auto-set** — `AppDbContext.SaveChangesAsync` is overridden to stamp
  `UpdatedAt = DateTimeOffset.UtcNow` on every tracked entity in `Modified` state.
- **Soft delete** — EF Core global query filter
  (`modelBuilder.Entity<Hardware>().HasQueryFilter(h => h.ArchivedAt == null)`) on
  `Hardware` and `Project`. Endpoints honour `?include_archived=true` by calling
  `.IgnoreQueryFilters()`.
- **M:N junctions** — `HardwareCategory` and `HardwareTag` are implicit join tables
  (EF Core "skip navigations": `ICollection<Category> Categories` on `Hardware`).
  `HardwareProject` is an **explicit** join entity because it carries `role` and
  `created_at`.
- **Uniqueness** — `Category.slug`, `Tag.name`, and `Project.slug` are UNIQUE; the M:N
  junctions each have a composite UNIQUE on `(hardware_id, category_id)` /
  `(hardware_id, tag_id)` / `(hardware_id, project_id)`.
- **DTOs vs entities** — Minimal API endpoints accept and return DTO records (in
  `Dtos/`), never the raw EF entity. Mapping is hand-written for v1 (small surface;
  no need for AutoMapper/Mapperly yet).
- **`last_used_at` / `last_activity_at` sync** — implemented in `Services/ActivityService`
  (called by both REST and gRPC endpoints). Keeps the rule in one place regardless of
  transport. SQLite and Postgres run the same C# code path.

---

## 4. API surface (REST + gRPC + MCP)

The same domain is exposed over **three transports**:
- **REST/JSON** at `/api/*` — the primary transport for the browser UI.
- **gRPC** at the same host/port (HTTP/2) — full mirror of REST, used by the Phase 3 CLI
  and any future mobile/desktop client.
- **MCP (Model Context Protocol)** at `/mcp` (Streamable HTTP) — a curated tool/resource
  surface for AI agents. Reads are broad; writes are limited to safe, intent-shaped
  operations.

All three transports call the same service layer (`Services/*`) so there is exactly one
implementation of every business rule (`last_used_at` sync, soft-delete, idle detection,
link idempotency, etc.).

ASP.NET Core .NET 9 auto-generates the OpenAPI document at `/openapi/v1.json`; **Scalar**
renders it at `/scalar`.

### Conventions (apply to every REST list endpoint)
- Pagination: `?limit=50&offset=0` (max `limit=200`).
- Sort: `?sort=field` or `?sort=-field` (descending).
- Search: `?q=<text>` (server decides which fields to match).
- Time filters: `?created_after=`, `?created_before=`, `?updated_after=`, `?updated_before=`.
- List response shape:
  ```json
  { "items": [...], "total": 123, "limit": 50, "offset": 0 }
  ```
- **PATCH semantics** — `PATCH` endpoints accept **JSON Patch** (RFC 6902,
  `application/json-patch+json`) via `Microsoft.AspNetCore.JsonPatch`. This avoids the
  "is `null` clear-or-unchanged?" ambiguity and lets clients express add/remove/replace
  unambiguously. Each PATCH endpoint validates the patch document against a
  per-entity allow-list of paths before applying.
- Link/unlink endpoints are **idempotent** — use `PUT` for link, `DELETE` for unlink.
  Re-linking returns the existing link (with optional `role` update via the same `PUT` body).
- Soft delete: `DELETE /api/hardware/{id}` sets `archived_at`; `?include_archived=true`
  on list endpoints brings them back. `DELETE /api/hardware/{id}?hard=true` is reserved for
  admin/CLI use only.

### REST endpoints

```
GET    /api/health

GET    /api/categories                              POST /api/categories
PATCH  /api/categories/{id}                         DELETE /api/categories/{id}
GET    /api/tags                                    POST /api/tags
DELETE /api/tags/{id}

GET    /api/hardware?category=&category=&tag=&tag=&status=&condition=&q=&idle_days=
                    &include_archived=&limit=&offset=&sort=
                                  # category & tag are repeatable; multi-values are AND
                                  # (e.g. category=sbc&category=arm-cortex-a72)
POST   /api/hardware
GET    /api/hardware/{id}
PATCH  /api/hardware/{id}
DELETE /api/hardware/{id}                           # soft delete by default

GET    /api/hardware/{id}/activities?kind=&project_id=
                                    &occurred_after=&occurred_before=&limit=&offset=
POST   /api/hardware/{id}/activities

GET    /api/hardware/{id}/projects
PUT    /api/hardware/{id}/projects/{pid}            # idempotent link, body: {role?}
DELETE /api/hardware/{id}/projects/{pid}

GET    /api/hardware/{id}/configs?kind=&current=
POST   /api/hardware/{id}/configs                   # records new firmware/OS/etc.
PATCH  /api/hardware/{id}/configs/{cid}
DELETE /api/hardware/{id}/configs/{cid}

GET    /api/hardware/{id}/loans
POST   /api/hardware/{id}/loans                     # start loan
PATCH  /api/hardware/{id}/loans/{lid}               # return / extend

GET    /api/projects?status=&priority=&q=&include_archived=&limit=&offset=&sort=
POST   /api/projects
GET    /api/projects/{id}
PATCH  /api/projects/{id}
DELETE /api/projects/{id}                           # soft delete
GET    /api/projects/{id}/hardware
PUT    /api/projects/{id}/hardware/{hid}            # idempotent link
DELETE /api/projects/{id}/hardware/{hid}

GET    /api/activities?hardware_id=&project_id=&kind=
                      &occurred_after=&occurred_before=&limit=&offset=

# Dashboard endpoints are thin wrappers around the same service functions
# used by the list endpoints — single source of truth for queries.
GET    /api/dashboard/stats
GET    /api/dashboard/idle?days=90&limit=&offset=
GET    /api/dashboard/suggestions?limit=&offset=
GET    /api/dashboard/overdue-loans

# Import/export (v1.5)
GET    /api/export/json
GET    /api/export/csv?entity=hardware|activities|projects
POST   /api/import/json
POST   /api/import/csv?entity=...                   # multipart upload
```

### gRPC mirror

`proto/hardware.proto` (package `hwinventory.v1`) defines a **full mirror** of the REST
surface — every REST endpoint has a gRPC equivalent. The proto is the source of truth for
the gRPC contract; REST and gRPC handlers both delegate to the same service layer.

> **Implementation status:** as of phase 2 only `HealthService` is wired up server-side;
> the other 7 services are declared in the proto but return `Unimplemented` at runtime
> until phase 3 lands the CLI client. See §11 for the full status table, integration
> tests, and the per-service rollout recipe.

**Services** (one `service` block per REST resource group):

| Service                | Mirrors                          |
|------------------------|----------------------------------|
| `HealthService`        | `GET /api/health`                |
| `CategoryService`      | `/api/categories/*`              |
| `TagService`           | `/api/tags/*`                    |
| `HardwareService`      | `/api/hardware/*` (CRUD + links + activities + configs + loans sub-resources) |
| `ProjectService`       | `/api/projects/*`                |
| `ActivityService`      | `/api/activities` cross-cutting list |
| `DashboardService`     | `/api/dashboard/*`               |
| `ImportExportService`  | `/api/import/*`, `/api/export/*` |

**Method naming** — proto methods follow the canonical CRUD verbs so they map
mechanically to REST:

| REST                                        | gRPC method                        |
|---------------------------------------------|------------------------------------|
| `GET /api/hardware`                         | `HardwareService.List`             |
| `GET /api/hardware/{id}`                    | `HardwareService.Get`              |
| `POST /api/hardware`                        | `HardwareService.Create`           |
| `PATCH /api/hardware/{id}`                  | `HardwareService.Update`           |
| `DELETE /api/hardware/{id}`                 | `HardwareService.Archive` (soft)   |
| `DELETE /api/hardware/{id}?hard=true`       | `HardwareService.Delete` (hard)    |
| `POST /api/hardware/{id}/activities`        | `HardwareService.LogActivity`      |
| `PUT /api/hardware/{id}/projects/{pid}`     | `HardwareService.LinkProject`      |
| `DELETE /api/hardware/{id}/projects/{pid}`  | `HardwareService.UnlinkProject`    |
| `POST /api/hardware/{id}/configs`           | `HardwareService.RecordConfig`     |
| `POST /api/hardware/{id}/loans`             | `HardwareService.StartLoan`        |
| `PATCH /api/hardware/{id}/loans/{lid}`      | `HardwareService.UpdateLoan`       |

**Conventions for the gRPC surface:**
- **PATCH equivalent** — `Update*` requests carry a `google.protobuf.FieldMask update_mask`
  alongside the entity message; only masked fields are written. (gRPC's native answer to
  the JSON-Patch question on the REST side.)
- **List requests** carry a single `ListXxxRequest` message with `int32 limit`,
  `int32 offset`, `string sort`, plus the same filter fields the REST endpoint accepts;
  responses are `ListXxxResponse { repeated Xxx items; int32 total; int32 limit; int32 offset; }`.
- **Timestamps** use `google.protobuf.Timestamp` (UTC).
- **JSON-blob fields** (`Hardware.specs`, `Hardware.identifiers`, `Activity.metadata`, …)
  use `google.protobuf.Struct` so the same free-form shape works on both transports.
- **Enums** are proto enums with a `_UNSPECIFIED = 0` zero value; the server rejects
  unspecified values on writes.
- **Errors** map REST status codes to gRPC `Status` codes:
  `400 → INVALID_ARGUMENT`, `401 → UNAUTHENTICATED`, `403 → PERMISSION_DENIED`,
  `404 → NOT_FOUND`, `409 → ALREADY_EXISTS` (link idempotency returns OK, not 409),
  `422 → FAILED_PRECONDITION`, `500 → INTERNAL`.
- **Streaming** — `ImportExportService.ExportJson` and `ExportCsv` return a
  **server-streaming** response of `bytes` chunks; `ImportJson` / `ImportCsv` accept a
  **client-streaming** request. Keeps large dumps off the heap on both ends.
- **Reflection** — `app.MapGrpcReflectionService()` is enabled in Development so `grpcurl`
  and similar tools can introspect without the `.proto` file.
- **Versioning** — the proto package is `hwinventory.v1`; any breaking change moves to
  `v2` and runs alongside `v1` for one release.

**Single source of truth** — REST endpoint handlers in `Endpoints/*`, gRPC service
classes in `Grpc/*`, and MCP tools/resources in `Mcp/*` are thin: each unwraps its
transport-specific request, calls the corresponding `Services/*` method, and re-packs
the result. No business logic lives in any transport adapter.

### MCP server

Mounted at `/mcp` via the **Streamable HTTP** transport (`app.MapMcp("/mcp")` from the
`ModelContextProtocol` C# SDK). Designed for AI agents (VS Code Copilot, Claude.ai, custom
clients) to answer questions like *"what hardware have I not touched in 6 months?"*,
*"what idle gear could run RISC-V code?"*, and to record activities the user mentions
in chat (*"I just flashed Zephyr on the nRF52840"*).

**Scope** — read everything; writes limited to **safe, intent-shaped** actions. No
destructive operations (delete/archive/hard-delete), no taxonomy mutation
(create/rename/delete Category or Tag), no bulk import/export, no unlink. Agents that need
those go through REST or the CLI.

**Tools** (`[McpServerTool]` methods, grouped by intent):

| Tool                       | Verb  | Notes                                                            |
|----------------------------|-------|------------------------------------------------------------------|
| `list_hardware`            | read  | All Hardware list filters (category[], tag[], status, condition, idle_days, q, include_archived, pagination, sort) |
| `get_hardware`             | read  | Full detail incl. categories, tags, recent activities, current configs, active loan |
| `list_idle_hardware`       | read  | Wraps `/api/dashboard/idle`                                      |
| `list_suggestions`         | read  | Idle hardware with no active project                             |
| `list_overdue_loans`       | read  | Wraps `/api/dashboard/overdue-loans`                             |
| `get_dashboard_stats`      | read  | Counts by category / condition / status                          |
| `list_projects`            | read  | All Project list filters                                         |
| `get_project`              | read  | Full detail incl. linked hardware                                |
| `list_activities`          | read  | Cross-cutting activity timeline with filters                     |
| `mark_used_today`          | write | Logs a `used` activity for `hardware_id` at now                  |
| `log_activity`             | write | Generic activity create (kind, description, occurred_at?, project_id?, metadata?) |
| `link_project`             | write | Idempotent link of hardware ↔ project (optional `role`)          |
| `record_firmware`          | write | Creates a `HardwareConfig` (kind=firmware\|os\|bootloader\|config), sets `is_current`, logs a `flashed` activity |
| `start_loan`               | write | Opens a loan (`loaned_to`, `due_at?`, `notes?`)                  |
| `return_loan`              | write | Closes an open loan (`returned_at?` defaults to now)             |
| `create_project`           | write | Creates a Project (title, description?, status?, priority?)      |

**Resources** (URI-addressable context for richer LLM grounding):

| URI template                  | Returns                                                       |
|-------------------------------|---------------------------------------------------------------|
| `hwinv://hardware/{id}`       | Same shape as `get_hardware`, as an MCP resource              |
| `hwinv://project/{id}`        | Same shape as `get_project`, as an MCP resource               |
| `hwinv://dashboard`           | Current dashboard snapshot (idle count, overdue loans, stats) |

**Conventions:**
- **Tool input schemas** are generated from C# parameter types by the SDK
  (`System.ComponentModel.DataAnnotations` attributes carry descriptions and constraints).
- **Tool outputs** are JSON serialised from the same DTO records the REST endpoints return.
- **Errors** are returned as MCP tool errors (`isError: true`) with a human-readable
  message; the same exceptions that produce REST 4xx/5xx map to MCP errors via a single
  middleware.
- **Pagination** — list tools accept `limit` (default 50, max 200) and `offset`, and
  return `{ items, total, limit, offset }` so the agent can page deterministically.
- **No streaming tools in v1** — every tool is request/response. Streaming MCP tools are
  reserved for a future "watch idle hardware" / "tail activities" use case.
- **Auth** — when the server runs locally (bound to `127.0.0.1`), `/mcp` is
  unauthenticated like the REST API. When `APP_ENV=production`, MCP shares the same
  bearer-token scheme as REST (verified in V8).
- **Discovery** — the server advertises a friendly `serverInfo.name = "hw-inventory"` and
  a `serverInfo.instructions` string ("This server exposes a personal hardware inventory.
  Use list_hardware to browse, get_hardware for detail, and intent-shaped write tools to
  log activities…") so agents pick the right tools without trial-and-error.

Auth:
- v1: **none**, but the server binds to `127.0.0.1` by default (config flag).
- v2: bearer token via env `AUTH_TOKEN`. If `APP_ENV=production`, the app **refuses to start**
  without `AUTH_TOKEN` set and without an explicit bind address. Frontend stores the token
  in `localStorage` initially (acceptable for personal-use; HttpOnly cookie is a later upgrade).

---

## 5. Frontend pages

- **Dashboard**
  - Idle hardware widget (clickable → Hardware list pre-filtered).
  - Project board (kanban-ish: idea / planned / in_progress / paused / done).
  - "Got nothing for it?" panel = suggestions endpoint.
  - Stats: total HW, by category, by condition.
- **Hardware list**
  - Filter bar (category multi-select, tag multi-select, status, condition, full-text on
    name/model/notes, "idle ≥ N days", "show archived").
  - Add button → drawer form.
  - Row quick actions: **Mark used today**, **Log activity**, **Move location**,
    **Link to project**, **Archive / sold / lost**.
- **Hardware detail**
  - Header (name, categories, status, condition, tags, last used, last activity,
    current firmware/OS badge, active-loan badge).
  - Quick-action toolbar (same set as list row).
  - Tabs: **Specs & IDs** (structured + JSON), **Activities** (timeline + add backdated),
    **Configs** (firmware/OS history with "current" highlight), **Projects** (linked, with role),
    **Loans** (history), **Links** (datasheet/vendor/repo), **Notes** (markdown).
- **Projects list** — filter by status/priority, kanban toggle, "show archived".
- **Project detail** — description, linked hardware (with role), status workflow buttons,
  links, notes.
- **Settings** — manage Categories and Tags; **Import / Export** (JSON + CSV via API in v1.5).

UI library: **Naive UI** (DataTable, Form, Drawer, Tabs, Tag, Timeline, Markdown render).

---

## 6. Repository layout

```
hw-inventory/
├── README.md
├── plan.md
├── .gitignore
├── .editorconfig
├── docker-compose.yml          # deploy-ready (V8 verification)
├── proto/                      # gRPC .proto definitions (shared by backend + CLI)
│   └── hardware.proto
├── backend/
│   ├── HwInventory.sln
│   ├── HwInventory.Api/        # ASP.NET Core Minimal API + gRPC services
│   │   ├── HwInventory.Api.csproj
│   │   ├── Program.cs          # app bootstrap, DI, middleware, route groups
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   ├── Config/             # strongly-typed IOptions (DatabaseUrl, IdleDefaultDays, …)
│   │   ├── Data/               # EF Core DbContext, Migrations/
│   │   │   ├── AppDbContext.cs
│   │   │   └── Migrations/
│   │   ├── Models/             # EF Core entity classes
│   │   ├── Dtos/               # request / response DTOs (replaces Pydantic schemas)
│   │   ├── Endpoints/          # Minimal API route groups (Hardware, Projects, Tags, …)
│   │   ├── Services/           # idle detection, last_used_at sync, suggestions
│   │   ├── Grpc/             # gRPC service implementations (generated stubs + logic)
│   │   ├── Mcp/              # MCP tools ([McpServerTool]) + resources, mounted at /mcp
│   │   └── Seed/             # `dotnet run --seed` → demo data
│   └── HwInventory.Tests/      # xUnit + WebApplicationFactory
│       ├── HwInventory.Tests.csproj
│       └── *.Tests.cs
└── frontend/
    ├── package.json
    ├── vite.config.ts
    ├── tsconfig.json
    ├── index.html
    ├── src/
    │   ├── main.ts
    │   ├── App.vue
    │   ├── router/
    │   ├── stores/             # Pinia: hardware, projects, settings
    │   ├── api/                # typed fetch wrapper (openapi-typescript types)
    │   ├── types/              # generated from OpenAPI (.d.ts)
    │   ├── views/              # Dashboard, HardwareList, HardwareDetail, ProjectList, ProjectDetail, Settings
    │   └── components/         # HardwareForm, ActivityTimeline, ProjectCard, …
    └── tests/
```

---

## 7. Modules & verification

All modules are built in parallel — there is no gated phase that blocks another.
Each module has a clear boundary; the verification steps below define *what "done" means*
for the whole system, checked in a logical dependency order (you can't verify gRPC until
the service layer is solid, can't verify the UI until REST is solid, etc.).

### Modules (built concurrently)

| Module | Contents |
|---|---|
| **Data** | EF Core entities, `AppDbContext`, migrations, seed command |
| **Services** | `ActivityService`, `HardwareService`, `DashboardService`, … — all business logic |
| **REST** | `Endpoints/*` route groups — thin wrappers over Services |
| **gRPC** | `Grpc/*` service classes + `proto/hardware.proto` — thin wrappers over Services |
| **MCP** | `Mcp/*` tools & resources — thin wrappers over Services, mounted at `/mcp` |
| **Frontend** | Vue 3 + Pinia + Naive UI, consuming REST via typed `fetch` wrapper |
| **Auth & deploy** | Bearer-token middleware, production-start guard, `docker-compose.yml` |
| **CLI** | `hw` command (`dotnet tool` or standalone), talking to gRPC |

---

### Verification steps

#### V1 — Data layer
- `dotnet ef database update` against SQLite produces a clean schema.
- `dotnet run --seed` populates ~10 hardware items (MCUs, SBC, laptop, phone, SSD),
  2–3 projects, sample activities, one firmware history entry, one open loan.
- Deleting the `.db` file and re-running both commands produces an identical clean state.
- All UNIQUE constraints and indexes on `LastUsedAt`, `LastActivityAt`, `ArchivedAt` exist.

#### V2 — Service layer
- `dotnet test` green: unit tests for `ActivityService` (correct `LastUsedAt` /
  `LastActivityAt` update per activity kind), idle-hardware query, suggestion query,
  overdue-loan query, link idempotency.
- `HardwareConfig.IsCurrent` enforcement: setting a new current config clears the previous
  one for the same `(HardwareId, Kind)`.
- Backdated activity: creating an `Activity` with `OccurredAt` in the past updates
  `LastUsedAt` only if it becomes the new maximum.

#### V3 — REST transport
- `dotnet test` green: `WebApplicationFactory` integration tests — one happy-path per route
  group (Category, Tag, Hardware, Project, Activity, Config, Loan, Dashboard, Import/Export).
- `GET /api/health` → 200.
- `GET /scalar` → Scalar UI loads; OpenAPI doc at `/openapi/v1.json` is valid.
- List endpoints return `{ items, total, limit, offset }` and respect `limit`, `offset`,
  `sort`, `q`, and all documented filter params.
- `PATCH` with JSON Patch document (RFC 6902) on Hardware and Project updates only the
  patched fields; an out-of-allow-list path returns 422.
- Soft-delete: `DELETE /api/hardware/{id}` → item absent from default list →
  reappears with `?include_archived=true`.
- `PUT` link → idempotent (second `PUT` returns 200, not 409).
- `DELETE /api/hardware/{id}?hard=true` → item gone permanently.
- Auth guard (production mode): server refuses to start without `AUTH_TOKEN` and explicit
  bind when `APP_ENV=production`.

#### V4 — gRPC transport
- `grpcurl -plaintext localhost:<port> list` returns all registered service names.
- `HardwareService.List` → returns same items as `GET /api/hardware` for identical filters.
- `HardwareService.LogActivity` → `LastUsedAt` / `LastActivityAt` updated identically
  to the REST equivalent — verified by reading back through either transport.
- `HardwareService.Update` with a `FieldMask` updates only masked fields.
- Error codes: `Get` with unknown id → `NOT_FOUND`; invalid request → `INVALID_ARGUMENT`.

#### V5 — MCP transport
- MCP `tools/list` returns the full curated tool set from §4 with correct input schemas.
- `list_hardware` with `idle_days=90` returns the same set as `GET /api/dashboard/idle`.
- `mark_used_today` for a hardware item → `LastUsedAt` updated; visible via REST and gRPC.
- `log_activity` with a backdated `occurred_at` → timeline correct across all transports.
- `record_firmware` → new `HardwareConfig` with `IsCurrent=true`; previous current config
  for same kind cleared; hardware detail (REST) shows updated firmware badge.
- `hwinv://hardware/{id}` resource → same shape as `GET /api/hardware/{id}`.
- MCP tool error on invalid input (`isError: true`, human-readable message).

#### V6 — Frontend
- `npm run dev` → Dashboard loads with idle widget, project board, stats.
- Hardware list: filter by category (multi), tag (multi), status, condition, full-text,
  idle ≥ N days, show archived.
- Hardware detail: all tabs render (Specs & IDs, Activities, Configs, Projects, Loans,
  Links, Notes); adding a backdated activity updates the idle widget on the Dashboard.
- "Mark used today" quick action → `LastUsedAt` updates; item leaves idle widget.
- Record firmware → "current" badge updates on hardware header.
- Create project → link hardware (idempotent PUT) → appears on Dashboard project board.
- Soft-delete hardware → gone from default list → recoverable via "show archived" toggle.
- `vitest` green for all component and store tests.

#### V7 — Cross-transport consistency
- The same write (e.g. `log_activity`) performed via REST, gRPC, and MCP in sequence each
  update the same DB row; all three transports read back the same final state.
- Seed → import/export round-trip: `GET /api/export/json` → wipe DB → `POST /api/import/json`
  → all items, projects, activities, configs, loans present.

#### V8 — Deploy-ready
- `dotnet ef database update` against a Postgres connection string produces the identical
  schema as SQLite; seed runs cleanly.
- `docker-compose up` starts Postgres + backend + frontend (nginx); all V3–V5 checks pass
  against the containerised stack.
- `APP_ENV=production` without `AUTH_TOKEN` → server refuses to start (logged error, non-zero exit).
- Bearer token in `Authorization: Bearer <token>` header gates REST, gRPC metadata, and
  MCP `/mcp` endpoint uniformly.

---

## 8. Risks & open decisions

| # | Topic | Default in plan | Revisit when |
|---|-------|-----------------|--------------|
| 1 | Auth model after deploy | Single-user bearer token | You want to share with someone |
| 2 | `specs` schema | Free-form JSON + structured ID fields | Friction in UI → add per-category template |
| 3 | Category hierarchy | Flat (`parent_id` reserved) | List gets >30 categories |
| 4 | Location modelling | Free-text in v1, `location_id` reserved | Inconsistent free-text strings annoy you |
| 5 | Attachments storage | Local disk via `storage_key` (UI phase 2) | You self-host on a VPS without much disk |
| 6 | Soft-delete | Enabled v1 for Hardware & Project | — |
| 7 | Timezone | Store UTC, render local in UI | — |
| 8 | Token in `localStorage` | Acceptable for personal use | You want stricter XSS posture → HttpOnly cookie |

---

## 9. Definition of done

A feature or change is **done** when all of the following hold.
When new features are added, update this section and §4/§7 accordingly.

**Code**
- All modules touched by the change compile without warnings (`dotnet build -warnaserror`,
  `vue-tsc --noEmit`).
- `dotnet test` green; `vitest` green. New behaviour has at least one test.

**Correctness (the core invariants must always hold)**
- `dotnet run --seed` → `dotnet run` → `GET /scalar` shows all endpoints; Vue UI loads.
- Deleting the SQLite file and re-running `dotnet ef database update` + seed produces a
  clean, fully working app.
- A write via any transport (REST, gRPC, MCP) is immediately visible on the other two.
- `log_activity` with kind `used`/`flashed`/`repaired`/`measured`/`configured` updates
  **both** `LastUsedAt` and `LastActivityAt`; kind `inspected`/`moved`/`note` updates
  **only** `LastActivityAt`.
- Soft-delete: item absent from default list; recoverable via `?include_archived=true`.
- `PUT` link is idempotent — second call returns 200, no duplicate row.

**Documented**
- `README.md` reflects any new endpoints, env vars, MCP tools, or run instructions.
- New MCP tools have `Description` attributes; Scalar UI shows the new endpoints.

**Plan updated**
- If the change adds or removes an endpoint, tool, entity, or verification step,
  the relevant section of this plan is updated in the same change.

---

## 10. Operations & data lifecycle

### 10.1 Current dev workflow (no Docker)
While the project is still under active development, work happens **directly on the dev
machine** — no container in the loop. Docker exists only for deployment to the thin
client / future server.

```powershell
# backend (http://127.0.0.1:5080)
cd backend\HwInventory.Api
dotnet run                      # normal run
dotnet run -- --seed            # one-shot: populate demo data, then exits

# frontend dev server (http://127.0.0.1:5173, proxies /api → :5080)
cd frontend
npm run dev

# tests
cd backend ; dotnet test
cd frontend ; npm test ; npm run build
```

Database file lives at `backend\HwInventory.Api\hw_inventory.db` (project cwd) when
running this way. It's git-ignored by the `*.db` rule.

### 10.2 Three layers of "database state"

| Concern | Where it lives | In git? |
|---|---|---|
| **Schema** (tables, columns, indexes) | `backend/HwInventory.Api/Data/Migrations/*.cs` (EF Core migrations) | ✅ yes |
| **Demo / seed data** | `backend/HwInventory.Api/Seed/DemoSeeder.cs` (idempotent C# code) | ✅ yes |
| **The actual `.db` file** | `data/hw_inventory.db` (Docker bind mount) or `backend/HwInventory.Api/hw_inventory.db` (dev) | ❌ never |

Rules:
- **Never commit a `.db` file.** It diffs poorly, would leak real inventory data, and
  goes stale against schema changes. The seeder is the source of truth.
- **Schema changes always happen via a new migration**
  (`dotnet ef migrations add <Name>` in `backend/HwInventory.Api`), never by editing an
  existing migration in-place.
- **SQLite startup auto-migrates** (`db.Database.MigrateAsync()` in `Program.cs`). Postgres
  does not — Postgres deployments must run `dotnet ef database update` separately.
- **Seeder is idempotent at the coarse level** — `DemoSeeder` skips if any hardware row
  already exists, so re-running `--seed` on a populated DB is a no-op.

### 10.3 Database portability (Windows ↔ Debian, x64 ↔ arm64)

The SQLite file format is byte-identical across OSes and CPU architectures, so the
`.db` from this Windows dev box can be copied to the Debian thin client (Wyse 5070,
amd64) or to a future arm64 server, and used as-is. No dump/restore step needed.

**Move procedure (no data loss):**
1. Stop the source app (`Ctrl-C` for `dotnet run`, or `docker compose down` on a
   container host) so the WAL is checkpointed back into the main file.
2. Copy the whole `data/` folder (or the `hw_inventory.db` + `hw_inventory.db-wal` +
   `hw_inventory.db-shm` triple if the sidecars exist).
3. Place it at the target's `data/` path.
4. On Debian, make sure the bind-mount directory is writable by the container UID:
   `chmod 777 data` (single-user box) or `chown 1654:1654 data` (match aspnet image).
5. `docker compose pull && docker compose up -d`. First boot auto-applies any missing
   migrations.

**Schema-version rule:** newer image + older DB = migrations forward-fill on boot (safe).
Older image + newer DB = queries throw on missing columns (don't do this — pull the newer
image first).

### 10.4 Backups

Two patterns:

- **Cold copy** (simple): stop the app, `cp -a data/ data.bak/` or zip and copy off-box.
- **Online snapshot** (no downtime): SQLite's online backup API gives a consistent file
  copy while the app keeps serving:
  ```bash
  docker compose exec app sh -c 'sqlite3 /data/hw_inventory.db ".backup /data/hw_inventory.bak"'
  ```
  Requires `sqlite3` in the runtime image (not installed by default; ~3 MB to add).

### 10.5 Deployment path (future, when promoting to thin client / server)

End-to-end loop (one-way push, no manual builds on the target):
1. `git push` to GitHub on `main` (or tag `v*.*.*`).
2. GitHub Actions (`.github/workflows/docker.yml`) runs the test gate, then buildx
   publishes a multi-arch image to `ghcr.io/<owner>/hw-inventory:latest` (and `:v1.2.3`
   for tagged releases).
3. On the target box:
   ```bash
   docker compose pull
   docker compose up -d
   ```
4. Health: `curl http://<host>:8080/api/health`; SPA at `http://<host>:8080/`.

Compose contract (`docker-compose.yml` + `.env`):
- One service `app`, pulled from GHCR; bind-mounts `./data:/data`; reads env from
  `.env` (`GHCR_OWNER`, `AUTH_TOKEN`, `HOST_PORT`, `IMAGE_TAG`, `APP_ENV`,
  `IDLE_DEFAULT_DAYS`).
- Postgres is kept under `profiles: ["postgres"]` — opt-in via `docker compose
  --profile postgres up -d` and a Npgsql `DATABASE_URL` in `.env`.

No TLS in this iteration (LAN only); a `compose.prod.yml` overlay with Caddy can be
added later for public deployments without changing the base image.

## 11. gRPC surface & testing

### 11.1 What's actually implemented

``proto/hardware.proto`` defines the **full contract** of all 8 services from §4
(HealthService, CategoryService, TagService, HardwareService, ProjectService,
ActivityService, DashboardService, ImportExportService) — ~50 RPC methods and
~30 message types in total. The proto is the single source of truth for the
gRPC surface and is intentionally kept ahead of the server implementation.

**Server-side implementation status:**

| Service              | Wired in ``Program.cs``? | Runtime behaviour                                   |
|----------------------|--------------------------|-----------------------------------------------------|
| ``HealthService``    | ✅ Yes                    | Returns ``{status:"ok"}`` — see ``HealthGrpcService.cs`` |
| ``CategoryService``  | ❌ No                     | All methods return ``StatusCode.Unimplemented``     |
| ``TagService``       | ❌ No                     | All methods return ``StatusCode.Unimplemented``     |
| ``HardwareService``  | ❌ No                     | All methods return ``StatusCode.Unimplemented``     |
| ``ProjectService``   | ❌ No                     | All methods return ``StatusCode.Unimplemented``     |
| ``ActivityService``  | ❌ No                     | All methods return ``StatusCode.Unimplemented``     |
| ``DashboardService`` | ❌ No                     | All methods return ``StatusCode.Unimplemented``     |
| ``ImportExportService`` | ❌ No                  | All methods return ``StatusCode.Unimplemented``     |

This is deliberate. Wiring each service is mechanical (the C# class unwraps the
request, calls the matching ``Services/*`` method that already powers the REST
endpoint, and re-packs the result) but adds ~500-800 lines of wrapper code that
nobody calls today — REST and MCP cover every current consumer. The contract
ships now so future clients (CLI in phase 3, mobile/desktop later) can generate
stubs and code against the real shape; the server-side wiring lands incrementally
when a service has an actual caller.

The proto is compiled with ``GrpcServices="Both"`` so the same assembly produces
server bases (used by ``HealthGrpcService`` today, the rest tomorrow) and client
stubs (consumed by the test project via project reference — no duplicate proto
compile).

**Contract-quality notes that shaped the proto:**
- Optional scalars use proto3 ``optional`` so clients can tell "absent" from
  "default", matching the REST DTO null vs default semantics.
- ``Hardware.cost`` is a decimal-as-string, not ``double``. Protobuf has no
  decimal type and double would silently round on money.
- JSON-blob fields (``identifiers``, ``specs``, ``links``, ``metadata``) use
  ``google.protobuf.Struct`` — round-trips object/array/string/number/bool but
  doesn't preserve int-vs-float. Acceptable for free-form spec data; promote to
  a typed field if anything becomes query-critical.
- Timestamps are ``google.protobuf.Timestamp`` (UTC).
- Partial updates use ``google.protobuf.FieldMask update_mask`` — gRPC's PATCH
  equivalent, avoiding the "null = clear or omit?" ambiguity.
- Enums use ``_UNSPECIFIED = 0`` distinct from any domain ``_UNKNOWN`` value;
  the server will reject unspecified values on write.
- Import/export is **unary** with ``bytes`` payloads, not streaming. The
  expected scale (a few thousand items for a personal inventory) doesn't
  justify streaming complexity; streaming variants can be added later if needed.

### 11.2 Bearer auth covers gRPC too

``BearerAuthMiddleware`` guards any path under ``/hwinventory.v1*`` exactly like the
REST endpoints under ``/api/*``. Verified live against the dev server with grpcurl:

| Scenario                         | Result               |
|----------------------------------|----------------------|
| no ``authorization`` header      | 401 → ``Unauthenticated`` |
| ``authorization: bearer wrong``  | 403 → ``PermissionDenied`` |
| ``authorization: bearer <good>`` | ``{status:"ok"}``    |

When ``AUTH_TOKEN`` is unset (dev mode), gRPC is reachable without a header.

### 11.3 In-process integration tests

``backend/HwInventory.Tests/GrpcSmokeTests.cs`` runs **5** xUnit tests against an
in-process ``WebApplicationFactory<Program>``:

1. no-auth + token unset → success
2. valid bearer → success
3. missing bearer when token set → ``Unauthenticated``
4. bad bearer when token set → ``PermissionDenied``
5. **contract-without-implementation** — every declared-but-unregistered
   service (Category, Tag, Hardware, Project, Activity, Dashboard,
   ImportExport) must return ``StatusCode.Unimplemented``. When a service is
   wired in phase 3, replace its case in this test with a real behavioural
   assertion — the test forces the implementation status to stay in sync with
   the contract.

A small ``GrpcApiFactory`` overrides ``HwInventoryOptions`` via DI
(``services.RemoveAll`` + ``AddSingleton``) — **never** by mutating environment
variables, which would leak across xUnit's parallel test runners. The factory
creates the ``GrpcChannel`` over ``TestServer.CreateHandler()``, which transparently
speaks HTTP/2 in-process and sidesteps the Kestrel/TLS issue described next.

### 11.4 The plain-HTTP / HTTP/2 trap (and why gRPC needs its own port)

gRPC requires HTTP/2. Kestrel's ``Http1AndHttp2`` mixed-protocol endpoint relies on
TLS + ALPN to negotiate, so on a plain-HTTP endpoint (LAN deployment, no TLS) it
silently downgrades every connection to HTTP/1.1, and live gRPC clients fail with:

> ``unable to establish HTTP/2 connection``

In-process tests don't hit this because ``TestServer`` simulates HTTP/2 without
going through Kestrel.

Fix shipped in ``Program.cs``: an **opt-in** second listener configured via the
``GRPC_BIND_ADDRESS`` env var. When set, Kestrel listens on:

- ``BIND_ADDRESS`` — HTTP/1.1 only (REST, SPA, MCP, OpenAPI)
- ``GRPC_BIND_ADDRESS`` — HTTP/2 only (gRPC)

When unset, the default deployment surface is unchanged (single port, no gRPC over
the wire). Browsers don't speak h2c, so the SPA stays on HTTP/1.1; gRPC clients
target the dedicated port. A future TLS overlay (Caddy in ``compose.prod.yml``) can
collapse both behind a single 443 with proper ALPN.

### 11.5 Live verification recipe

```powershell
# 1. start the server with gRPC enabled
$env:AUTH_TOKEN = "demo-secret"
$env:GRPC_BIND_ADDRESS = "http://127.0.0.1:5081"
dotnet run --project backend\HwInventory.Api

# 2. install grpcurl once (Windows)
winget install --id FullStoryDev.grpcurl

# 3. call HealthService.Check
grpcurl -plaintext `
  -H "authorization: bearer demo-secret" `
  -proto proto\hardware.proto -import-path proto `
  -d "{}" 127.0.0.1:5081 hwinventory.v1.HealthService/Check
# → {"status": "ok"}
```

Reflection is **disabled outside Development** (see ``Program.cs``), so ``grpcurl
list`` won't enumerate services in production — clients ship the ``.proto`` file
out-of-band, which is the standard pattern for production gRPC.

### 11.6 Rollout plan for the remaining services

When a real consumer needs a service, wire it in roughly this order (each step
is mechanical because the business logic already lives in ``Services/*``):

1. Create ``backend/HwInventory.Api/Grpc/<Name>GrpcService.cs`` that inherits
   from the generated ``<Name>Base`` class.
2. For each RPC: unwrap the proto request → call the matching ``Services/*``
   method → map the DTO result back to the proto message (helpers in a new
   ``Grpc/ProtoMapping.cs`` mirror the existing ``Services/Mapping.cs``).
3. ``app.MapGrpcService<<Name>GrpcService>();`` in ``Program.cs`` next to
   ``MapGrpcService<HealthGrpcService>()``.
4. Replace the corresponding ``AssertUnimplemented`` line in
   ``GrpcSmokeTests.DeclaredButUnregisteredServices_ReturnUnimplemented`` with
   a real behavioural test (and add per-method tests under a new test class).
5. Bump the proto comment block at the top of ``hardware.proto`` to remove the
   newly-implemented service from the "contract only" list.

Suggested order based on what a CLI client would want first:
``DashboardService.GetStats`` → ``HardwareService.{List, Get, MarkUsedToday}``
→ ``ActivityService.List`` → ``ProjectService.{List, Get}`` → write methods
(``Create``/``Update``/``Link*``) → ``CategoryService``/``TagService`` →
``ImportExportService``.

The ``v1`` package label freezes per-service: the message and method shapes
for any service that has shipped end-to-end are stable; services still marked
"contract only" in §11.1 may have breaking proto changes until they ship.

