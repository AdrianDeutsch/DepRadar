# ADR 0005 — Explainable, additive risk scoring

- Status: Accepted
- Date: 2026-06-24
- Deciders: Architecture

## Context

Tech leads don't trust a black-box "health: 62". The score has to be **explainable**
(why is it 62?) and the logic has to be **testable** in isolation, since it is the
core value of the product.

## Decision

A pure domain service, `PackageRiskScorer`, turns a package version's signals into a
list of `RiskFinding`s and an additive `HealthScore`:

- Inputs (assembled from persisted data, no I/O in the scorer): resolved-version
  license, latest-version license, deprecation flag, and known advisories.
- **Findings** carry a category (Security / License / LicenseShift / Maintenance), a
  level, a stable code and a human message — they *are* the explanation.
- **Score** = 100 minus a per-finding penalty (Critical 50 / High 30 / Medium 15 /
  Low 5), floored at 0; overall level = the worst finding's level.
- **License-shift** uses an ordered `LicenseCategory` (Permissive < WeakCopyleft <
  Copyleft < Unknown); a move to a higher category is "tightened" — the OSS→commercial
  "MediatR case".
- **Project rollup** = the worst package score across the transitive graph.

### Sources

- Vulnerabilities: **OSV.dev** `/v1/query` (GHSA/CVE), severity mapped to the level.
- License + deprecation: the **NuGet registration catalog** entry (no extra calls —
  same data the graph resolver already fetches).
- GitHub repo-health (archived / last commit) is intentionally deferred to a later
  slice so Slice 3 needs no GitHub token.

## Consequences

- The score is auditable and the scorer is covered directly by unit tests.
- Weighting is deliberately simple; it can be tuned without touching call sites.
- License classification uses a curated SPDX subset + family prefixes; exotic licenses
  fall into `Unknown` (itself a flagged signal), which is the safe default.
