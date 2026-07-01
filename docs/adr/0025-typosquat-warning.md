# ADR 0025 — Typosquat warning (lookalike detection)

- Status: Accepted
- Date: 2026-07-02
- Deciders: Architecture

## Context

Typosquatting is a first-order supply-chain attack: publish `lodahs`, wait for typos of
`lodash`. The typo happens where a *human writes the name* — at `npm install` or in a
manifest edit — and a squat package often looks perfectly healthy (no CVEs, fine
license), so the risk model alone cannot see it.

## Decision

- **A pure detector, checked at the human boundary.** `Lookalike` computes the
  Damerau–Levenshtein distance (edits + adjacent transpositions — the classic typo
  operations) of each **direct target** against a curated top-package list per
  ecosystem. Transitive and lockfile packages are never checked — they were resolved by
  a machine, not typed by a human.
- **Curated, embedded lists** (`KnownPackages`, ~80–90 names per ecosystem): squatters
  imitate the most-downloaded names, so the list only needs to cover the popular
  targets. Embedding keeps the check deterministic, offline and testable.
- **Conservative thresholds, and a warning — never a gate.** Distance 1 for names of
  four-plus characters, distance 2 only for ten-plus; an exact match with a known
  package is of course fine. Lookalike detection has inherent false positives
  (`expresso` vs `express`), so the result is a prominent report warning (text +
  `warnings` array in JSON) that does not change the exit code — the policy gate stays
  driven by verifiable facts.

## Consequences

- The warning also fires when the typo'd package does **not** exist — turning a bare
  "not found" into "did you mean `requests`?".
- Verified live: `npm expres` (a real, empty crate-squat with a perfect health score)
  and `lodahs` (npm's `0.0.1-security` malware placeholder) are both flagged; `requets`
  yields the did-you-mean hint before the not-found error.
- The lists are a maintenance point; they change slowly (popularity is sticky) and a
  stale list only weakens the warning, never breaks a scan.

[ADR 0020]: 0020-manifest-scanning-and-ecosystem-cli.md
