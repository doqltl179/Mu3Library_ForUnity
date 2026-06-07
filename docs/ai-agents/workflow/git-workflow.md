# Git Workflow

## When

- branch, merge, push, release sync, or hotfix execution details are needed,
- `.github/instructions/git-workflow.instructions.md` gives the policy but not enough step detail.

## Branch Policy

- Default working branch: `develop`.
- Release branch: `main`.
- Task branches are `workBranch` values such as `feature/<topic>` or `hotfix/<topic>`.

## WorkBranch Flow

1. Validate and review local changes on `workBranch`.
2. Check whether local `develop` is mergeable.
3. Merge local `workBranch` into local `develop`.
4. Validate and review `develop`.
5. If release on `main` is requested:
   - check whether local `main` is mergeable,
   - merge local `develop` into local `main`,
   - validate and review local `main`.
6. If remote `main` sync is requested:
   - verify pushable status,
   - push local `main` to remote `main`,
   - validate remote status.
7. After release succeeds on `main`:
   - sync local `main` back into local `develop`,
   - remove local `workBranch` only if it was created for this task.

## Pushable Status

Before pushing local `main` to remote `main`:

1. Fetch and check ahead/behind state.
2. Confirm no merge or rebase conflict risk with the remote tip.
3. If local and remote are out of sync, stop the push flow.
4. Restore local branch state to before the release-sync attempt if a failed merge or rebase changed it.
5. Sync local branch with remote, then restart from the merge stage.

## Commit Policy

- Documentation-only commits are allowed directly on `main`.
- Non-documentation commits must not be made directly on `main`.
- Code changes should be committed on `develop` or `workBranch`, then synchronized into `main`.

## Release Policy

- Releases are performed through `main`.
- Every release needs release notes.
- Release note consistency matters, but omission prevention is the priority.

## Hotfix Flow

1. Create `hotfix/<topic>` from `main`.
2. Implement and verify the fix on the hotfix branch.
3. Merge `hotfix/<topic>` into `main`.
4. Publish the patch release.
5. Sync `main` back into `develop`.
