# Visual assets

| Asset           | What it shows                                                        |
| --------------- | -------------------------------------------------------------------- |
| `banner.svg`    | Repository header banner.                                            |
| `demo.gif`      | Dashboard tour: landing → healthy graph → a risky package flagged.   |
| `landing.png`   | The landing view (intro + clickable example packages).              |
| `dashboard.png` | A healthy 24-package transitive graph + sortable risk ranking.       |
| `risk.png`      | A risky package flagged across categories, + SBOM, upgrade advice & graph chat. |
| `graph.png`     | A larger (28-package) dependency graph.                              |

These were captured live from the running dashboard. To re-capture, run the app and
screenshot the deep-link `http://localhost:<port>/?package=<id>` after scanning a package.
