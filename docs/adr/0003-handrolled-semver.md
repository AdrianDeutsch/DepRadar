# ADR 0003 — Hand-rolled SemVer value object in the Domain

- Status: Accepted
- Date: 2026-06-23
- Deciders: Architecture

## Context

The domain needs a first-class, comparable version type (`SemVer`) to order versions
and, later, to detect license/maintenance changes across versions. The canonical
library for the NuGet ecosystem is `NuGet.Versioning` (`NuGetVersion`), which fully
implements floating versions and range grammar.

However, ADR 0001 mandates a **dependency-free Domain**. Pulling `NuGet.Versioning`
into the core would leak an ecosystem library into the model.

## Decision

Implement a small `SemVer` value object in `DepRadar.Domain.ValueObjects`:

- Parses `MAJOR.MINOR.PATCH[-prerelease][+build]` with the official semver.org regex.
- Implements precedence per SemVer 2.0.0 §11 (build metadata ignored), covered by
  unit tests including the canonical ordering example.

The broader **NuGet version *range*** grammar (e.g. `[6.0.0, )`, floating `*`) is
deliberately **not** modeled here; ranges are stored as raw strings on
`DependencyEdge.VersionRange` and resolved in the ingestion pipeline.

## Consequences

- The Domain stays free of external dependencies and the value-object pattern is
  demonstrated end-to-end.
- We accept responsibility for SemVer-correctness (mitigated by tests).
- If full range resolution becomes domain-critical, revisit by introducing
  `NuGet.Versioning` **in Infrastructure** (not Domain), mapping to domain types at
  the boundary.
