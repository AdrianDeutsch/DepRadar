# ADR 0012 — Multi-channel drift alerts & digest

- Status: Accepted
- Date: 2026-06-26
- Deciders: Architecture

## Context

Drift alerts ([ADR 0011]) shipped with one channel (Slack). Teams triage in different
places — some want a **GitHub issue** instead of (or alongside) a Slack ping. And beyond
event-by-event alerts, a periodic **digest** ("everything that changed across all my
packages") is its own useful artifact. Both were asked for; both should fit the existing
seams without reshaping the domain — a good test of the architecture's extensibility.

This is also the answer to "can we keep extending the Application, Infrastructure and
Presentation layers?": yes — each addition lands cleanly in one or more of them while the
Domain (the risk + drift model) stays untouched.

## Decision

- **A GitHub-issue channel** (`GitHubIssueDriftNotifier`, **Infrastructure**) implements
  the existing `IDriftNotifier` port and opens an issue via the REST API
  (`POST /repos/{owner}/{repo}/issues`). Issue title/body come from a pure `DriftIssue`
  formatter (**Application**), so wording is unit-tested without the HTTP client.
- **Fan-out, not a switch** (`CompositeDriftNotifier`, **Infrastructure**): the registered
  `IDriftNotifier` is composed from *all* configured channels. `AddInfrastructure` builds
  the list from config (`Alerts:SlackWebhookUrl`, `Alerts:GitHubRepo`) and falls back to
  the no-op notifier when none are set. Adding a third channel later is one class + one
  registration — `RunScanHandler` never changes.
- **Drift digest** (`DriftDigestBuilder` + `GetDriftDigestQuery/Handler`, **Application**):
  computes drift for every tracked root (reusing `GetTrackedRootsAsync` and the
  `DriftAnalyzer`) and renders one Markdown report, served at `GET /api/drift/digest`
  (**Presentation**). No new persistence — it composes parts that already exist.

## Consequences

- The notification side is now genuinely pluggable; channels are independent and
  best-effort (one flaky channel never blocks the others, and never fails a scan).
- Each feature demonstrably spans the layers it should and **adds nothing to the Domain**,
  which is the point of the dependency-inversion boundaries.
- GitHub issues are created per drift (no de-dup/labels yet); with the opt-in, interval-
  bounded watchlist that is at most one issue per package per interval — refinement
  (find-or-update an open issue) is noted as a follow-up.

[ADR 0011]: 0011-autonomous-monitoring-and-badge.md
