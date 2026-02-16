---
description: "Git branch and release execution policy for Codex tasks"
---

# Git Workflow Instructions

## Branch Policy

- Default working branch is `develop`.
- Release branch is `main`.
- Create additional working branches as needed (referred to as `workBranch` below, e.g. `feature/<topic>`).

## Execution Order For `workBranch`

When work is performed on `workBranch`, follow this order:

1. Validate and review local changes on `workBranch`.
2. Check whether local `develop` is mergeable, then merge local `workBranch` -> local `develop`, and validate/review `develop`.
3. If release work on `main` is requested:
   - Check whether local `main` is mergeable.
   - Merge local `develop` -> local `main` (release in progress).
   - Validate/review local `main`.
4. If remote `main` sync is requested:
   - Check whether remote `main` is pushable (no conflict risk).
   - Push local `main` -> remote `main`.
   - Validate/review remote `main` status.
5. After release is completed successfully on `main`:
   - Re-sync local `main` -> local `develop` (to bring possible main-side documentation updates back).
   - Remove local `workBranch` (only if `workBranch` was created for this task).

## "Pushable" Status Definition

Before pushing local `main` to remote `main`, verify conflict risk first:

1. Confirm local/remote sync state (`git fetch` + ahead/behind check).
2. Confirm there is no merge/rebase conflict risk with the current remote tip.
3. If local and remote are out of sync:
   - Stop the push flow.
   - Restore local branch state to before the release-sync attempt.
     - Example: `git reset --hard ORIG_HEAD` (if a merge/rebase changed local history during sync attempt).
     - Example: `git rebase --abort` or `git merge --abort` (if an operation is still in progress).
   - Sync local branch with remote first.
   - Re-run the release sync steps from the merge stage.

## Commit Policy

- Documentation-only commits are allowed directly on `main`.
- Never make non-documentation commits directly on `main`.
- All code changes must be committed on at least `develop` (or `workBranch`) first, then synchronized into `main`.
- If needed, create additional working branches and integrate through `develop`.

## Release Policy

- Releases must be performed through `main`.
- Every release must include release notes describing changes.
- Keep release note format as consistent as possible across versions.
- The goal is to prevent release-note omissions, not to over-constrain release style.

## Standard Hotfix Flow

When an urgent production fix is needed, use this standard flow:

1. Create `hotfix/<topic>` from `main`.
2. Implement and verify fix on `hotfix/<topic>`.
3. Merge `hotfix/<topic>` -> `main` and publish patch release.
4. Sync `main` -> `develop` to keep branches aligned after hotfix.
