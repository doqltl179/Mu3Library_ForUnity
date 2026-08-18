# Git Workflow

## When

- branch, merge, push, release sync, or hotfix execution details are needed,
- `.github/instructions/git-workflow.instructions.md` gives the policy but not enough step detail.

## Branch Policy

- Integration branch: `develop`. Release branch: `main`. **Neither takes a direct commit.**
- Normal work happens on a task branch cut from `origin/develop`, named `<type>/<scope>-<summary>` in lowercase. `<type>` is a Conventional Commit type: `feat`, `fix`, `docs`, `chore`, `refactor`, `test`, `perf`. The scope matches the commit scope for the same work.
- A task branch may be checked out in its own worktree when the work is long-running or must not disturb the primary checkout. The pull request needs the branch; the worktree is optional.
- An activated [graph-engineering workflow](graph-engineering.md) creates its plan-declared `agent/<graph-id>/<node>` branches and worktrees instead. Their base, paths, `develop` destination, and cleanup gate must be recorded before creation.
- [`branch-strategy.yml`](../../../.github/workflows/branch-strategy.yml) enforces the destinations: into `develop` from a task branch, an `agent/...` branch, or `main` for release sync; into `main` only from `develop`.
- Being on a branch that does not belong to the current task is a hard stop: inspect and report it before any edit, stage, commit, merge, push, or deletion.
- Delete a merged task branch only after confirming its commits are contained in `develop`, then verify both the local and remote branch lists.

## Normal Flow

1. Run the branch preflight before editing:
   - `git status --short --branch`
   - `git branch --show-current`
   - `git fetch origin`
   - `git rev-list --left-right --count origin/develop...develop`
2. **Cut the task branch from `origin/develop`, not from whatever is checked out.**

   ```bash
   git fetch origin && git switch -c <type>/<scope>-<summary> origin/develop
   ```

   A new session starts on the branch last used. When that branch is already merged, work cut from it starts beside other changes instead of on top of them, and the conflict only surfaces at step 7.
3. Review `git diff` and `git status --short --untracked-files=all`, then group files by one concern at a time.
4. Stage explicit paths for the selected concern and commit it with a focused message. Never use `git add .` or stage an unclear untracked file.
   - Treat untracked Unity source and `.meta` files as user changes until ownership is clear. Do not silently stage, delete, or fold them into an unrelated commit; ask when ownership cannot be assigned.
5. Run the verification the touched surface needs, and keep the commands and their results for the pull request body.
6. Push the task branch and verify its remote tip: `git push -u origin <branch>`.
7. Open the pull request into `develop`, then check for conflicts and resolve them in place.
8. After it merges, delete the task branch locally and on the remote, then fast-forward local `develop` from `origin/develop`.
9. Releasing to `main` is a separate unit: open a pull request from `develop` into `main`, and after it merges sync `main` back into `develop` and verify both protected branches.

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

## Task Branch Cleanup

This repository does not delete merged branches automatically, so cleanup is a real step. It needs no separate approval once every condition below holds, because the commits provably survive in `develop`. A **worktree** is not covered here: it may hold a Unity `Library` directory and keeps its own explicit-approval requirement.

1. Confirm the pull request merged and `git log --oneline develop..<branch>` is empty, so the branch holds no unique commits.
2. Confirm no other open pull request was stacked on the branch. Keep it until that one merges too.
3. Delete the branch locally and on the remote, then verify both branch lists.
4. If any precondition fails, stop and report rather than improvising a merge or deletion.

## Pushable Status

Before pushing a protected branch (`develop` or `main`) to its remote counterpart during release sync:

1. Fetch and check ahead/behind state.
2. Confirm no merge or rebase conflict risk with the remote tip.
3. If local and remote are out of sync, stop the push flow.
4. Restore local branch state to before the release-sync attempt if a failed merge or rebase changed it.
5. Sync local branch with remote, then restart from the relevant merge or commit stage.

## Commit Policy

- Follow Conventional Commits. The type and scope stay English, and **the subject and body are written in Korean**; branch names stay English. History written before this rule keeps its original language, so do not rewrite past commits.
- Scope is lowercase and names either a package or the surface inside one: `base`, `urp`, `watermelon` for packages; `di`, `object-pool`, `audio`, `mvp`, `localization` and similar for a surface within a package; `release`, `agents`, `workflow`, `tooling`, `project` for repository-level work.
- Use `watermelon` for `Mu3Library_Game_WatermelonGame`, never `game`. One package answers to one name, here and in the label table below.
- Commit on a task branch, one concern per commit. `develop` and `main` receive changes only through a merged pull request.
- `develop` reaches `main` only through an explicit release pull request.
- Before every commit, inspect the staged path list and confirm it belongs to one concern.

```
feat(base): Localization AssetTable 로딩 지원
fix(urp): UnregisterEffectAll 타입 비교 수정과 IDisposable 추가
docs(agents): 이슈·라벨 관례를 git-workflow 정본에 추가
chore(release): base 0.26.0으로 버전 상향
```

## Issue To Pull Request

Handling a GitHub issue is one unit of work that ends at the pull request, not at a dirty working tree.

One issue gets one task branch and one pull request into `develop`. Releasing what accumulated on `develop` to `main` is a separate unit.

1. Run the preflight, then cut the task branch from `origin/develop` as in «Normal Flow».
2. Implement the change and run the verification the touched surface requires.
3. Commit with Conventional Commits, one concern per commit, then push the task branch.
4. Open the pull request into `develop`. Put `Closes #<issue>` in the body to link the issue, and apply labels from the table below.
   - `Closes #<issue>` **links** the issue but does not close it at this merge. GitHub closes an issue only when the reference reaches the default branch, which is `main`. The issue closes when the release pull request from `develop` merges. An issue still open after its task branch merged is expected; do not close it by hand and do not read it as a failed merge.
5. Check for conflicts and resolve them in place.
6. Register what is left as new issues, per the next section.
7. Record the verification commands and their results in the pull request body, and clean up the task branch once it merges.
8. Report that the unit is finished, naming what changed and the pull request number, and send a push notification when the environment can. Verification here drives the Unity Editor CLI through `compile-unity.sh` and takes minutes, so the person who asked is usually elsewhere; a bare "done" with no pull request number makes them open the browser to learn anything. Send it when you stop while blocked, too.

## Check For Conflicts After Opening The Pull Request

`develop` moves whenever another task branch merges. Check right after opening the pull request and resolve in place. "The PR is open" is not the end of the unit: a pull request that cannot merge is usually found days later by someone who no longer remembers the context.

```bash
gh pr view <number> --json mergeable,mergeStateStatus
# when CONFLICTING
git fetch origin && git merge origin/develop
```

Resolve by merging the base branch into the head branch. Do not rebase. Rewriting the history of a branch that is already pushed detaches the pull request's review comments from the code they point at.

A conflict is usually two decisions changing the same place, so read both before matching the text. Read the other side's commit message and the documents it changed, and judge whether that decision still holds after this change. Merge both when it does; when this change overrides it, say so in the merge commit message. Never drop one side silently, because the next person who wants it back cannot find why it went.

Run the verification again after merging. Two sides passing separately does not mean the merged result passes.

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
| Area | `watermelon` | `Mu3Library_Game_WatermelonGame`, matching the commit scope of the same name |
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

- The checked-out branch does not belong to the current task, or its name does not match the `<type>/<scope>-<summary>` form.
- A commit is about to land directly on `develop` or `main`.
- A graph branch or worktree is not present in the active plan, two running nodes overlap, or the integration destination moved from the recorded base.
- Untracked files cannot be assigned to a focused change without user direction.
- Local and remote branch state are out of sync.
- A merge, rebase, or push would overwrite remote work.
- Verification or reviewer approval is missing for a release-sensitive surface.
- The branch flow no longer matches the user-requested release or hotfix path.

## Hotfix Flow

1. Implement and verify the hotfix on a `fix/<scope>-<summary>` task branch cut from `origin/develop`. Do not create a `hotfix/*` branch; the CI check does not accept that prefix.
2. Merge it into `develop` through a pull request like any other task.
3. If the user explicitly requests a release, open the release pull request from `develop` into `main`.
4. Publish the patch release from `main`, then sync `main` back into `develop`.
