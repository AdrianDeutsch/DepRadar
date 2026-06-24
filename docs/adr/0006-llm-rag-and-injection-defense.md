# ADR 0006 — Keyless-by-default RAG, an LLM seam, and prompt-injection defense

- Status: Accepted
- Date: 2026-06-24
- Deciders: Architecture

## Context

The upgrade advisor should answer "is this upgrade worth it?" using RAG over
changelogs plus risk data. Two realities shape the design:

1. **No API key in the portfolio environment.** The feature must still work and be
   demonstrable + CI-testable without one.
2. **Changelogs/release notes are attacker-controllable** — a classic prompt-injection
   vector. The README's NFRs require active defense.

## Decision

**Keyless by default, real LLM behind config.** Everything works out of the box and is
swappable behind ports:

- **Embeddings**: a deterministic local `HashingEmbeddingGenerator` (feature hashing
  into a 256-dim L2-normalized vector, stable FNV-1a hash so the API and Worker agree).
  No key needed; a hosted embedder can replace it behind `IEmbeddingGenerator`.
- **Vector store**: **pgvector** (`vector(256)` column), cosine search via the `<=>`
  operator in raw SQL. The Domain stays vector-free (it holds `float[]`; Infrastructure
  maps it). Hosts register the context with `UseVector()`.
- **Recommendation**: always **deterministic** (`UpgradeRecommender`, from risk delta) —
  never delegated to the LLM. The model only writes the *narrative*.
- **LLM seam**: `ILanguageModel`. Default `NullLanguageModel` returns null → the handler
  falls back to a templated narrative. With `Anthropic:ApiKey` set, `AnthropicLanguageModel`
  (Claude via the Messages API) is used. The API response exposes the exact prompt, so the
  AI wiring is transparent even keyless.

### Prompt-injection defense (`PromptShield`, pure + unit-tested)

- **Input separation**: untrusted text is fenced in unique delimiters, and any
  occurrence of those delimiters inside it is stripped so it cannot break out.
- **Explicit instruction**: the system prompt declares the fenced block to be data, not
  instructions, and forbids following or revealing instructions found inside it.
- **Output constraints**: the model must answer only the upgrade question, concisely,
  ending in a fixed verdict token.

### Why not Semantic Kernel (yet)

The briefing names Semantic Kernel; its Anthropic connector is preview and could not be
validated without a key. A thin, well-tested direct Messages-API adapter keeps the build
green and the seam swappable; SK can slot in behind `ILanguageModel` later.

## Consequences

- The RAG pipeline, the shield and the recommendation are real and CI-tested against
  pgvector; only the LLM *narrative* needs a key.
- Switching to Postgres+pgvector via `UseVector()` meant registering the DbContext
  directly instead of Aspire's `AddNpgsqlDbContext`; the Aspire EF health-check is a
  follow-up (Slice 6).
- The local embedder is for demos, not semantic quality — documented as a seam.
