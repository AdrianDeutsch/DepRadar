# DepRadar.Tool (`depradar`)

A dependency-health CLI for .NET: scan a NuGet package or project for **security,
license and maintenance** risk, fail CI on policy violations, and remediate.

It runs the whole analysis **in-process against live data** (NuGet, OSV.dev, GitHub) —
no server and no database — so it works standalone in CI.

```bash
dotnet tool install --global DepRadar.Tool

# Scan a package or a project; exit code 1 fails the build on a policy breach
depradar scan WindowsAzure.Storage --fail-on high --no-deprecated
depradar scan ./MyApp.csproj --forbid copyleft --sbom sbom.json --sarif results.sarif

# Compare two versions (added/removed deps + CVEs introduced or cleared)
depradar diff Newtonsoft.Json 12.0.3 13.0.3

# Auto-fix: bump vulnerable dependencies to the minimal safe version (incl. transitive)
depradar fix ./MyApp.csproj                  # patch in place
depradar fix ./MyApp.csproj --open-pr --repo owner/name   # open a PR (needs GITHUB_TOKEN)
```

Exit codes: `0` policy passed · `1` policy violated · `2` usage error.

Part of [**DepRadar**](https://github.com/AdrianDeutsch/DepRadar) — a full dependency-health
platform (dashboard, SBOM/SARIF, drift monitoring, multi-channel alerts). MIT licensed.
