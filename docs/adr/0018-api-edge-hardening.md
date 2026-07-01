# ADR 0018 — API edge hardening (opt-in key + rate limiting)

- Status: Accepted
- Date: 2026-07-01
- Deciders: Architecture

## Context

The self-review flagged that the HTTP API and dashboard had **no authentication and no
rate limiting** — fine for a local demo, a gap the moment it is exposed. The fix must not
break the zero-config local/test experience (the whole platform runs with `docker compose
up` and no secrets), so hardening has to be *opt-in and permissive by default*.

## Decision

A single presentation-layer concern, `ApiSecurity` (SoC), wires two independent controls,
both configured under `Security:*` and both degrading to a safe no-op:

- **Rate limiting (always on).** A per-client fixed-window limiter
  (`Security:RateLimitPerMinute`, default 300) partitioned by API key when present, else
  remote IP. Applied as the ASP.NET Core `GlobalLimiter`, so it covers every endpoint
  without touching each `MapXEndpoints` group. Rejections return `429`.
- **API-key gate (opt-in).** When `Security:ApiKey` is set, every `/api/*` request must
  carry a matching `X-API-Key` header; the comparison is **constant-time over SHA-256
  digests** (`CryptographicOperations.FixedTimeEquals`) to avoid length/timing leaks.
  Health probes, the dashboard (`wwwroot`) and the SignalR hub stay open — only the JSON
  API surface is gated. Unset ⇒ the API is open, as before.

## Consequences

- Zero-config runs (local, Docker, the integration tests, which drive the mediator, not
  HTTP) are unchanged: no key ⇒ open, and the default rate limit is far above test traffic.
- Deployers opt in with two settings and no code change.
- A full API key ≠ real identity/RBAC — this is edge hardening (a shared secret + a
  throttle), not a user-authorization system; OAuth/OIDC + per-user scopes would be the
  next step if DepRadar became multi-tenant.
- Verified live (Production env, no DB): open static `/` → 200; `/api/*` without/with a
  wrong key → 401; with the correct key → passes the gate (500 only because no DB was
  attached); and the limiter returns 429 past the configured window. A full HTTP
  integration test is deferred — this repo's tests deliberately drive the mediator rather
  than host the web app.
