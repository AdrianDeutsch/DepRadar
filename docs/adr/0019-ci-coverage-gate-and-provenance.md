# ADR 0019 — CI coverage gate + build provenance

- Status: Accepted
- Date: 2026-07-01
- Deciders: Architecture

## Context

Two self-review gaps: CI ran the tests but measured nothing, and the published
`DepRadar.Tool` package shipped with no provenance. For a supply-chain *security* tool,
its own pipeline is a fair thing to hold to the standard it enforces on others.

## Decision

- **Coverage as a floor, not a target.** `dotnet test` collects coverage
  (`coverlet.collector` on every test project); ReportGenerator merges the runs, writes a
  Markdown summary to the job summary, and a step fails the build only if line coverage
  falls below **40 %** (`assemblyfilters` restrict this to `DepRadar.*` product code, not
  the test assemblies). The floor is a regression tripwire — it catches "someone deleted
  half the suite", not "we're below an aspirational number" — and is meant to be raised as
  coverage grows. Measured locally at ~60 % for Domain+Application.
- **Keyless build provenance.** `publish.yml` runs `actions/attest-build-provenance` over
  the packed `.nupkg` using GitHub's OIDC token (Sigstore) — a SLSA provenance attestation
  with **no signing certificate to manage**. Consumers verify with
  `gh attestation verify <file>.nupkg -R AdrianDeutsch/DepRadar`.

## Consequences

- The pipeline now reports and defends a coverage floor and ships verifiable provenance —
  the tool meets a slice of its own bar.
- Provenance ≠ a signed package: NuGet **author signing** needs a real code-signing
  certificate, which is an owner-only, out-of-band step (the assistant must not create or
  handle signing keys). Build provenance is the keyless, automatable half; author signing
  is documented as the manual complement.
- The 40 % floor is deliberately conservative to avoid flaky failures on the first runs;
  raising it is a one-line change once the real CI number is observed.
