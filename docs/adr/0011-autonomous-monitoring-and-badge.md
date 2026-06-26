# ADR 0011 — Autonomous monitoring, retention & health badge

- Status: Accepted
- Date: 2026-06-26
- Deciders: Architecture

## Context

Drift ([ADR 0010]) records history but is pull-only: someone has to re-scan and look.
The valuable product is *push* — DepRadar watching your dependencies and telling you the
moment one rots. That needs three things the drift slice deferred: bounded history, a
schedule, and an outbound alert. A README **health badge** is a cheap, high-visibility
addition that reuses the same assessment.

## Decision

- **Retention.** Snapshots are pruned to the newest `N` per root (50) right after each
  one is recorded, in `RunScanHandler`. Count-based retention is predictable, always
  preserves the ≥2 drift needs, and keeps the table bounded without a background job.
- **Slack drift alerts.** A new `IDriftNotifier` port follows the established
  keyless-by-default seam: `NullDriftNotifier` unless `Alerts:SlackWebhookUrl` is set,
  then `SlackDriftNotifier` (a resilient typed `HttpClient` whose base address is the
  webhook). After recording a snapshot, the handler compares the two latest and notifies
  **only** when `DriftAlert.Actionable` (Domain) finds high-severity *new* risk — so
  alerts are signal, never the routine churn. Best-effort: a webhook failure never fails
  a scan.
- **Autonomous watchlist.** A worker `WatchlistRescanService` (`BackgroundService`)
  re-queues a scan for every previously-scanned root on `Watch:IntervalHours`. It is
  **opt-in** (disabled at 0, the default) so normal and test runs never re-scan in the
  background. The existing alert hook then fires automatically — watchlist + alert =
  continuous monitoring with no new moving parts on the alert path.
- **Health badge.** A pure `BadgeRenderer` (Application) emits a flat shields-style SVG;
  `GET /packages/{id}/badge.svg` always returns one (a neutral "not scanned" badge when
  there is no scan) so a README `<img>` never 404s.

## Consequences

- Monitoring is a thin orchestration over parts that already existed (snapshots, the
  drift analyzer, the scan pipeline) — no new persistence, no new risk logic.
- Everything stays off by default; enabling it is two config values. No secrets in the
  repo (the webhook URL is host config, like the LLM key).
- Pure pieces (`DriftAlert`, `DriftAlertMessage`, `BadgeRenderer`) are unit-tested; an
  integration test seeds a healthy baseline, runs a scan, and asserts the alert fires.

## Follow-ups since shipped

- **Retention moved off the scan path.** Pruning was originally inline in
  `RunScanHandler` after each snapshot. It now lives in a worker `SnapshotRetentionService`
  (`Retention:IntervalHours` / `Retention:MaxSnapshotsPerRoot`, sensible defaults) that
  prunes every root on a schedule — so a scan never pays for cleanup, and retention is one
  tunable place rather than a constant in the hot path.

[ADR 0010]: 0010-scan-history-and-drift.md
