# ADR 0014 — Remediation: the minimal safe upgrade

- Status: Accepted
- Date: 2026-06-27
- Deciders: Architecture

## Context

Knowing a package is vulnerable is half the job; the other half is *what do I upgrade to?*
The answer should be the **least disruptive** fix: the smallest version that clears every
advisory on that package.

## Decision

- **Read the patched version from the advisory, don't guess by re-scanning.** A
  `GetRemediationsHandler` walks the vulnerable nodes of the assessed graph and, per
  advisory, asks the vulnerability source for the smallest fixed version above the current
  one. A pure `RemediationCalculator` then takes the **highest** of those per-advisory
  fixes — the version that clears them all — or reports none when any advisory is unpatched.
- **Look up fixes by advisory id, not package name.** `IVulnerabilitySource` gained
  `GetFixedVersionAsync(advisoryId, package, aboveVersion)`, implemented against OSV's
  `/v1/vulns/{id}` endpoint (which returns the affected ranges + `fixed` events). This is
  keyed by the GHSA/CVE id, so it sidesteps the casing problem below.

## The gotcha that shaped this

OSV's NuGet `/v1/query` is **case-sensitive on the package name** — `Newtonsoft.Json`
returns the advisory, `newtonsoft.json` returns nothing. The scan path is fine (the NuGet
resolver carries the canonical casing), but ids reloaded from Postgres are normalized to
lower-case, so an early version of remediation that re-queried OSV per candidate version
silently reported a still-vulnerable version as "safe". Querying by **advisory id** (and
matching the package case-insensitively *within* the returned advisory) is both correct and
robust, and avoids per-candidate version probing entirely.

## Consequences

- Remediation is exact (driven by the advisory's own `fixed` data) and cheap (one lookup
  per distinct advisory, cached), surfaced at `GET /packages/{id}/remediation`, in the CLI,
  and inline in the dashboard drill-down ("fix available: upgrade to …").
- The case-sensitivity lesson is captured here so the OSV-by-name pitfall isn't reintroduced.
- Opening the fix as a pull request is the natural next layer on top of this computation.

## Follow-up since shipped: `depradar fix` (apply the fix)

- A pure `ManifestPatcher` rewrites the `Version=` of the named
  `PackageReference`/`PackageVersion` entries with a **targeted text edit** (not an XML
  re-serialize), so formatting, comments and ordering survive — important for a clean PR.
- The CLI `fix` command parses the manifest's versioned references, finds the vulnerable
  *direct* dependencies (OSV at the declared version) and their safe versions, applies the
  bumps, then either writes the file in place or — with `--open-pr` — opens a PR.
- An `IPullRequestOpener` port (Null by default, `GitHubPullRequestOpener` when a token is
  set) raises the PR via the GitHub REST API: read the base ref, create a branch, commit
  the patched file, open the PR. Same keyless-by-default seam as the alert channels.
- **Transitive fixes via parent-bump.** `fix` now scores the *whole graph* of each direct
  dependency (via `SafeUpgradeFinder` + `ProjectAnalyzer`) and bumps it to the smallest
  version whose transitive closure is advisory-free. Because newer parents pull newer
  transitives, this clears transitive CVEs too — and when no parent version resolves a
  clean graph (e.g. a deprecated package), it says so rather than pretending. Bounded to
  `SafeUpgradeFinder.MaxCandidates` resolutions per dependency, cached.
- **Scheduled auto-fix.** A `dependency-autofix` workflow (cron + dispatch) runs
  `depradar fix --open-pr` so fix PRs appear on a cadence, the same way Dependabot does.
