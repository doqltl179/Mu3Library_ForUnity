---
name: agent-role-audit
description: "Use when adding, refactoring, or reviewing agent documents in this repository to detect role overlap, missing ownership, weak boundaries, and whether an agent should be kept, narrowed, split, merged, or rejected."
---

# Agent Role Audit

## Purpose

Audit the structural fitness of the agent catalog before the framework grows further.

## Use This Skill When

- a new agent is proposed,
- an existing agent becomes too broad,
- two agents appear to touch the same files or decisions,
- the framework needs a continue-or-rework decision after an iteration.

## Workflow

1. List the agents involved in the current concern.
2. Compare mission, responsibilities, non-goals, inputs, and outputs.
3. Mark overlap severity.
4. Decide whether to keep, narrow, split, merge, defer, or reject.
5. Issue the structural disposition for the current iteration: continue or rework.
6. Record required catalog or routing changes.

## Output Expectations

- findings ordered by severity,
- overlap summary,
- ownership gaps,
- recommended structural action,
- follow-up edits required in docs or routing files.