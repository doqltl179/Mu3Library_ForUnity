---
description: "Compact Git branch and release execution policy for Codex tasks"
---

# Git Workflow Instructions

Use this instruction when branch, merge, push, release sync, or hotfix flow is requested.

Detailed flow guide: `docs/ai-agents/workflow/git-workflow.md`.

## Branch Policy

- Default working branch: `develop`.
- Release branch: `main`.
- The normal branch allowlist is `develop` and `main` plus local `agent/<graph-id>/...` branches recorded by an activated [graph-engineering plan](../../docs/ai-agents/workflow/graph-engineering.md).
- Outside graph engineering, a task branch is an exception only when the user explicitly provides its exact name, purpose, merge destination, and cleanup plan before work begins.
- If the current branch is not `develop` or `main` and no such authorization exists, stop before editing, staging, or committing and report the branch mismatch.

## Graph Worktree Exception

- Keep the primary `develop` worktree clean while node agents work in exact plan-declared worktrees.
- Graph branches are local-only by default. Under `orchestrator` control, `release-manager` may create planned branches/worktrees and locally integrate reviewed commits after the activation gate passes; pushing or deleting them is not authorized without the applicable user request or cleanup confirmation.
- Use one branch and worktree per node, plus one integration branch/worktree. Do not let two agents edit the same worktree.
- Create dependent nodes from the current integration tip containing their predecessors; cherry-pick only exact reviewer-approved IDs in topological order on a revisioned integration branch.
- Fast-forward `develop` only after combined verification and only if local and remote `develop` still match the recorded base. Never force, auto-rebase, or guess through a conflict.
- Before cleanup, inspect exact worktree paths/sizes, dirty state, unique commits, and every contained Unity `Library` path/size. Obtain general confirmation plus separate confirmation naming each exact `Library`; never force-remove a dirty worktree.

## Core Rules

- At task start, record `git status --short --branch`, `git branch --show-current`, `git branch -vv`, and the ahead/behind state against the intended remote branch.
- On `develop`, classify the working tree by concern before staging. Stage explicit paths per concern and create one focused commit per concern; do not use broad staging such as `git add .`.
- Treat untracked Unity source and `.meta` files as user changes until ownership is clear. Do not silently stage, delete, or mix them into an unrelated commit.
- Validate and review changes before merging between branches.
- Do not make non-documentation commits directly on `main`.
- Code changes should land on `develop` before release sync to `main`.
- Release work is published from `main`.
- Before pushing either protected branch, fetch the remote and check local/remote sync and conflict risk. Stop if the remote is ahead or diverged.
- Never place a worktree under a Unity `Library` directory or share a Unity `Library` directory across worktrees.
- After a successful release from `main`, sync `main` back into `develop`.
- Remove an obsolete branch only after confirming its commits are contained in the destination or explicitly preserved elsewhere; verify both local and remote branch lists afterward.

## Stop Conditions

- The checked-out branch is outside the normal allowlist without explicit user authorization.
- A graph branch/worktree is not present in the active plan, two running nodes overlap, or the integration destination moved from the recorded base.
- Untracked files cannot be assigned to a focused change without user direction.
- Local and remote branch state are out of sync.
- A merge, rebase, or push would overwrite remote work.
- Verification or reviewer approval is missing for release-sensitive surfaces.
- The branch flow no longer matches the user-requested release or hotfix path.
