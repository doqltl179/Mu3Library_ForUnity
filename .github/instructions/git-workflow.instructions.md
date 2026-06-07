---
description: "Compact Git branch and release execution policy for Codex tasks"
---

# Git Workflow Instructions

Use this instruction when branch, merge, push, release sync, or hotfix flow is requested.

Detailed flow guide: `docs/ai-agents/workflow/git-workflow.md`.

## Branch Policy

- Default working branch: `develop`.
- Release branch: `main`.
- Additional task branches may be created as `workBranch` values such as `feature/<topic>` or `hotfix/<topic>`.

## Core Rules

- Validate and review changes before merging between branches.
- Do not make non-documentation commits directly on `main`.
- Code changes should land on `develop` or a task branch before release sync to `main`.
- Release work is published from `main`.
- Before pushing `main`, check local/remote sync and conflict risk.
- After a successful release from `main`, sync `main` back into `develop`.

## Stop Conditions

- Local and remote branch state are out of sync.
- A merge, rebase, or push would overwrite remote work.
- Verification or reviewer approval is missing for release-sensitive surfaces.
- The branch flow no longer matches the user-requested release or hotfix path.
