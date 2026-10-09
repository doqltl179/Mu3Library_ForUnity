---
owns: "Local Git policy: branch roles, preflight, per-unit worktrees and task branches, commit convention, staging, integrating base changes, cleanup, and stop conditions"
volatility: evolving
reviewed: 2026-10-09
---

# Git Workflow

Branch names and switches come from the profile: `policy.integration_branch`, `policy.release_branch`, `policy.protected_branches`, `policy.worktrees`, `policy.worktree_root`, `policy.branch_pattern`, `policy.commit_convention`, `policy.commit_language`.

## When

- before the first edit of a unit, and before any commit, merge, push, worktree, or branch deletion.

## Route Away When

- issues, pull request targets, and labels: [issues-and-prs.md](issues-and-prs.md),
- versions, tags, and promotion to the release branch: [release.md](release.md),
- parallel units and fan-in: [concurrency.md](../operating-model/concurrency.md).

## Branch Policy

| Branch | Role |
|---|---|
| `policy.integration_branch` (default `develop`) | Development. Every unit starts from it and its pull request targets it |
| `policy.release_branch` (default `main`) | Release. Changes arrive only through a promotion pull request from the integration branch ([release.md](release.md)) |
| Task branch | One per unit, named by `policy.branch_pattern`, living in its own worktree |

- Never commit directly to a branch in `policy.protected_branches`; changes arrive through merged pull requests.
- With Conventional Commits, the branch `<type>` is one of `feat`, `fix`, `docs`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, and the scope matches the commit scope.
- One task branch carries one bounded unit and, when `policy.issue_first` is on, one issue.

## Preflight

Run in the main checkout before creating a unit's worktree, and in the worktree before committing:

```bash
git status --short --branch
git fetch origin
```

Stop if the checkout is not the one expected for this step, holds changes you cannot attribute, or its branch diverged from its remote.

## Worktrees

When `policy.worktrees` is on (the default), every unit runs in its own worktree:

```bash
git fetch origin
git worktree add --no-track -b <branch> <worktree_root>/<branch-with-slashes-as-dashes> origin/<integration_branch>
```

- The main checkout stays on the integration branch, clean. Use it only to coordinate: read, create issues and worktrees, keep a multi-unit plan. Never edit project files there.
- Run every command of the unit inside its worktree. Dependencies and build outputs are per worktree; install them there with `commands.install`.
- Never share a worktree between units or reuse one for a different branch.
- `--no-track` keeps the task branch from tracking the integration branch; the first `git push -u origin <branch>` sets its own upstream.
- A fresh worktree holds only tracked files. It lacks dependency folders, build outputs, engine import caches, local configuration, and untracked links or junctions. `commands.worktree_setup` recreates what the project needs and runs right after the worktree is created.
- Before relying on worktrees, weigh the setup cost per worktree: disk (`agentkit.py capacity` shows free disk) and first-setup time, such as a full engine re-import. When it outweighs the isolation, set `policy.worktrees = false`: units then run one at a time in the main checkout.

When `policy.worktrees` is off, cut the task branch in the current checkout: `git switch -c <branch> --no-track origin/<integration_branch>`.

## Task Branch

Always cut from the freshly fetched remote integration branch, never from whatever is checked out: a branch cut from an already-merged branch starts beside other work and conflicts late. To build on an unmerged unit, stack the new branch on that unit's branch and state the merge order in both pull requests.

## Commit Convention

- With `policy.commit_convention = "conventional"`: `<type>(<scope>): <subject>`, type and scope in English and lowercase, subject and body in `policy.commit_language` (or `project.language` when empty).
- One concern per commit. The body explains why, not what. Reference the unit's issue (`Refs #<n>`).
- Never rewrite commits that are already pushed.

## Staging

- Stage explicit paths for one concern. Never `git add .` or `git add -A` on a tree you have not inspected.
- Inspect `git diff --cached --stat` before every commit.
- Treat untracked files you did not create as the user's: do not stage, move, or delete them without asking.
- Never stage secrets, build outputs, or files the project ignores.

## Integrating Base Changes

- To bring a pushed branch up to date, merge the base into it (`git merge origin/<base>`). Do not rebase a pushed branch: it detaches review comments and breaks collaborators.
- Rebasing is acceptable only on local, unpushed commits.
- Resolve conflicts by reading both sides' intent (commit messages, linked issues, docs); never drop a side silently. The `git-conflict-resolve` skill walks this.
- Re-run verification after every merge; two passing sides do not guarantee a passing merge.

## Cleanup

With head-branch deletion on («Repository Settings» in [issues-and-prs.md](issues-and-prs.md)), the platform deletes the remote task branch at merge. After a unit's pull request merged, the `git-worktree-cleanup` skill removes its worktree (with `policy.worktrees = false`, only the task branch) and updates the main checkout, so the next unit starts from the latest integration branch. This needs no further confirmation when every condition holds; otherwise stop and ask:

- the pull request is merged and `git log origin/<integration_branch>..<branch>` is empty,
- the worktree has no uncommitted or untracked changes,
- no open pull request is stacked on the branch,
- this task created the worktree and branch.

## Stop Conditions

Stop and report instead of improvising when:

- a commit is about to land on a protected branch, or project files are about to be edited in the main checkout while `policy.worktrees` is on,
- the branch or worktree does not match the unit or the naming pattern,
- local and remote state diverged, or a push would overwrite remote work,
- files cannot be attributed to the current concern,
- verification or a required review is missing for what is about to be pushed.
