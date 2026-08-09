---
description: "Compact Git branch and release execution policy for Codex tasks"
---

# Git Workflow Instructions

Use this instruction when branch, merge, push, release sync, or hotfix flow is requested.

Detailed flow guide: `docs/ai-agents/workflow/git-workflow.md`.

## Branch Policy

- Default working branch: `develop`.
- Release branch: `main`.
- The normal branch allowlist is exactly `develop` and `main`; agents must not create or use another branch by default.
- A task branch is an exception only when the user explicitly provides its exact name, purpose, merge destination, and cleanup plan before work begins.
- If the current branch is not `develop` or `main` and no such authorization exists, stop before editing, staging, or committing and report the branch mismatch.

## Core Rules

- At task start, record `git status --short --branch`, `git branch --show-current`, `git branch -vv`, and the ahead/behind state against the intended remote branch.
- On `develop`, classify the working tree by concern before staging. Stage explicit paths per concern and create one focused commit per concern; do not use broad staging such as `git add .`.
- Treat untracked Unity source and `.meta` files as user changes until ownership is clear. Do not silently stage, delete, or mix them into an unrelated commit.
- Validate and review changes before merging between branches.
- Do not make non-documentation commits directly on `main`.
- Code changes should land on `develop` before release sync to `main`.
- Release work is published from `main`.
- Before pushing either protected branch, fetch the remote and check local/remote sync and conflict risk. Stop if the remote is ahead or diverged.
- After a successful release from `main`, sync `main` back into `develop`.
- Remove an obsolete branch only after confirming its commits are contained in the destination or explicitly preserved elsewhere; verify both local and remote branch lists afterward.

## Stop Conditions

- The checked-out branch is outside the normal allowlist without explicit user authorization.
- Untracked files cannot be assigned to a focused change without user direction.
- Local and remote branch state are out of sync.
- A merge, rebase, or push would overwrite remote work.
- Verification or reviewer approval is missing for release-sensitive surfaces.
- The branch flow no longer matches the user-requested release or hotfix path.
