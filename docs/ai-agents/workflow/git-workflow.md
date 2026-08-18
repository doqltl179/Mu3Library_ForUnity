# Git Workflow

## When

- branch, merge, push, release sync, or hotfix execution details are needed,
- `.github/instructions/git-workflow.instructions.md` gives the policy but not enough step detail.

## Branch Policy

- Default working branch: `develop`.
- Release branch: `main`.
- Normal serial work uses only `develop` and `main`.
- An activated [graph-engineering workflow](graph-engineering.md) may create only its exact plan-declared local `agent/<graph-id>/...` branches and worktrees. Their base, paths, `develop` destination, and cleanup gate must be recorded before creation.
- Outside graph engineering, a task branch is permitted only after explicit user authorization naming the branch, its merge destination, and its cleanup plan.
- An unexpected task branch is a hard stop: inspect and report it before any edit, stage, commit, merge, push, or deletion.
- Remove an obsolete branch only after confirming its commits are contained in the destination or explicitly preserved elsewhere, then verify both the local and remote branch lists afterward.

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
   - Treat untracked Unity source and `.meta` files as user changes until ownership is clear. Do not silently stage, delete, or fold them into an unrelated commit; ask when ownership cannot be assigned.
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

## Parallel Worktree Flow

Use [graph-engineering.md](graph-engineering.md) as the lifecycle owner. Git execution follows these boundaries:

1. Require a clean, fetched, synchronized `develop`, then record its commit as the immutable graph base.
2. Under `orchestrator` control, `release-manager` creates the plan-declared integration branch/worktree and ready node branches/worktrees. Keep the primary `develop` worktree free of node edits.
3. Require focused candidate commits and reviewer disposition over exact IDs.
4. `release-manager` cherry-picks only approved IDs on the revisioned integration branch in topological order. Stop and abort the current cherry-pick on conflicts, unknown files, or base mismatch.
5. The plan-named verification owner runs combined checks, then `reviewer` reviews the evidence on the integration branch.
6. Fetch again. If local or remote `develop` moved, increment the graph revision and build new revisioned resources; preserve old resources and revalidate on the new base.
7. `release-manager` fast-forwards local `develop` only to the exact reviewer-approved integration tip while it remains `HEAD`. Do not push unless the user requested it.
8. Before deletion, `release-manager` inventories exact worktree paths/sizes, dirty state, unique commits, and each contained Unity `Library` path/size. Obtain general cleanup confirmation plus a separate confirmation naming each exact Unity `Library`; never force-remove a dirty worktree.

Never place a worktree inside a Unity `Library` directory, share a Unity `Library` across worktrees, force-update a branch, reuse a stale revision name, or auto-rebase a stale graph.

## Explicit Task-Branch Exception

This section applies outside the plan-declared graph exception.

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

## Stop Conditions

Stop and report instead of improvising when any of these holds.

- The checked-out branch is outside the allowlist above and no explicit user authorization exists.
- A graph branch or worktree is not present in the active plan, two running nodes overlap, or the integration destination moved from the recorded base.
- Untracked files cannot be assigned to a focused change without user direction.
- Local and remote branch state are out of sync.
- A merge, rebase, or push would overwrite remote work.
- Verification or reviewer approval is missing for a release-sensitive surface.
- The branch flow no longer matches the user-requested release or hotfix path.

## Hotfix Flow

1. Implement and verify the hotfix on `develop`; do not create a `hotfix/*` branch automatically.
2. If the user explicitly requests a release, synchronize the verified `develop` into `main`.
3. Publish the patch release from `main`.
4. Sync `main` back into `develop` after the release succeeds.
