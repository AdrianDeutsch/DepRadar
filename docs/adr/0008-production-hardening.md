# ADR 0008 — Production hardening

- Status: Accepted
- Date: 2026-06-25
- Deciders: Architecture

## Context

Slices 1–5 delivered the functionality. Slice 6 makes it production-near and clears
the items earlier ADRs deferred (caching, migrations, the stale-scan reaper, the EF
health check) plus custom observability.

## Decision

- **Caching** (`HybridCache`): external GET/POST responses (NuGet, OSV, deps.dev) are
  cached at the **raw-JSON level** (6-hour TTL) in `HttpJsonCache`. Caching the response
  string — not parsed domain types — keeps serialization trivial and avoids fragile
  round-tripping of types like `NuGetVersion`. Idempotent re-scans hit the cache, not the
  API, conserving quota. (In-memory L1 today; a Redis L2 would share across processes.)
- **EF Core migrations** replace `EnsureCreated`. A design-time factory enables
  `dotnet ef`; the `InitialCreate` migration includes the **pgvector extension** and the
  `vector(256)` column. Hosts and integration tests apply migrations with `Migrate()`,
  so every test run validates the migration against real pgvector.
- **Stale-scan reaper**: a worker `BackgroundService` requeues scans stuck in `Running`
  (e.g. a crashed worker), relying on the idempotent upserts to make retries safe.
- **Observability**: a custom `ActivitySource` + `Meter` ("DepRadar") emit scan spans and
  metrics (`depradar.scans.completed`, `depradar.packages.discovered`), registered with
  OpenTelemetry in ServiceDefaults and surfaced through Aspire.
- **Health**: a database readiness check on `/health`.

## Consequences

- Re-scans are fast and quota-friendly; the schema is versioned and migration-validated.
- Lost/abandoned scans self-heal.
- Generated migration code is exempted from the analyzer bar via `.editorconfig`.

## Follow-ups since shipped

- **Redis L2 cache**: HybridCache now uses a Redis `IDistributedCache` (wired by Aspire)
  as a shared L2, so the API and Worker share cached responses across processes; absent
  Redis it falls back to L1.
- **GitHub repo-health**: a best-effort enricher resolves a package's source repo (via
  deps.dev) and reads `archived` / `pushed_at` from the GitHub API (token-optional,
  cached), feeding `ARCHIVED` (high) and `STALE` (medium) maintenance findings. Adding
  the two columns required a second migration (`AddRepositoryHealth`) — exactly the kind
  of schema change `Migrate()` catches that `EnsureCreated` would silently mask.
