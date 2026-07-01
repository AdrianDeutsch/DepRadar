# ADR 0024 — Multi-ecosystem support (Cargo / crates.io)

- Status: Accepted
- Date: 2026-07-01
- Deciders: Architecture

## Context

npm ([ADR 0016]) proved the ports generalize; PyPI ([ADR 0017]) proved they survive a
non-SemVer grammar. Cargo is the first ecosystem added *after* the consolidation wave
(EcosystemCommand, OsvProtocol, TomlLockfile, exploit intelligence) — the test of how
cheap ecosystem N+1 has actually become.

## Decision

- **The Domain did not change** (fourth time). Cargo versions are real SemVer; crate
  names flow through `PackageId.FromNormalized` lower-cased.
- **`CargoReq` is a normalizer, not a matcher.** Cargo's requirement grammar is npm's
  with two twists — a bare requirement means *caret* (npm: exact) and comparators are
  comma-separated (npm: space). `CargoReq` rewrites onto `NpmRange` instead of
  duplicating comparator logic.
- **crates.io facts map cleanly onto the model:** a version's `yanked` flag is the
  deprecation signal, and yanked versions are excluded from requirement resolution —
  exactly Cargo's behavior (a pinned/lockfile scan may still target one deliberately).
  Only `normal`, non-optional dependencies are followed (what `cargo build` pulls for a
  downstream consumer). OSV serves RUSTSEC advisories under `ecosystem=crates.io`
  through the shared `OsvProtocol`; EPSS/KEV escalation applies automatically.
- **Everything else was reuse.** `Cargo.toml` parsing is a small pure parser;
  `Cargo.lock` shares the `[[package]]` shape with poetry.lock/uv.lock, so
  `TomlLockfile` (extracted from the PyPI parser) covers it. The CLI verb, manifest +
  lockfile targets, `--sbom`/`--sarif`, the policy gate and the Action's
  `ecosystem: cargo` all came from the existing seams.

## Consequences

- Ecosystem N+1 has shrunk to: a registry client, a thin resolver, ~30 lines of
  requirement normalization, two small parsers and a DI block — the consolidation paid
  for itself.
- One deliberate deviation: a fully-specified version argument pins exactly (a
  deterministic assessment of what was declared), while Cargo itself would treat it as
  caret; partial/operator forms follow Cargo semantics.
- Auto-fix for `Cargo.toml` stays out of scope for now (same line as npm/PyPI initially).
- Verified live: `cargo regex "=1.5.4"` resolves the transitive graph (regex-syntax)
  and flags CVE-2022-24713; `Cargo.toml` and `Cargo.lock` scans skip dev-dependencies
  and assess the exact locked set (smallvec 1.6.0 escalated via EPSS).

[ADR 0016]: 0016-multi-ecosystem-npm.md
[ADR 0017]: 0017-multi-ecosystem-pypi.md
[ADR 0020]: 0020-manifest-scanning-and-ecosystem-cli.md
[ADR 0023]: 0023-lockfile-scanning.md
