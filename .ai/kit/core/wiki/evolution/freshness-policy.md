---
owns: "Freshness metadata on every source file, the review cadence per volatility, what counts as reviewed, and how overdue files are handled"
volatility: stable
reviewed: 2026-10-09
---

# Freshness Policy

Every source file says how fast its content goes stale and when it was last verified. `agentkit.py freshness` turns that into a review queue.

## When

- writing frontmatter for any page, card, skill, or stack pack,
- a file is overdue, or an external change may have made it wrong.

## Route Away When

- the review procedure: the `kit-freshness-review` skill,
- reacting to a specific external change: [change-intake.md](change-intake.md).

## Metadata

| Field | Meaning |
|---|---|
| `volatility` | How fast the content goes stale; one value from «Cadence» |
| `reviewed` | `YYYY-MM-DD` of the last verification against sources and reality |
| `sources` | Primary sources a reviewer re-checks; required for `volatile` pages and every stack pack |

## Cadence

<!-- agentkit:table cadence -->
| Volatility | Review every (days) | Use for |
|---|---|---|
| `stable` | 365 | Principles and structure that do not depend on external tools |
| `evolving` | 180 | Workflows, roles, and procedures that change with practice |
| `volatile` | 90 | External facts: tool file formats, model behavior, framework versions |

## What Reviewed Means

- Re-read the file against its `sources` and against how the kit and projects actually behave today.
- Fix what is wrong, then set `reviewed` to today.
- Bumping `reviewed` without verifying is faking success under [integrity.md](../principles/integrity.md).
- A file whose facts cannot be verified now keeps its old date and gets an intake record explaining what is unknown.

## Overdue Handling

- `agentkit.py check` warns about overdue files; `agentkit.py freshness --strict` fails, which the kit repository's scheduled CI uses to open a review issue.
- Review overdue files with `kit-freshness-review`, volatile files first.
- In an installed project, `agentkit.py freshness --kit` shows overdue kit files: the remedy is `kit-update`, not a local edit.

## Early Review Triggers

Review a file before its due date when:

- an intake record names it,
- a tool or model release changes behavior it describes,
- a correction or lesson contradicts it.
