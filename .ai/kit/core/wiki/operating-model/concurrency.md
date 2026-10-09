---
owns: "Whether units may run at the same time and how many: read and write scope analysis, ordering rules, machine capacity, the node budget, and fan-in"
volatility: evolving
reviewed: 2026-10-09
---

# Concurrency

Running units at the same time saves time only when they cannot corrupt each other's inputs and the machine can carry them. This page owns both checks; `work-decompose` applies them when splitting a request.

## When

- a request splits into two or more units,
- deciding how many units or subagents to run at once on this machine.

## Route Away When

- how one owner runs and hands off: [delegation.md](delegation.md),
- splitting the request itself: the `work-decompose` skill.

## Scope Analysis

Before ordering units, record two sets for each one:

- **Write set**: every file, document, and shared resource the unit changes, including generated output its source edits cause, and mutable resources it uses (database, port, build cache, device).
- **Read set**: every file and document whose content the unit relies on: rule pages, profile, glossary, specs, decision records, shared contracts and types, and code it calls or imitates.

Read sets are the ones agents underestimate. When unsure whether a unit relies on something another unit changes, count it as a read.

## Ordering Rules

| Relation between units A and B | Rule |
|---|---|
| A and B write the same thing | Never at the same time: order them, or merge them into one unit |
| A writes what B reads | B waits until A is merged, and B's worktree is cut after that (or stacked on A's branch). B never works from a half-finished version |
| Each writes what the other reads | A cycle: merge the units, or re-split so every shared item has one writer that runs first |
| Neither | Independent: may run at the same time, within «Capacity» |

Example: unit `q` edits document `a`; units `w` and `e` read `a`. Run `q` first; start `w` and `e` after `q` merges (together, if they are independent of each other).

- A change to what agents themselves follow (kit pages, profile, glossary, shared specs) is read by every later unit: schedule it first and alone.
- Also order units that would race on one external resource (the same database, port, emulator, or deployment target), even when their files differ.
- Each unit already has its own issue, worktree, and task branch ([request-lifecycle.md](request-lifecycle.md)), so independent units never share a working tree.

## Capacity

Every concurrent unit starts local processes: language servers, builds, test runners, dev servers, emulators, containers. Running more than the machine holds causes memory exhaustion, swapping, killed processes, and failures that look like code defects.

1. Before running units at the same time, run `agentkit.py capacity`. It reports CPU cores, total and available memory, and a recommended number of concurrent units per cost class from the tables below.
2. Classify each unit by its heaviest step.
3. Run at most the smallest of: the recommendation for the heaviest class among the ready units, `policy.max_parallel_units` when it is above 0, and the node budget.
4. Re-check before each new wave. When memory pressure appears (swapping, killed processes, builds suddenly much slower), finish running units before starting more, and lower the limit.
5. When capacity cannot be measured, run `heavy` units one at a time and at most two `standard` units together.

<!-- agentkit:table capacity -->
| Cost class | Memory per unit (GB) | Units per CPU core | Typical work |
|---|---|---|---|
| `light` | 0.5 | 1 | Docs, configuration, small scripts; no builds or test runs |
| `standard` | 2 | 0.5 | Code changes with a language server, unit tests, linters |
| `heavy` | 6 | 0.25 | Full builds, game engines, mobile emulators, browser end-to-end tests, containers |

Memory kept free for the operating system, the editor, and the main session:

<!-- agentkit:table capacity-reserve -->
| Setting | Value |
|---|---|
| `reserve_fraction` | 0.25 |
| `reserve_min_gb` | 2 |

The reserve is the larger of the two: a fraction of total memory or the minimum. Units run by remote or cloud agents use no local memory, but rate limits and cost still bound them through «Node Budget».

## Node Budget

- `orchestrator` sets a maximum number of units and of concurrent units in the plan before dispatching, recording the capacity result it used.
- A unit needs a distinct deliverable and a reason it cannot merge into a neighbor. No speculative, duplicate, or exploratory fan-out.
- Only `orchestrator` adds units; it re-checks the budget when it does.

## Fan-In

1. Each unit returns focused commits and evidence in its own pull request into the integration branch; its reviewer approves the exact commit IDs.
2. Pull requests merge in dependency order. A dependent unit's worktree is cut after its predecessors merged, or stacked on a predecessor's branch with the merge order stated in both pull requests.
3. When units must be verified together before merging, `release-manager` combines the approved branches in dependency order on one temporary branch and stops on any conflict it cannot resolve from both sides' stated intent; the plan's named owner runs combined verification there and the reviewer gates it.
4. A dependent unit becomes ready only after its predecessors are merged.
