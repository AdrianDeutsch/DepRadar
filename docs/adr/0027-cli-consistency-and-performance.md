# ADR 0027 — CLI consistency and scan performance

- Status: Accepted
- Date: 2026-07-02
- Deciders: Architecture

## Context

Three gaps surfaced in a cross-layer review. (1) A committed `depradar.json` governed
only the NuGet `scan` — the ecosystem verbs silently ignored it, an inconsistency at
the edge of a bug for anyone relying on a repo-committed gate. (2) Advisory lookups ran
strictly sequentially: a 77-node Go graph meant 77 OSV round trips one after another
(41 s measured). (3) The README's terminal visual showed colors the CLI did not
actually produce.

## Decision

- **One policy resolution for every verb** (`CliPolicy`): an explicit `--policy` file,
  else an auto-detected `./depradar.json`, else the flag-built fallback — now shared by
  `scan`, `npm`, `pypi`, `cargo` and `go`. The file's full semantics (failOn,
  allowDeprecated, forbiddenLicenses, VEX-style ignore) apply to all ecosystems because
  `PolicyEvaluator` was always graph-generic.
- **Bounded-concurrent advisory lookups** in `ProjectAnalyzer` (8 at a time via
  `SemaphoreSlim` + `Task.WhenAll`, node order preserved). The bound stays well below
  the resilience handler's rate limits; HybridCache's stampede protection dedupes
  concurrent identical lookups (the KEV catalog fetch in particular).
- **ANSI colors in the text report** (`Ansi`): severity-colored levels, red
  VULN/FAILED, green PASSED, yellow warnings — disabled when stdout is redirected or
  `NO_COLOR` (no-color.org) is set, so CI logs and piped output stay byte-identical.

## Consequences

- Measured on the same cold-cache go.mod scan (77 modules): **41.2 s → 10.6 s** (3.9×).
- Verified live: a `depradar.json` with `failOn: medium` fails an npm manifest scan
  (exit 1) and its `ignore` list flips it back to exit 0 — the committed gate now
  really is the single source of truth.
- The README terminal capture is now honest — the CLI produces those colors on a TTY
  (12 ANSI sequences measured; 0 when piped).

[ADR 0020]: 0020-manifest-scanning-and-ecosystem-cli.md
