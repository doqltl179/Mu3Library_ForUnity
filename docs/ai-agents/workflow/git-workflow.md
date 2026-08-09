# Git Workflow

## When

- branch, merge, push, release sync, or hotfix execution details are needed,
- `.github/instructions/git-workflow.instructions.md` gives the policy but not enough step detail.

## Branch Policy

- Default working branch: `develop`.
- Release branch: `main`.
- Normal agent work uses only `develop` and `main`; no task branch is created automatically.
- A task branch is permitted only after explicit user authorization naming the branch, its merge destination, and its cleanup plan.
- An unexpected task branch is a hard stop: inspect and report it before any edit, stage, commit, merge, push, or deletion.

## Normal Flow

1. Run the branch preflight before editing:
   - `git status --short --branch`
   - `git branch --show-current`
   - `git branch -vv`
   - `git fetch origin`
   - `git rev-list --left-right --count origin/develop...develop`
2. Stop if the current branch is not `develop` for normal work, or if the intended branch is ahead/diverged from its remote unexpectedly.
3. Review `git diff` and `git status --short --untracked-files=all`, then group files by one concern at a time.
4. Stage explicit paths for the selected concern and commit it with a focused message. Never use `git add .` or stage an unclear untracked file.
5. Validate local `develop`, fetch again, and confirm it is not behind or diverged from `origin/develop`.
6. Push `develop` and verify its remote tip.
7. If release on `main` is explicitly requested:
   - check whether local `main` is mergeable,
   - merge local `develop` into local `main`,
   - validate and review local `main`.
8. If remote `main` sync is requested:
   - verify pushable status,
   - push local `main` to remote `main`,
   - validate remote status.
9. After release succeeds on `main`:
   - sync local `main` back into local `develop`,
   - verify both protected branches.

## Explicit Task-Branch Exception

1. Record the user-authorized branch name, destination, and cleanup plan before creating it.
2. Keep the branch focused and classify/stage paths by concern.
3. Validate it, merge it into the authorized destination, and verify the destination.
4. Delete the local and remote task branch only after confirming no unique commits or required untracked files would be lost.
5. If any precondition fails, stop rather than improvising a merge or deletion.

## Pushable Status

Before pushing local `develop` or `main` to its remote counterpart:

1. Fetch and check ahead/behind state.
2. Confirm no merge or rebase conflict risk with the remote tip.
3. If local and remote are out of sync, stop the push flow.
4. Restore local branch state to before the release-sync attempt if a failed merge or rebase changed it.
5. Sync local branch with remote, then restart from the relevant merge or commit stage.

## Commit Policy

- Documentation-only commits are allowed directly on `main`.
- Non-documentation commits must not be made directly on `main`.
- Code changes should be committed on `develop`, one concern per commit, then synchronized into `main` only for an explicit release.
- Before every commit, inspect the staged path list and confirm it belongs to one concern.

## Release Policy

- Releases are performed through `main`.
- Every release needs release notes.
- Release note consistency matters, but omission prevention is the priority.

## Hotfix Flow

1. Implement and verify the hotfix on `develop`; do not create a `hotfix/*` branch automatically.
2. If the user explicitly requests a release, synchronize the verified `develop` into `main`.
3. Publish the patch release from `main`.
4. Sync `main` back into `develop` after the release succeeds.
