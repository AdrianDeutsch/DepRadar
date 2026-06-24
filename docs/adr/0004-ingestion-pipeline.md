# ADR 0004 — Durable scan queue + in-process Channels pipeline in the Worker

- Status: Accepted
- Date: 2026-06-24
- Deciders: Architecture

## Context

A transitive scan is slow (many NuGet calls) and must not block the HTTP request.
Aspire runs the API and the Worker as **separate processes**, so a purely in-process
`System.Threading.Channels` pipeline cannot span the two. We also want scans to
survive a restart and the API to stay decoupled from how/where work runs.

## Decision

A **two-stage** pipeline:

1. The API handles `POST /api/packages/{id}/scan` by creating a `Scan` row in state
   `Queued` and returning **202 Accepted** with a `Location` to `/api/scans/{id}`.
   Postgres is the durable queue.
2. The **Worker** runs the processing pipeline with `System.Threading.Channels`:
   - `ScanPollingService` (producer) polls the DB for `Queued` scans every 2s and
     writes their ids into a bounded `Channel<ScanId>` (with an in-flight set to
     avoid re-enqueuing the same scan).
   - `ScanConsumerService` (consumer) drains the channel and runs each scan through
     the mediator (`RunScanCommand`) in its own DI scope.

The `RunScanHandler` owns the `Scan` lifecycle (`Running` → `Completed`/`Failed`),
so a single bad scan never stops the pipeline, and re-delivery is idempotent.

### Alternatives considered

- **In-process Channel fed directly by the API**: simplest, but couples processing
  to the API process and loses durability — rejected given Aspire's process split.
- **Message broker (RabbitMQ/Azure Service Bus)**: durable and scalable, but adds
  infrastructure we don't need yet. The DB-poll is a deliberate "good enough" choice;
  swapping in a broker later only touches the producer.

## Consequences

- Scans are durable and the API returns instantly; status is pollable (and becomes
  the basis for SignalR live updates in Slice 5).
- Polling adds up to ~2s latency before a scan starts — acceptable, revisited if needed.
- A scan claimed (`Running`) when the worker crashes stays `Running`; a stale-scan
  reaper is a follow-up (Slice 6 hardening).
