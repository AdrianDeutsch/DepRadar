# ADR 0013 — Explainable & exportable findings (paths + SARIF)

- Status: Accepted
- Date: 2026-06-26
- Deciders: Architecture

## Context

A risk score and a list of findings tell you *what* is wrong, but two questions decide
whether anyone acts: *why is this (transitive) package even here?* and *can the finding
land where my team already triages?* — i.e. GitHub's Security tab, not a bespoke UI.

## Decision

- **Vulnerability paths.** A pure `DependencyPathFinder` (BFS shortest path over the
  resolved edges, keyed by package id) answers "how did this get pulled in?".
  `GetVulnerabilityPathsQuery` traces the chain to every vulnerable node
  (`root → A → B`), surfaced at `GET /packages/{id}/vulnerability-paths` and inline in the
  dashboard's risk drill-down ("pulled in via …"). Pure + reusable.
- **SARIF export.** A pure `SarifBuilder` renders the assessed graph as **SARIF 2.1.0**:
  one rule per finding code, one result per finding, severity mapped to
  `error`/`warning`/`note`, `partialFingerprints` for cross-run dedup, and — reusing the
  path finder — the dependency path appended to vulnerability messages. Exposed at
  `GET /packages/{id}/sarif`, via the CLI `--sarif`, and the **GitHub Action** now uploads
  it with `github/codeql-action/upload-sarif`, so DepRadar findings appear natively in the
  repository's **Security** tab.

## Consequences

- Findings are now both **explainable** (the path) and **portable** (SARIF) — the same
  `GraphAssessment` feeds the dashboard, the CLI and GitHub code scanning.
- SARIF results point at the manifest/package coordinate (dependency findings have no
  source line); GitHub accepts this and dedups via the fingerprints.
- The path finder is a small piece of pure graph logic reused by both features and the
  SARIF messages — no duplication.
