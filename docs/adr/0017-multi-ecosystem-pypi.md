# ADR 0017 — Multi-ecosystem support (PyPI)

- Status: Accepted
- Date: 2026-06-30
- Deciders: Architecture

## Context

npm ([ADR 0016]) proved the ports generalize beyond NuGet. PyPI is the harder second
test: unlike npm, **PyPI versions are not SemVer** (PEP 440 allows epochs, `~=`
compatible-release, `==1.4.*` wildcards, and pre/post/dev suffixes), and dependency
metadata (`requires_dist`) follows PEP 508 with environment markers and extras. If the
Domain survives PyPI unchanged, the seam is real.

## Decision

- **The Domain did not change.** The scorer, graph and risk model stay ecosystem-agnostic.
  PyPI project names flow through `PackageId.FromNormalized` after PEP 503 canonicalization
  (`PyPiName`), and PyPI releases are projected onto the existing `SemVer`.
- **Three pure resolvers (Application), fully unit-tested**, since they hold the only
  ecosystem-specific semantics:
  - `PyPiVersion` — normalizes a *final* PEP 440 release to `SemVer` (pads `1.4` → `1.4.0`,
    drops a zero epoch) and **rejects** pre/post/dev/local/non-zero-epoch versions, so
    resolution targets stable releases.
  - `PyPiSpecifier` — PEP 440 specifier matching (`~=`, `==X.*` wildcards, `===`, comma-AND)
    over `SemVer` comparisons, plus `BestMatch` (highest satisfying stable).
  - `PyPiRequirement` — parses one `requires_dist` (PEP 508) entry into name + specifier +
    whether it is `extra`-gated (optional).
- **New Infrastructure adapters only.** `PyPiRegistryClient` (the pypi.org JSON API,
  cached), `PyPiDependencyGraphResolver` (implements the existing `IDependencyGraphResolver`),
  and `PyPiVulnerabilitySource` (the same OSV endpoint with `ecosystem=PyPI`). They produce
  the same `ResolvedGraph` and `VulnerabilityRecord` the NuGet/npm paths do.
- **Reuse, not a fork.** `IPyPiScanner` wires the PyPI adapters into the *same* stateless
  `ProjectAnalyzer` (reusing npm's no-op metadata/repo-health sources), so the console
  renderer, policy gate and `GraphAssessment` are shared. The CLI exposes it as
  `depradar pypi <package> [version]`.

## Consequences

- PyPI splits a package across two documents — `/pypi/{name}/json` (release list + latest
  info) and `/pypi/{name}/{version}/json` (that version's `requires_dist`). The resolver
  fetches both (memoized per resolution); a node's dependencies come from its *own* version,
  not the latest.
- Optional (`extra`-gated) requirements are skipped, matching a default `pip install`.
  Environment markers (`python_version`, …) are not evaluated — a deliberate scope line; we
  resolve the unconditional runtime graph.
- PyPI has no package-level "deprecated" flag (yanking is per-file), so the maintenance
  signal is license + age + OSV only; `IsDeprecated` is always false for PyPI nodes.
- PyPI is read-only (scan + score); auto-fix/remediation and the dashboard/DB path stay
  NuGet-only.
- Verified live: `pypi urllib3 1.24.1` flags its CVEs (exit 1 under `--fail-on high`);
  `pypi requests 2.19.1` resolves the transitive graph (urllib3, idna, certifi, chardet) and
  surfaces advisories on requests/urllib3/idna.

[ADR 0016]: 0016-multi-ecosystem-npm.md
