---
owns: "Token and context economy: how to read, search, and summarize narrowly, and the line budgets per file type"
volatility: evolving
reviewed: 2026-10-09
---

# Context Budget

Context is the scarcest resource an agent has: everything loaded competes with the task, and long contexts degrade instruction following. These rules keep reading and writing small.

## When

- a task may read many files, logs, or long command output,
- a session is growing long,
- writing or reviewing any always-loaded or frequently loaded file.

## Route Away When

- what belongs in memory or records: [memory-policy.md](memory-policy.md).

## Reading Rules

1. Narrow before reading: search (`rg`, file lists, symbol search) for the exact term, then open only the matching range.
2. Follow routes: entry → section index → owning page. Never read a whole section to find one rule.
3. Do not read every agent card; read the catalog, then only the candidate cards.
4. Read a stack pack only when touching its files; tools may load it automatically.
5. Do not re-read files or output you already have unless they changed.

## Output Rules

1. Summarize command output to the result, the failure point, affected files, and the next action.
2. Answer with the smallest complete result.
3. In long sessions, keep a running summary in the plan (decisions, touched files, open risks, verification state) so work survives context compaction.

## Line Budgets

`agentkit.py check` enforces these limits; the first matching row applies.

<!-- agentkit:table budgets -->
| Files | Max lines | Why |
|---|---|---|
| `AGENTS.md` | 120 | Loaded into every session for every tool; includes the project's guardrails |
| `core/START.md` | 65 | Inlined into `AGENTS.md` |
| `core/wiki/**/README.md` | 40 | Section indexes: routing only |
| `core/wiki/**/*.md` | 150 | Read on demand; split by question when larger |
| `core/agents/**/*.md` | 45 | Loaded as a subagent prompt; description is read for every delegation decision |
| `core/skills/**/*.md` | 80 | Loaded when the skill triggers |
| `core/stacks/**/*.md` | 80 | May load automatically whenever matching files are touched |
| `core/templates/**/*.md` | 80 | Scaffolds |
| `.ai/project/agents/*.md` | 45 | Same as core cards |
| `.ai/project/skills/**/*.md` | 80 | Same as core skills |
| `.ai/project/wiki/rules/*.md` | 80 | Path-scoped project rules; load automatically whenever matching files are touched |
| `.ai/project/wiki/**/*.md` | 150 | Same as core pages |
| `.ai/project/lessons.md` | 120 | Read when relevant; prune or promote instead of growing |

## Why Page Count Is Not Budgeted

A page costs nothing until a route sends a reader to it. What costs is an always-loaded file that grows, a rule written twice, and a page no route reaches. So the wiki is bounded by ownership and routing, not by counting files: splitting a page to shrink an always-loaded file is a saving even though the page count rises.
