---
name: git-commit
description: "Record changes as commits: group working-tree changes by concern, stage explicit paths per group, inspect the staged set, and write each message per the commit convention in the commit language. Use when verified changes on a task branch are ready to record, or when the working tree mixes changes for more than one concern."
category: git
volatility: evolving
reviewed: 2026-10-09
---

# Commit Changes

## Use When
- Verified changes on a task branch are ready to record.
- The working tree mixes changes for several concerns that need separate commits.

## Do Not Use When
- The current branch is the integration branch or a protected branch → `git-branch-start` first.
- A merge is in progress → `git-conflict-resolve`.
- The change has not been verified → verify per [verification.md](../../wiki/workflows/verification.md) first.

## Inputs
- The task branch and its uncommitted changes.
- `policy.commit_convention` and `policy.commit_language` (empty means `project.language`).
- Linked issue numbers, if any.

## Steps
1. Confirm the branch is not in `policy.protected_branches`: `git branch --show-current`.
2. List the changes: `git status --short` and `git diff --stat`. Read each diff and note the concern it serves. Leave unstaged any path this task did not change, and report it.
3. Group the changes into commits, one concern per commit, per «Staging» in [git-workflow.md](../../wiki/workflows/git-workflow.md).
4. Stage one group by explicit path per «Staging»: `git add -- <path>...`, never a whole-tree form such as `git commit -a`. When one file holds two concerns, write this group's hunks to a patch and stage it with `git apply --cached <patch>`; interactive staging is unavailable.
5. Inspect the staged set: `git diff --cached --stat`, then `git diff --cached`. Unstage anything outside the group with `git restore --staged -- <path>`. Confirm nothing staged breaks «Secrets And Sensitive Data» in [integrity.md](../../wiki/principles/integrity.md).
6. Compose the message per «Commit Convention» in git-workflow.md, in `policy.commit_language`. Write it to a temporary file outside the repository.
7. Commit: `git commit -F <file>`. If a hook fails, fix the cause, re-stage, and commit again; never bypass hooks (see «Never Fake Success»).
8. Repeat steps 4–7 for each remaining group.
9. Confirm `git status --short` lists only paths left uncommitted on purpose, and review `git log --oneline <integration_branch>..HEAD`.

## Output
- Each new commit's short hash and subject.
- Paths left uncommitted and why.
