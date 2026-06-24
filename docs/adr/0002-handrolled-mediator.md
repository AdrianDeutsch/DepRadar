# ADR 0002 — Hand-rolled mediator instead of MediatR

- Status: Accepted
- Date: 2026-06-23
- Deciders: Architecture

## Context

The application layer uses a CQRS-style request/handler split. MediatR is the usual
choice, but as of **July 2025 MediatR is commercially licensed** (Lucky Penny
Software, dual-license with a free community tier). DepRadar's entire reason to
exist is to **flag exactly this kind of OSS-to-commercial license shift** before it
surprises a team. Shipping a commercially-licensed mediator in our own core would
undercut the product's message.

## Decision

Implement a **lean, hand-rolled mediator** (~120 lines incl. docs) in
`DepRadar.Application/Messaging`:

- `IRequest<TResponse>`, `IRequestHandler<TRequest,TResponse>`, `ISender`.
- A cached wrapper resolves the single handler per request type (no `dynamic`).
- `IPipelineBehavior<TRequest,TResponse>` gives cross-cutting hooks
  (e.g. `LoggingBehavior`), which is the real value a mediator provides.

Alternatives considered:

- **Wolverine** (MIT): powerful but heavier, with its own conventions.
- **Cortex.Mediator** (MIT): a drop-in clone, but still an external library with its
  own lifecycle risk.
- **MediatR free tier**: rejected — a commercial dependency in the core contradicts
  the product thesis.

An architecture test (`No_layer_should_reference_the_commercial_MediatR_package`)
asserts MediatR never reappears in any layer.

## Consequences

- Zero third-party licensing risk in the application core ("eat your own dog food").
- We own ~120 lines of dispatch code and its tests.
- The public surface mirrors MediatR's idioms, so the code stays familiar to reviewers.
