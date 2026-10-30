---
owns: "How the kit stays current: freshness review, external change intake, the tech radar, model calibration, versioning, and adopting outside guidance"
volatility: stable
reviewed: 2026-10-09
---

# Evolution

Tools, models, frameworks, and practices change faster than any document. This section owns the machinery that keeps the kit current instead of trusting it to stay correct.

The loop: the **tech radar** says what to watch → `trend-scout` scans it and files **intake** records → accepted changes become update units → **versioning** ships them with migration notes → projects run `kit-update`. In parallel, **freshness** metadata forces every file to be re-verified on a cadence, and **model calibration** retires rules that newer models no longer need.

<!-- agentkit:begin index -->
| Page | Owns |
|---|---|
| [change-intake.md](change-intake.md) | How a change to the kit enters and flows: sources of change, the intake record, triage, impact classes, and the path from intake to release |
| [external-guidance.md](external-guidance.md) | How outside material (vendor docs, articles, other prompts, community practice) is evaluated and adopted into the kit: source priority, adoption rules, and keep-or-reject criteria |
| [freshness-policy.md](freshness-policy.md) | Freshness metadata on every source file, the review cadence per volatility, what counts as reviewed, and how overdue files are handled |
| [model-calibration.md](model-calibration.md) | How the kit adapts to changing model capability: scaffold rules, tier-to-model mapping, and the calibration procedure |
| [tech-radar.md](tech-radar.md) | The watchlist of external tools, standards, and model families the kit depends on, with their sources, status, and last check date |
| [versioning.md](versioning.md) | Kit versioning: version numbers, the kit changelog format, migration notes for projects, and how projects consume updates |
<!-- agentkit:end index -->
