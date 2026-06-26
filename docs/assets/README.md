# Visual assets

| Asset           | What it shows                                                        |
| --------------- | -------------------------------------------------------------------- |
| `banner.svg`    | Repository header banner.                                            |
| `demo.gif`      | Dashboard tour: landing → healthy graph → a risky package flagged.   |
| `landing.png`   | The landing view (intro + clickable example packages).              |
| `dashboard.png` | A healthy 24-package transitive graph + sortable risk ranking.       |
| `risk.png`      | A risky package flagged across categories, + SBOM, upgrade advice & graph chat. |
| `diff.png`      | Upgrade-impact diff: Newtonsoft 12.0.3 → 13.0.3 clears a CVE (+30 health). |
| `drift.png`     | Drift since the previous scan: newly deprecated/archived/vulnerable (−65 health). |
| `graph.png`     | A larger (28-package) dependency graph.                              |

These were captured live from the running dashboard. To re-capture, run the app and
screenshot the deep-link `http://localhost:<port>/?package=<id>` after scanning a package.
