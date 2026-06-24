# ADR 0007 — Live progress via a DB-poll SignalR broadcaster, and a static dashboard

- Status: Accepted
- Date: 2026-06-25
- Deciders: Architecture

## Context

The scan is processed in the **Worker**, but live progress must reach a browser
connected to the **API** (separate processes — see ADR 0004). The portfolio also
needs a visible dashboard and an audit-ready export.

## Decision

- **Live updates**: the API hosts a SignalR `ScanHub`; clients join a group per scan
  id. A hosted `ScanProgressBroadcaster` polls Postgres (the source of truth the Worker
  writes to) every second and pushes status changes to the relevant group. This bridges
  the process split with **no message broker** and survives restarts. Per-scan grouping
  keeps each client to its own scan.
- **Dashboard**: a **static `wwwroot`** SPA (plain HTML/JS/CSS, `@microsoft/signalr` +
  `cytoscape` from CDN) served by the API. No SPA build step, no extra toolchain — it
  calls the existing JSON endpoints and the hub. It shows live progress, the graph
  (nodes colored by risk), a sortable risk ranking with drill-down, and upgrade advice.
- **Report**: `GET /packages/{id}/report` returns **Markdown** assembled by a pure
  `ReportBuilder` from the existing graph-risk + upgrade queries (composed via the
  mediator). Markdown is keyless, diff-able and audit-friendly; PDF is a follow-up.

## Consequences

- Live, demoable progress without Redis/back-plane; a 1s poll adds trivial DB load.
- The dashboard ships as files — easy to host, trivial to review — at the cost of a
  hand-rolled SPA rather than Blazor/React.
- Verified end-to-end live: a real scan (NuGet + OSV) drives the hub, graph, risk and
  report; only the LLM narrative needs a key.
