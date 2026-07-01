# ADR 0026 — Multi-ecosystem support (Go)

- Status: Accepted
- Date: 2026-07-02
- Deciders: Architecture

## Context

The fifth ecosystem, covering the last major package manager on the roadmap. Go's twist
is the opposite of PyPI's: **there is no range grammar at all** — `go.mod` requirements
are exact versions and the toolchain uses minimal version selection (MVS). The data
source is the keyless Go module proxy (proxy.golang.org), which speaks plain
text/JSON: `@v/list`, `@latest`, and the raw `go.mod` of any version.

## Decision

- **The Domain did not change** (fifth time). Go versions are strict semver behind a
  `v` prefix (`GoVersion` strips it); pseudo-versions parse as pre-releases and rank
  naturally below tags, `+incompatible` is valid build metadata. Module paths flow
  through `PackageId.FromNormalized` verbatim (OSV keys Go advisories by module path).
- **No range resolver — the BFS follows exact requires.** Each module version's own
  direct (non-`// indirect`) `require` lines are the edges. This yields the DECLARED
  graph: where MVS would collapse multiple required versions of one module to the
  maximum, we keep them all — the conservative choice for risk scanning.
- **Proxy specifics handled where they live:** upper-case path segments are
  `!`-escaped in `GoProxyClient` (per protocol); the ORIGINAL version string is kept
  for `.mod` fetches (the proxy is exact about pseudo/`+incompatible` forms) while the
  parsed value drives ordering.
- **Targets:** `depradar go <module> [version]`, `go.mod` (manifest; indirect requires
  skipped) and `go.sum` (lock target — honestly a checksum LEDGER that may hold a
  superset of selected versions; scanning it covers everything the module graph could
  resolve to). The proxy serves **no license metadata**, so Go nodes carry no license
  facts — advisories, exploit intelligence and staleness are the signals.

## Consequences

- Ecosystem N+1 shrank again: no range matcher at all this time — a text-protocol
  client, a resolver, two ~50-line parsers and the CLI/Action spec.
- License findings are absent for Go (documented scope line; deps.dev could enrich
  this later).
- Verified live: `go golang.org/x/text v0.3.7` flags CVE-2022-32149 and resolves the
  pseudo-versioned x/tools dependency; a real `go.mod` resolved 77 modules with
  KEV-escalated Criticals on old `golang.org/x/net` (HTTP/2 Rapid Reset); `go.sum`
  scanned the exact recorded set.

[ADR 0017]: 0017-multi-ecosystem-pypi.md
[ADR 0024]: 0024-multi-ecosystem-cargo.md
