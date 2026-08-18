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

## Issue To Pull Request

Handling a GitHub issue is one unit of work that ends at the pull request, not at a dirty working tree.

This repository has no feature-branch path. [`branch-strategy.yml`](../../../.github/workflows/branch-strategy.yml) accepts a pull request into `main` only from `develop`, and into `develop` only from `main` for release sync. Do not open a pull request from a task branch; it fails that check.

1. Confirm `develop` is checked out and synchronized with `origin/develop` using the preflight above.
2. Implement the change and run the verification the touched surface requires.
3. Commit with Conventional Commits, one concern per commit, then push `develop`.
4. Open the pull request from `develop` into `main`. Put `Closes #<issue>` in the body to link the issue, and apply labels from the table below.
5. Check for conflicts and resolve them in place.
6. Register what is left as new issues, per the next section.
7. Record the verification commands and their results in the pull request body.

## Leftovers Belong In Issues, Not The Pull Request Body

Nearly every task ends with something still to do. Written only in the pull request body it is buried the moment the PR merges, because nobody re-reads a closed PR. Register follow-up work as a GitHub issue instead.

Register an issue for:

- Work deliberately deferred out of scope, including anything recorded as "not done in this PR".
- A defect or inconsistency found while implementing: where docs and code disagree, or where a convention names something that does not exist yet.
- Follow-up that cannot finish inside the repository, such as a package registry submission or an external account change.
- Something a document already specifies but no code implements.

Do not register an issue for:

- Anything fixable now. Fix it instead; an issue is not a way to defer.
- Something an open issue already covers. Comment on that issue; duplicates cost the list its signal.
- A broad unimplemented area the roadmap already excludes. Splitting it into issues turns the list into a copy of the roadmap.

Every issue body states three things: **where it came from** (the originating issue or PR number), **why it is needed**, and **what closes it**. An item with no "what closes it" is not an issue yet but a pending judgment; label it `analysis` and write that judgment as the task.

## Labels

Apply labels to both issues and pull requests. There are two axes, and each gets exactly one label.

| Axis | Label | When |
|---|---|---|
| Kind | `bug` | Behavior differs from intent |
| Kind | `enhancement` | New feature or improvement |
| Kind | `documentation` | README, CHANGELOG, or agent-framework docs |
| Kind | `follow-up` | Split off from other work; nearly always present on an issue registered by the rule above |
| Kind | `analysis` | The deliverable is an investigation or a decision rather than code |
| Area | `base` | `Mu3Library_Base` |
| Area | `urp` | `Mu3Library_URP` |
| Area | `game` | `Mu3Library_Game_WatermelonGame` |
| Area | `agents` | Agent framework: `.github/agents`, `instructions`, `prompts`, `skills`, and `docs/ai-agents/` |
| Area | `tooling` | `tools/`, `compile-unity.sh`, and `.github/workflows/` |

Work inside a `UnityProject_*` development project takes the area of the package it exercises.

**Create a new label when none fits.** Never force one on: a label whose meaning is off is worse than no label. Always pass `--description` when creating one. An undescribed label makes the next person guess its meaning, and from that point the same label carries two meanings.

```bash
gh label list
gh label create <name> --color <hex> --description "<when to apply it>"
gh issue create --title "<title>" --label follow-up --label base --body "..."
gh pr edit <number> --add-label documentation --add-label agents
```

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
