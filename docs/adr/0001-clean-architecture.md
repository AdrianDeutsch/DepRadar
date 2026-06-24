# ADR 0001 — Clean Architecture with enforced layer boundaries

- Status: Accepted
- Date: 2026-06-23
- Deciders: Architecture

## Context

DepRadar ingests data from several external APIs, builds a graph, scores risk and
adds an LLM layer. These concerns evolve at different speeds and must stay
independently testable. The domain model (packages, versions, the dependency graph)
is the stable core; persistence, HTTP clients and AI are volatile details.

## Decision

We use **Clean Architecture** with four layers and a strictly inward dependency
direction:

```
Domain  ←  Application  ←  Infrastructure  ←  Presentation (Api / Worker / AppHost)
```

- **Domain** has zero external dependencies — no EF Core, no DI, no mediator types.
- **Application** holds use cases and ports (interfaces); it depends only on Domain.
- **Infrastructure** implements the ports (EF Core, deps.dev client).
- **Presentation** composes everything (Aspire-orchestrated Api + Worker).

The rule is **enforced in CI** by NetArchTest architecture tests, not just by
convention (`tests/DepRadar.Architecture.Tests`).

Vertical Slice Architecture was considered. It was rejected for this project
because the explicit, reviewable layer boundaries are part of the portfolio intent,
and the ceremony is contained by keeping the layers thin.

## Consequences

- Business rules are unit-testable without any infrastructure.
- Swapping a data source or the persistence engine touches only Infrastructure.
- A small amount of mapping/DTO boilerplate is the accepted cost.
