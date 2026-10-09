---
name: adr-write
description: "Write an architecture decision record in docs.adr_dir with context, options considered and their trade-offs, the decision, consequences, and status, numbered sequentially; supersede an accepted record instead of editing it. Use when a decision about structure, technology, interfaces, data, or cross-cutting conventions is made or proposed and is costly to reverse."
category: docs
volatility: evolving
reviewed: 2026-10-09
---

# Write Architecture Decision Record

## Use When
- A decision is costly to reverse: architecture, technology or vendor choice, public interface, data model, or cross-cutting convention.
- A plan, a review, or the user asks to record a decision.
- An accepted record no longer holds and must be superseded.

## Do Not Use When
- Deciding what to build rather than how → `requirements-spec-write`.
- Describing how existing behavior works → `project-docs-sync`.
- Changing a kit rule or policy → `kit-page-update`.

## Inputs
- The decision question, its drivers (constraints, quality attributes, deadlines), and who owns the decision.
- The options and the evidence for each: benchmarks, prototypes, vendor docs, incidents.
- `docs.adr_dir` and `project.language` from the profile.

## Steps
1. Read «Decision Records» in [documentation.md](../../wiki/workflows/documentation.md) for file naming and statuses.
2. Search existing records on the same topic: `ls <docs.adr_dir>` and `rg -il "<topic terms>" <docs.adr_dir>`. If an accepted record answers the same question, the new record supersedes it.
3. Name the file per «Decision Records» in documentation.md, taking the number after the highest existing one.
4. Write Context: the problem, drivers, and constraints as facts, with no solution yet.
5. Write Options Considered: at least two real options, each with benefits, costs, risks, and evidence. Include keeping the status quo when it is viable.
6. Write Decision: the chosen option and the driver that decided it.
7. Write Consequences: what becomes easier, what becomes harder, required follow-up work, and the signal that would trigger revisiting the decision.
8. Set the status per «Decision Records» in documentation.md, keeping `proposed` until the decision owner accepts. When the user owns the decision, pause and ask per «Stop, Pause, or Proceed» in [integrity.md](../../wiki/principles/integrity.md), with the options and your recommendation.
9. When superseding, change the old record only as «Decision Records» allows, and link the old and new records to each other.
10. Register follow-up work as issues with `github-issue-create`, then commit with `git-commit`.

## Output
- The record's path, number, and status; the superseded record, if any; follow-up issues created.
