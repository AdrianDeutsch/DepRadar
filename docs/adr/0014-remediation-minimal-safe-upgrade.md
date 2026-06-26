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
