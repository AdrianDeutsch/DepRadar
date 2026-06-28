# ADR 0016 — Multi-ecosystem support (npm)

- Status: Accepted
- Date: 2026-06-28
- Deciders: Architecture

## Context

DepRadar was NuGet-only. Supporting a second ecosystem (npm) is the clearest test of
whether the ports actually generalize — or whether NuGet assumptions had leaked into the
Domain.

## Decision

- **The Domain did not change.** `PackageId`, `SemVer`, the scorer, the graph and risk
  model are ecosystem-agnostic. npm ids (incl. scoped `@scope/name`) flow through
  `PackageId.FromNormalized`, and npm versions parse with the existing `SemVer`.
- **New Infrastructure adapters only.** `NpmRegistryClient` (registry.npmjs.org),
  `NpmDependencyGraphResolver` (implements the existing `IDependencyGraphResolver`), and
  `NpmVulnerabilitySource` (the same OSV endpoint with `ecosystem=npm`). They produce the
  same `ResolvedGraph` and `VulnerabilityRecord` the NuGet path does.
- **A pure `NpmRange` resolver** (Application) handles npm's distinct range grammar —
  `^`, `~`, x-ranges, hyphen ranges, comparators and `||` unions — the npm counterpart of
  NuGet's range resolution. It is fully unit-tested (25 cases) since it is the one piece
  with real ecosystem-specific semantics.
- **Reuse, not a fork.** `INpmScanner` wires the npm adapters into the *same* stateless
  `ProjectAnalyzer` (with no-op metadata/repo-health, which are NuGet-specific), so the
  console renderer, policy gate and `GraphAssessment` are shared. The CLI exposes it as
  `depradar npm <package> [version]`.

## Consequences

- Adding an ecosystem is "an adapter + a range resolver", not a rewrite — the payoff of
  the inward-pointing dependencies.
- npm is read-only for now (scan + score); npm auto-fix/remediation and the dashboard/DB
  path stay NuGet-only, a deliberate scope line.
- Verified live: `npm minimist 1.2.0` flags the prototype-pollution CVE; `npm express`
  resolves a 68-package transitive graph clean.
