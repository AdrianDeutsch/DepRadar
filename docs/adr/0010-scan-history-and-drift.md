# ADR 0010 — Scan history & drift detection

- Status: Accepted
- Date: 2026-06-26
- Deciders: Architecture

## Context

The version diff ([ADR 0009]) answers "how do versions A and B differ?". The more
operationally valuable question is temporal: *"what changed in my dependencies since I
last looked?"* — a transitive package that newly became vulnerable, deprecated or
archived while my own version stayed put.

The scan tables are **idempotent upserts**: they hold the *current* facts and overwrite
on every re-scan, so historical risk state is lost. Detecting drift therefore requires
remembering past assessments.

## Decision

- **Append-only snapshots.** When a scan completes, `RunScanHandler` loads the assessed
  graph (reusing `GraphAssessmentLoader`) and records a `ScanSnapshot` — the per-package
  risk state (score, level, deprecated/archived/stale, advisory ids, license) plus the
  overall score/level, stamped with the completion time. It is **best-effort**: a
  snapshot failure logs and never fails the scan.
- **One JSON column, no child table.** A snapshot is immutable, so its package states
  serialize to a single `jsonb` column via a value converter rather than a second table.
  The row is indexed by `(RootPackageId, CreatedAt)`, making "the two most recent
  snapshots" a cheap read.
- **A pure `DriftAnalyzer`** (Domain) compares the two latest snapshots into a
  `DriftReport` of typed events (`BecameVulnerable`, `BecameDeprecated`, `BecameArchived`,
  `BecameStale`, `AdvisoryCleared`, `HealthRegressed/Improved`, `Package Added/Removed`)
  with a net-health delta, ordered worst-first. Fully unit-testable, no I/O.
- **Surfaced** at `GET /packages/{id}/drift` and an auto-loading dashboard panel ("Drift
  since last scan"). With a single scan the response flags `hasBaseline: false`.

## Consequences

- Drift is a deliberately **stateful** feature (it needs history), the natural complement
  to the **stateless** CLI/diff of ADR 0009 — the same `GraphAssessment` feeds both.
- History grows append-only; pruning/retention is a future concern, not a launch blocker.
- Storing states as `jsonb` keeps the schema flat and the migration trivial; the
  `AddScanSnapshots` migration is validated against real pgvector on every test run.
- A new integration test exercises the full `jsonb` round-trip (write on scan, read for
  drift) so the serialization can never silently regress.

[ADR 0009]: 0009-stateless-analysis-cli-and-policy.md
