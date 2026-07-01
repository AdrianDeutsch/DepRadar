# ADR 0023 — Lockfile scanning (package-lock.json, poetry.lock, uv.lock)

- Status: Accepted
- Date: 2026-07-01
- Deciders: Architecture

## Context

Manifest scans ([ADR 0020]) resolve ranges the way the package manager *would* — but a
lockfile records what *is* installed. Scanning the lockfile is therefore the most
precise risk statement a repo can get, and CI usually has one.

## Decision

- **The lockfile IS the resolution — nothing is re-resolved.** `ScanLockedAsync` on the
  scanner ports builds a flat `ResolvedGraph` straight from the locked (name, version)
  pairs and feeds it through the unchanged assessment pipeline via a
  `FixedGraphResolver` — so vulnerabilities, exploit intelligence (EPSS/KEV) and scoring
  all apply identically. Re-resolving each entry would both be slow and describe a
  different (fresher) graph than the one actually installed.
- **Two pure parsers.** `NpmLockfile` reads `packages` from lockfile v2/3 (path keys →
  name after the last `node_modules/`, scoped and nested included; `dev: true`, links
  and versionless entries skipped, mirroring the manifest scan). `PyPiLockfile` reads
  the repeated `[[package]]` blocks shared by **poetry.lock and uv.lock** with a
  line-based reader — the files are machine-generated with a stable layout, so the
  Application layer stays free of a TOML dependency (the ADR 0003 trade-off again).
- **Same CLI, one more file kind.** `depradar npm ./package-lock.json` (also
  `npm-shrinkwrap.json`) and `depradar pypi ./poetry.lock` / `uv.lock` dispatch inside
  the existing `EcosystemCommand`; policy gate, `--sbom` and `--sarif` work unchanged.

## Consequences

- Registry documents are still fetched per package — but only for license/latest facts;
  a package that vanished from the registry is still assessed on its advisories.
- The flat graph has no edges, so path-based views are empty for lockfile scans; the
  risk ranking, policy gate and exports are unaffected.
- Non-final versions (PyPI pre/post/dev releases) are skipped and reported as a count —
  the same stable-release scope line as ADR 0017.
- Verified live: a v3 `package-lock.json` scanned exactly its two runtime entries
  (dev-only jest skipped) and failed the gate on minimist 1.2.5; a `poetry.lock`
  scanned requests/urllib3/pyyaml at their locked versions with EPSS-escalated
  severities.

[ADR 0017]: 0017-multi-ecosystem-pypi.md
[ADR 0020]: 0020-manifest-scanning-and-ecosystem-cli.md
