---
name: git-branch-start
description: "Start a unit's workspace: run the preflight in the main checkout, then create the unit's own worktree and task branch from the freshly fetched integration branch (or, with worktrees off, cut the branch in place), named from the unit's issue, stopping on any unexpected repository state. Use when a unit with an issue is about to change tracked files and has no worktree or task branch yet."
category: git
volatility: evolving
reviewed: 2026-10-09
---

# Start Unit Worktree

## Use When
- A bounded unit is about to change tracked files and has no worktree or task branch yet.
- Its issue exists, or `policy.issue_first` is off or `hosting.platform` is `none`.

## Do Not Use When
- The unit has no issue yet and `policy.issue_first` is on → `github-issue-create` first.
- A worktree for this unit already exists → work inside it; record work with `git-commit`.
- The task branch exists but lags its base → `git-conflict-resolve`.
- The work is not yet one bounded unit → `work-decompose` first.

## Inputs
- The unit's issue number and title, and its Conventional Commits type.
- From the profile: `policy.integration_branch`, `policy.worktrees`, `policy.worktree_root`, `policy.branch_pattern`, `policy.protected_branches`.

## Steps
1. In the main checkout, run «Preflight» in [git-workflow.md](../../wiki/workflows/git-workflow.md). If anything is unexpected, stop per «Stop Conditions» and report the exact output; do not stash, reset, or discard work this task did not create.
2. Compose the branch name from `policy.branch_pattern` per «Branch Policy», using the issue title for the summary.
3. Confirm the name is free: `git branch --list <name>` and `git ls-remote --heads origin <name>` print nothing; with worktrees, `git worktree list` shows no worktree at the target path. Otherwise stop and ask whether it belongs to this unit.
4. With `policy.worktrees` on, create the worktree per «Worktrees»: `git worktree add --no-track -b <name> <worktree_root>/<name-with-slashes-as-dashes> origin/<integration_branch>`. Confirm `<worktree_root>` is ignored by Git (`git check-ignore <worktree_root>`); if not, stop and report.
5. With `policy.worktrees` off, cut the branch in place: `git switch -c <name> --no-track origin/<integration_branch>`.
6. Move into the new workspace for every later command. If `commands.install` is set, run it there.
7. Confirm `git branch --show-current` prints `<name>` and `git status --short` prints nothing.

## Output
- The worktree path (or "in place"), the task branch name, the issue number, and the integration-branch commit it starts from (`git rev-parse --short HEAD`).
- Any preflight finding that stopped or changed the procedure.
