---
name: git-worktree-cleanup
description: "Remove a finished unit's worktree and task branch after its pull request merged: confirm the merge, confirm the worktree holds nothing unsaved and nothing is stacked on the branch, then remove the worktree and delete the local and remote branch, and update the main checkout. Use when a unit's pull request has merged, or the user asks to clean up merged worktrees."
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
3. If any condition fails, stop and report which one; never force-remove.
4. Make sure nothing is using the worktree: run every command from the main checkout (move your own shell there first), and ask the user to close terminals or editor windows opened in it. A process whose current directory is inside the worktree blocks deleting it, notably on Windows.
5. Remove the worktree: `git worktree remove <worktree>`, then `git worktree prune`. If it reports `Permission denied` after `git worktree list` no longer shows the worktree, Git already removed its files and only the empty directory is held by a process: once that process has left, delete the empty directory (`rmdir <worktree>`).
6. Delete the branch: `git branch -d <branch>` (use `-D` only after step 2 proved a squash merge), and `git push origin --delete <branch>` only when the remote branch still exists (with head-branch deletion on, the platform already removed it).
7. Update the main checkout: `git switch <integration_branch>` and `git pull --ff-only`.
8. Confirm with `git worktree list` and `git branch -a --list "*<branch>*"`.

## Output
- The removed worktree path and deleted branches, or the condition that stopped the cleanup.
