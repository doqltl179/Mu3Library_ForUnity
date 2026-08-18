---
description: "Entry card for branch, merge, push, release-sync, and hotfix work; routes to the owning Git workflow page"
---

# Git Workflow Instructions

Use this instruction when branch, merge, push, release sync, or hotfix flow is requested.

**This file routes only.** Branch policy, the preflight and normal flow, the task-branch exception, worktree rules, pushable status, commit, release, hotfix flow, and every stop condition are owned by [git-workflow.md](../../docs/ai-agents/workflow/git-workflow.md). Read that page before the first Git command, and do not restate its rules here.

## Route

| Need | Open |
|---|---|
| Branch policy, preflight, normal flow, task-branch exception, pushable status, commit/release/hotfix flow, stop conditions | [git-workflow.md](../../docs/ai-agents/workflow/git-workflow.md) |
| Node branches, worktree isolation, ready waves, fan-in, cleanup gates | [graph-engineering.md](../../docs/ai-agents/workflow/graph-engineering.md) |
| Version bump, tag, changelog, GitHub Release execution | [release.instructions.md](release.instructions.md), [release-execution.md](../../docs/ai-agents/workflow/release-execution.md) |
| Who performs the Git lifecycle for graph work | [routing/README.md](../../docs/ai-agents/routing/README.md) |

## Before The First Command

Two things gate every Git action here; both are detailed on the owning page.

- Run the branch preflight and confirm the current branch is authorized for this work.
- Confirm no stop condition applies. If one does, report it instead of working around it.
