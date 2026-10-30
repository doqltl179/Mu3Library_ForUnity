---
owns: "How a change to the kit enters and flows: sources of change, the intake record, triage, impact classes, and the path from intake to release"
volatility: evolving
reviewed: 2026-10-09
---

# Change Intake

Every change to the kit, whatever triggered it, enters through one record and one flow, so nothing is applied on impulse and nothing observed is lost.

## When

- something outside or inside the kit suggests the kit is wrong, incomplete, or outdated,
- a project proposes a change upstream.

## Route Away When

- routine re-verification on a schedule: [freshness-policy.md](freshness-policy.md),
- importing an external article or prompt: [external-guidance.md](external-guidance.md).

## Sources Of Change

| Source | Typical trigger | Filed by |
|---|---|---|
| Radar scan | Tool, model, standard, or framework release | `trend-scout` via `kit-trend-scan` |
| Model generation change | New model family or tier behavior | `trend-scout`; see [model-calibration.md](model-calibration.md) |
| Project feedback | A lesson that applies beyond one project | Any agent via `kit-upstream-propose` |
| Failure | An agent followed the kit and produced a wrong result | The agent that observed it |
| Freshness review | A file found wrong during review | `kit-librarian` |
| Maintainer request | A new need or idea | The maintainer |

## Intake Record

An issue in the kit repository (`evolution.upstream`) labeled `intake`, stating:

1. **Source** — URL or origin, and the date observed.
2. **Change** — what is different, in one paragraph; facts separated from inferences.
3. **Affected files** — kit files that are now wrong or incomplete.
4. **Impact class** — per «Impact Classes».
5. **Proposed action** — edit, add, deprecate, or no action, with the owning file.

When the hosting platform is unavailable, append the same fields to `.ai/project/intake-pending.md`, tell the user, and delete each entry once it is filed as an issue. Plan files are not used: they are deleted at closeout.

## Triage

- `kit-librarian` triages: accept, merge into an existing record, defer with a reason, or reject with a reason.
- Changes to roles, routing, structure, or invariants also need `role-governor`.
- An accepted record becomes one bounded update unit, or several if it touches unrelated owners.

## Impact Classes

| Class | Examples | Version bump |
|---|---|---|
| `patch` | Fact correction, wording, a broken link, a re-verified date | patch |
| `minor` | New agent, skill, stack pack, page, adapter, or optional profile key; additive behavior | minor |
| `major` | Renamed or removed asset or profile key, changed generated layout, changed invariant | major |

## Flow

1. Intake record → triage.
2. Task branch in the kit repository; apply the change with the matching `kit-*` skill.
3. `agentkit.py sync` and `agentkit.py check` pass.
4. Review: `code-reviewer`, plus `role-governor` for structural changes.
5. Changelog entry under Unreleased, with migration steps for `minor` and `major`, per [versioning.md](versioning.md).
6. Release; projects adopt it with `kit-update`.
