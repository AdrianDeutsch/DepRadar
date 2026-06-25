# ADR 0009 — Stateless analysis pipeline, CLI tool, policy gate & upgrade diff

- Status: Accepted
- Date: 2026-06-25
- Deciders: Architecture

## Context

The API/Worker path persists every scan (durable, observable, dashboard-driven). But
two high-value use cases do not want a database at all:

1. **Shift-left CI**: developers want `depradar scan MyApp.csproj` to fail a build on a
   policy breach, on any machine, with no server and no Postgres.
2. **Upgrade-impact diff**: "what do I take on if I move 12 → 13?" needs two *ad-hoc*
   resolutions, not a stored scan.

The challenge was adding these without forking the risk logic or coupling them to EF Core.

## Decision

- **A stateless `ProjectAnalyzer`** (Application) resolves a graph from NuGet, scores
  every node, and returns the **same `GraphAssessment`** the persisted path produces via
  `GraphAssessmentLoader`. Because the output type is identical, **every downstream
  feature — SBOM, report, chat, policy, diff — works unchanged regardless of source.**
  The analyzer depends only on ports (resolver, vulnerability, repo-health, metadata), so
  it never touches the database.
- **A third host, `DepRadar.Cli`** (`PackAsTool`, command `depradar`), composes
  `AddApplication() + AddInfrastructure()` *without* the `DbContext` and resolves the
  analyzer. The same Application core now backs the **API, the Worker, and the CLI** —
  Clean Architecture's payoff made concrete. Exit codes (`0/1/2`) make it CI-native.
- **A `RiskPolicy` + pure `PolicyEvaluator`** gate an assessment (fail-on level,
  deprecated packages, forbidden license categories) → a list of violations. Pure and
  source-agnostic, unit-tested in isolation.
- **Upgrade-impact diff**: the resolver gained an optional **pinned root version**, so
  the analyzer can resolve any version on demand. A pure `GraphDiffer` diffs two
  `GraphAssessment`s into added/removed/changed packages and **introduced vs. cleared
  advisories**, exposed at `GET /packages/{id}/diff?from=&to=`, in the CLI (`depradar
  diff`), and on the dashboard ("Upgrade impact" panel + `?diff=` deep link).
- **Shared staleness threshold** moved to `Domain.Risk.MaintenanceThresholds`, so the
  persisted (`RiskRepository`) and stateless paths classify maintenance identically.

## Consequences

- The risk model has exactly one implementation; the CLI and diff are thin compositions
  over it (DRY, no logic fork).
- The CLI runs anywhere with just .NET + network — no Docker, no database — which is
  precisely what a CI gate needs.
- Pinned-version resolution doubles NuGet/OSV traffic for a diff, bounded and cached
  (HybridCache), which is acceptable for an on-demand operation.
- `GraphAssessment` is now the project's central currency: produced two ways, consumed
  five.
