---
name: git-worktree-cleanup
description: "Clean up after a unit's pull request merged: confirm the merge, confirm nothing unsaved and nothing stacked on the branch, then remove the worktree (when worktrees are on) and delete the task branch, and update the main checkout to the latest integration branch. Use when a unit's pull request has merged, or the user asks to clean up merged worktrees or branches."
---
<!-- agentkit:generated from .ai/kit/core/skills/git-worktree-cleanup/SKILL.md. Do not edit: change the source, then run `python .ai/kit/tools/agentkit.py sync`. Paths are relative to the project root. -->

# Clean Up Merged Unit

## Use When
- A unit's pull request merged into the integration branch.
- The user asks to clean up worktrees or branches of merged work.

## Do Not Use When
- The pull request is open, closed without merging, or awaiting review → leave the worktree in place.
- The worktree or branch was not created for a task you can identify → stop and ask the user.

## Inputs
- The worktree path and task branch, and the pull request number.
- `policy.integration_branch` from the profile.

## Steps
1. From the main checkout, run `git fetch origin --prune` and confirm the pull request merged: `gh pr view <number> --json state,mergedAt` shows `MERGED`.
2. Check every condition in «Cleanup» in [git-workflow.md](.ai/kit/core/wiki/workflows/git-workflow.md):
   - `git log --oneline origin/<integration_branch>..<branch>` prints nothing. After a squash or rebase merge it may print commits; then compare the pull request's final diff with the integration branch, and stop and ask if they differ.
   - `git -C <worktree> status --short` prints nothing.
   - `gh pr list --base <branch> --state open` prints nothing.
3. If any condition fails, stop and report which one; never force-remove. With `policy.worktrees = false` there is no worktree: check the main checkout's `git status --short` instead, skip steps 4 and 5, and continue at step 6.
4. Make sure nothing is using the worktree: run every command from the main checkout (move your own shell there first), and ask the user to close terminals or editor windows opened in it. A process whose current directory is inside the worktree blocks deleting it, notably on Windows.
5. Remove the worktree: `git worktree remove <worktree>`, then `git worktree prune`. If it reports `Permission denied` after `git worktree list` no longer shows the worktree, Git already removed its files and only the empty directory is held by a process: once that process has left, delete the empty directory (`rmdir <worktree>`).
6. Delete the branch: `git branch -d <branch>` (use `-D` only after step 2 proved a squash merge), and `git push origin --delete <branch>` only when the remote branch still exists (with head-branch deletion on, the platform already removed it).
7. Update the main checkout: `git switch <integration_branch>` and `git pull --ff-only`.
8. Confirm with `git worktree list` and `git branch -a --list "*<branch>*"`.

## Output
- The removed worktree path and deleted branches, or the condition that stopped the cleanup.
