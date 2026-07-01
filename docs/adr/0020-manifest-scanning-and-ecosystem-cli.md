# ADR 0020 — Manifest scanning and the unified ecosystem CLI

- Status: Accepted
- Date: 2026-07-01
- Deciders: Architecture

## Context

The npm/PyPI commands ([ADR 0016], [ADR 0017]) accepted only a single package name,
while the NuGet `scan` already took a whole `.csproj`. Real CI usage scans the
*project's* manifest, not one package at a time. Two further debts sat next to that
gap: a version argument that wasn't an exact version was **silently ignored**
(`depradar npm express "^4"` scanned latest 5.x), and the three OSV vulnerability
sources were near-duplicates with `GetFixedVersionAsync` stubbed to `null` for
npm/PyPI even though OSV serves the patched version for every ecosystem.

## Decision

- **Manifests are first-class scan targets.** Two pure Application parsers —
  `NpmManifest` (package.json `dependencies`) and `RequirementsFile` (pip
  requirements syntax: comments, `\` continuations, option lines, PEP 508 lines via
  the existing `PyPiRequirement`) — expand a file into direct dependencies. The CLI
  scans each root, unions the graphs (`GraphMerge`, extracted from `scan`), and
  gates the merged result once.
- **Range-aware root resolution.** The scanners now resolve a non-exact version
  argument the way the package manager would: npm ranges via `NpmRange.BestMatch`,
  PEP 440 specifiers via `PyPiSpecifier.BestMatch`, against the registry's published
  versions. An unsatisfiable spec is a **miss**, never a silent fallback to latest.
- **One OSV protocol.** `OsvProtocol` centralizes the `/v1/query` and
  `/v1/vulns/{id}` wire logic; the NuGet/npm/PyPI sources are thin adapters differing
  only in ecosystem name and version grammar (`SemVer.TryParse` vs
  `PyPiVersion.TryParse`). `GetFixedVersionAsync` now works for every ecosystem.
- **One ecosystem command.** `EcosystemCommand` carries the shared CLI engine
  (args, manifest expansion, merge, policy, report, `--sbom`/`--sarif`);
  `NpmCommand`/`PyPiCommand` shrink to declarative specs. SBOM/SARIF export thereby
  reaches npm/PyPI with zero new writer code.

## Consequences

- `devDependencies` are deliberately excluded (what `npm install --omit=dev`
  deploys); nested requirement files (`-r`), editable installs and git/file/workspace
  specifiers are not registry packages — they surface in the report's `unresolved`
  list instead of failing the scan.
- A manifest scan takes no version argument; mixing both is a usage error.
- Fixed-version hints unblock a future `depradar fix` for npm/PyPI manifests.
- Verified live: `npm ./package.json` resolved 71 packages with `^4.18.0` →
  express 4.22.2 (not latest 5.x) and reported the git dependency as unresolved;
  `pypi ./requirements.txt` honored `>=1.21.1,<1.24` → urllib3 1.23, merged multiple
  roots, and wrote CycloneDX + SARIF artifacts.

[ADR 0016]: 0016-multi-ecosystem-npm.md
[ADR 0017]: 0017-multi-ecosystem-pypi.md
